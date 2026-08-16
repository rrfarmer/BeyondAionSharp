using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// A fortress gate that puts a squad out when something attacks it. Retail patterns
/// <c>BGuard_*Gate*</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Despite sharing a prefix with the abyss guards
/// this is a different mechanic, and reading it as the other one would have been wrong: a gate does
/// not call for help as it weakens. It is attacked, waits, and puts out waves on a fixed chain —
/// no health bands, no coin flips, the same squad every time.
/// <list type="bullet">
/// <item>being attacked arms the chain, ten seconds out on the common variants</item>
/// <item>each wave arms the next and places its squad two metres out for ten minutes</item>
/// <item>most gates then stand a few seconds and remove themselves</item>
/// <item>the fortress-chief gates instead <b>loop</b> back to the first wave and keep going for as
/// long as the fight lasts</item>
/// <item>leaving the fight clears the squad and takes the gate with it</item>
/// </list>
/// <para>
/// <b>The chain lengths are the table's, not an assumption:</b> of 62 pattern variants, 50 put out
/// two waves, six put out three, three put out four and three put out one.
/// </para>
/// <para>
/// <b>Not translated:</b> the <c>on_message</c> handler on 10009, which dismisses a gate. Nothing
/// ported sends it — it belongs to the fortress siege code rather than to any NPC — so translating
/// it would add a listener with no speaker.
/// </para>
/// </remarks>
[AIName("gate_squad")]
public class GateSquadAI : PatternAi
{
    /// <summary>Retail's <c>SPAWN_ID_1</c>: leaving the fight clears exactly this group.</summary>
    private const int Squad = 1;

    /// <summary>Ten minutes, two metres out — the same on every branch in the family.</summary>
    private const int SquadLife = 600;
    private const float Nearby = 2f;

    /// <summary>
    /// The slot the chain uses for its last link — the one whose only job is to remove the gate.
    /// Retail numbers it after the waves, and so does this.
    /// </summary>
    private static int ClosingSlot(int waves) => waves;

    private static readonly ConcurrentDictionary<int, AiPattern> ByNpcId = new ConcurrentDictionary<int, AiPattern>();
    private static readonly AiPattern Nothing = new AiPattern();

    private static AiPattern Build(int npcId)
    {
        if (!GateSquads.ByGate.TryGetValue(npcId, out GateSquads.Squad squad) || squad.Waves.Length == 0)
            return Nothing;

        var branches = new List<PatternBranch>();
        int priority = squad.Waves.Length + 2;

        for (int i = 0; i < squad.Waves.Length; i++)
        {
            GateSquads.Wave wave = squad.Waves[i];
            var actions = new List<PatternAction>();

            bool last = i == squad.Waves.Length - 1;
            int nextSlot = last
                ? (squad.LoopsTo >= 0 ? squad.LoopsTo : ClosingSlot(squad.Waves.Length))
                : i + 1;
            int nextDelay = last
                ? (squad.LoopsTo >= 0 ? squad.Waves[squad.LoopsTo].DelayMillis : squad.DespawnAfterMillis)
                : squad.Waves[i + 1].DelayMillis;

            actions.Add(Do.ArmTimer(nextSlot, nextDelay));
            foreach ((int npc, int count) in wave.Summons)
                actions.Add(wave.OnTarget
                    ? Do.SpawnOnTarget(npc, Squad, count: count, range: Nearby, liveSeconds: SquadLife)
                    : Do.SpawnNear(npc, Squad, count: count, range: Nearby, liveSeconds: SquadLife));

            branches.Add(Branch(priority--, "", [When.Timer(i)], actions.ToArray()));
        }

        // A looping gate has no closing link: retail's last wave arms the first one again.
        if (squad.LoopsTo < 0)
        {
            branches.Add(Branch(priority--, "and it is gone",
                [When.Timer(ClosingSlot(squad.Waves.Length))],
                Do.DespawnSelf()));
        }

        return new AiPattern
        {
            OnEnterAttack = Of(
                Branch(priority, "", When.Always,
                    Do.ArmTimer(0, squad.Waves[0].DelayMillis))),

            OnBattleTimer = Of(branches.ToArray()),

            // Retail's on_leave_attack_state: the squad goes, and so does the gate.
            OnLeaveAttack = Of(
                Branch(priority, "", When.Always,
                    Do.Despawn(Squad),
                    Do.DespawnSelf())),
        };
    }

    public GateSquadAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => Build(id));
}
