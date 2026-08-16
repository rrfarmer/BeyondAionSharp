using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// An abyss guard that calls for reinforcements as it is worn down. Retail patterns
/// <c>DGuard_*</c> and <c>LGuard_*</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. This is the largest single cluster in the
/// missing-adds backlog and it is one mechanic, not eighty: <b>86 pattern variants covering 460
/// guards</b>, each calling up its own level bracket's attackers and healer on the same timer, in the
/// same bands, at the same odds. What differs between variants is which npcs get called — so the
/// structure lives here and the facts live in <see cref="GuardReinforcements"/>, read out of the
/// patterns by <c>tools/client-extract/extract_guard_reinforcements.py</c>.
/// <list type="bullet">
/// <item>entering combat arms timer 0 at twenty seconds</item>
/// <item>timer 0 re-arms itself and lights timers 1 and 2 — 1 is the call, 2 is casts we do not
/// translate</item>
/// <item>timer 1 calls up whatever this guard's band says, three metres out, for ten minutes</item>
/// <item>leaving the fight sends them all away</item>
/// </list>
/// <para>
/// <b>The bands are retail's own, gaps included.</b> Most guards are written as
/// <c>is_hp_lower_than 35</c> against <c>is_hp_in_boundary 36..70</c> and <c>71..100</c>, so a guard
/// sitting at exactly 35% matches no band and calls nobody. That is left as it is rather than tidied
/// into <c>0..35</c>: a one-percent dead spot is what retail ships, and closing it would be a change
/// dressed up as a translation.
/// </para>
/// <para>
/// <b>Two shapes, from the table rather than from a guess.</b> 344 guards have a single band — below
/// 35, always, one kind of summon. 116 have the full three-band escalation at a coin flip each. The
/// shape census over every branch in the family found 198 of 205 identical (timer 1, ten-minute
/// lifetime, three metres, own point); the seven that differ are the artifact and officer variants,
/// which place absolutely and are not covered here.
/// </para>
/// <para>
/// <b>The casts are not translated</b> — thirteen indices are addressed across the family and no guard
/// carries thirteen skills. Timer 2 and the friend-rescue handlers (<c>on_see_friend_attacked</c>,
/// <c>on_friend_spelled</c>) are cast-only and go with them, as does the <c>on_message</c> pair on
/// 10001. What is here is index-free: the timers, the bands, the counts and the summons.
/// </para>
/// </remarks>
[AIName("guard_reinforcement")]
public class GuardReinforcementAI : PatternAi
{
    /// <summary>Retail's <c>SPAWN_ID_1</c>: leaving the fight clears exactly this group.</summary>
    private const int Called = 1;

    private const int HeartbeatMillis = 20000;
    private const int CallDelayMillis = 1000;

    /// <summary>Timer 2 carries only casts, but it is armed so the table still reads against retail.</summary>
    private const int CastTimerMillis = 10000;

    /// <summary>Ten minutes, three metres out — the same on every branch in the family.</summary>
    private const int ReinforcementLife = 600;
    private const float Nearby = 3f;

    /// <summary>
    /// One pattern per guard npc id. Built on demand because the table differs per guard and a
    /// fortress holds hundreds of them.
    /// </summary>
    private static readonly ConcurrentDictionary<int, AiPattern> ByNpcId = new ConcurrentDictionary<int, AiPattern>();

    /// <summary>A guard whose id is not in the table does nothing beyond being aggressive.</summary>
    private static readonly AiPattern Nothing = new AiPattern();

    private static AiPattern Build(int npcId)
    {
        if (!GuardReinforcements.ByGuard.TryGetValue(npcId, out GuardReinforcements.Band[]? bands))
            return Nothing;

        var branches = new List<PatternBranch>
        {
            // The heartbeat, and the two timers it lights.
            Branch(11, "", [When.Timer(0)],
                Do.ArmTimer(0, HeartbeatMillis),
                Do.ArmTimer(1, CallDelayMillis),
                Do.ArmTimer(2, CastTimerMillis)),
        };

        // Bands cannot overlap, so their order relative to each other does not matter; they are
        // numbered downwards from the heartbeat so the table reads like the pattern it came from.
        int priority = 10;
        foreach (GuardReinforcements.Band band in bands)
        {
            var actions = new List<PatternAction>();
            foreach ((int summonId, int count) in band.Summons)
                actions.Add(Do.SpawnNear(summonId, Called, count: count, range: Nearby,
                    liveSeconds: ReinforcementLife));

            PatternCondition[] guards = band.Chance >= 100
                ? [When.Timer(1), When.HpBetween(band.Low, band.High)]
                : [When.Chance(band.Chance), When.Timer(1), When.HpBetween(band.Low, band.High)];

            branches.Add(Branch(priority--, "", guards, actions.ToArray()));
        }

        return new AiPattern
        {
            OnEnterAttack = Of(
                Branch(13, "", When.Always,
                    Do.ArmTimer(0, HeartbeatMillis))),

            OnBattleTimer = Of(branches.ToArray()),

            // Retail's on_leave_attack_state. Without it a guard that resets leaves its wave standing.
            OnLeaveAttack = Of(
                Branch(12, "", When.Always,
                    Do.Despawn(Called))),
        };
    }

    public GuardReinforcementAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern =>
        ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => Build(id));
}
