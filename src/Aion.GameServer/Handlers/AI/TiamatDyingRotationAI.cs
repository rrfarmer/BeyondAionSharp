using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tiamat in her dying phase (219362). Retail pattern
/// <c>IDTiamat_Tiamat_Dragon_Dying_Named_60_Al</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Replaces the aionemu class's approximation for
/// this NPC. The difference is not a detail: <c>TiamatWeakenedDragonAI</c> chooses its breath with
/// <c>Rnd.NextInt(3)</c>, where retail runs a <b>fixed rotation of 45 steps across four health
/// bands</b>. A learnable sequence replaced by a coin flip changes how the fight is played — in
/// retail a raid pre-positions for the next breath, and against a die it cannot.
/// <list type="bullet">
/// <item><b>76-100</b> — M, M, L, R, eighteen seconds a step</item>
/// <item><b>51-75</b> — L, M, R, then three thorn rows five seconds apart, then R, M, L</item>
/// <item><b>26-50</b> — twelve-second breaths off the <c>Beacon*8s</c> marks, with thorn rows and a
/// cyclops crack every two seconds, then the same run in reverse</item>
/// <item><b>0-25</b> — eight-second breaths off the <c>Beacon*4s</c> marks, and gravity bombs and a
/// quake join the thorns and cracks</item>
/// <item><b>unbanded</b> — a three-second heartbeat, which every banded chain hangs off</item>
/// </list>
/// <para>
/// <b>The telegraph is the point.</b> Each breath step first places a beacon for seven seconds, and
/// the beacon's heading picks the cone: dir 17 for left, none for middle, 105 for right. None of that
/// existed here — the beacons were spawned by nothing at all, so every breath arrived unannounced.
/// </para>
/// <para>
/// <b>Half the casts are translated.</b> The top two bands address indices 1/2/3, resolved to 20922 /
/// 20924 / 20926 by their stack names (<c>IDTIAMAT_TIAMAT_BREATH{L,M,R}_CAST</c>, unique in the skill
/// table, and agreeing with the branch comment, the beacon number and the index all at once). The
/// lower two bands address 6/8/10 and 7/9/11 — faster-cast variants, as their <c>Beacon*8s</c> and
/// <c>Beacon*4s</c> marks imply — whose ids are <b>not resolved</b>. Those bands place their beacons
/// and hazards faithfully and cast nothing, which is the honest half-translation: the telegraph and
/// the ground hazards are what a raid reads, and inventing a skill id would be a guess in the one
/// place this work does not guess.
/// </para>
/// <para>
/// <b>Evaluation order is retail's, flat.</b> <see cref="TiamatRotation"/> keeps the steps in document
/// order rather than grouped by band because the ordering between bands matters here — see its own
/// remarks on the 51..74 / 51..75 boundary.
/// </para>
/// <para>
/// <b>Not translated:</b> the <c>set_idle_timer</c> / <c>change_direction</c> pair on leaving the
/// fight, and the wake-up furniture (a dust effect and an instance timer NPC), which belong to the
/// instance's own sequencing rather than to the fight.
/// </para>
/// </remarks>
[AIName("tiamat_dying_rotation")]
public class TiamatDyingRotationAI : PatternAi
{
    /// <summary>Retail arms the chain seven seconds after she is engaged.</summary>
    private const int OpeningMillis = 7000;

    /// <summary>Retail's <c>SPAWN_ID_1</c>: everything the rotation places goes under it.</summary>
    private const int Placed = 1;

    /// <summary>
    /// The breaths whose ids resolve, by the index the pattern addresses. See the class remarks —
    /// only the top two bands' indices are here, and the rest deliberately cast nothing.
    /// </summary>
    private static readonly Dictionary<int, int> ResolvedBreaths = new Dictionary<int, int>
    {
        [1] = 20922, // Ultimate Atrocity, stack IDTIAMAT_TIAMAT_BREATHL_CAST
        [2] = 20924, // ...BREATHM_CAST
        [3] = 20926, // ...BREATHR_CAST
    };

    private static sbyte Facing(int degrees) =>
        (sbyte)PositionUtil.ConvertAngleToHeading((degrees + 360) % 360);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId = new();
    private static readonly AiPattern Nothing = new AiPattern();

    private static AiPattern Build(int npcId)
    {
        if (!TiamatRotation.ByBoss.TryGetValue(npcId, out TiamatRotation.Step[]? table))
            return Nothing;

        var branches = new List<PatternBranch>();

        // Priorities descend in the table's own order, which is retail's evaluation order.
        int priority = table.Length;

        foreach (TiamatRotation.Step step in table)
        {
            var actions = new List<PatternAction> { Do.ArmTimer(step.NextTimer, step.DelayMillis) };

            foreach (TiamatRotation.Placement spawn in step.Spawns)
            {
                var spot = new SpawnSpot(spawn.X, spawn.Y, spawn.Z, Facing(spawn.Degrees));
                var spots = new SpawnSpot[spawn.Count];
                for (int i = 0; i < spawn.Count; i++)
                    spots[i] = spot;
                actions.Add(Do.SpawnAt(spawn.NpcId, Placed, spawn.LiveSeconds, spots));
            }

            // The label names the direction, and the index agrees with it; both are checked against
            // the skill's own stack name in the table above rather than trusted on their own.
            foreach (int index in step.SkillIndices)
            {
                if (ResolvedBreaths.TryGetValue(index, out int skillId))
                    actions.Add(Do.SkillOnSelf(skillId));
            }

            PatternCondition[] guards = step.Banded
                ? [When.Timer(step.Timer), When.HpBetween(step.Low, step.High)]
                : [When.Timer(step.Timer)];

            branches.Add(Branch(priority--, step.Label, guards, actions.ToArray()));
        }

        return new AiPattern
        {
            OnEnterAttack = Of(
                Branch(priority, "SetTimer", When.Always,
                    Do.ArmTimer(0, OpeningMillis))),

            OnBattleTimer = Of(branches.ToArray()),

            // Retail's Despawn_All: leaving the fight or dying clears everything she placed.
            OnLeaveAttack = Of(
                Branch(priority, "Despawn_All", When.Always,
                    Do.Despawn(Placed))),

            OnDie = Of(
                Branch(priority, "Despawn_All", When.Always,
                    Do.Despawn(Placed))),
        };
    }

    public TiamatDyingRotationAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => Build(id));
}
