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
/// <b>All nine breaths are translated.</b> The three cast times retail ships — twelve seconds, eight
/// and four — line up with the three beacon families the bands use, the stack names spell the
/// direction and the duration, and the index pairs interleave in the same order as the skill ids.
/// See <see cref="ResolvedBreaths"/>.
/// <para>
/// <b>Hard mode casts the same skills, and that is an inference from absence.</b> The skill table has
/// hard-specific <i>damage</i> halves for every breath (<c>IDTIAMAT_HARD_TIAMAT_BREATH*_DMG</c>) and
/// <b>no hard cast half at all</b>, so there is nothing else it could be casting. That is weaker
/// evidence than normal mode's name match — it rests on the absence being deliberate rather than an
/// omission in the data — and it is flagged here rather than buried.
/// </para>
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
    /// Every breath the rotation casts, by the index the pattern addresses.
    /// </summary>
    /// <remarks>
    /// All nine resolve, and the structure is what settles them. Retail has exactly three breath cast
    /// times — twelve seconds, eight and four — and the skill table names them
    /// <c>BREATH{L,M,R}_CAST</c>, <c>BREATH{L,M,R}8S_CAST</c> and <c>BREATH{L,M,R}4S_CAST</c>, with
    /// <c>duration</c> 12000, 8000 and 4000 to match. The bands that address indices 6-11 place the
    /// <c>Beacon*8s</c> and <c>Beacon*4s</c> marks, which is the same claim from the other side.
    /// <para>
    /// The index numbering closes it: 6/8/10 and 7/9/11 are interleaved L/M/R pairs, and so are the
    /// skill ids — 21149/21151 for left, 21153/21155 for middle, 21157/21159 for right, even for four
    /// seconds and odd for eight. Four independent orderings agreeing is not a coincidence to be
    /// hedged against.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<int, int> ResolvedBreaths = new Dictionary<int, int>
    {
        [1] = 20922,  // BREATHL_CAST,   12s
        [2] = 20924,  // BREATHM_CAST,   12s
        [3] = 20926,  // BREATHR_CAST,   12s
        [6] = 21149,  // BREATHL4S_CAST,  4s — the 0-25 band's Beacon*4s marks
        [8] = 21153,  // BREATHM4S_CAST,  4s
        [10] = 21157, // BREATHR4S_CAST,  4s
        [7] = 21151,  // BREATHL8S_CAST,  8s — the 26-50 band's Beacon*8s marks
        [9] = 21155,  // BREATHM8S_CAST,  8s
        [11] = 21159, // BREATHR8S_CAST,  8s
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
