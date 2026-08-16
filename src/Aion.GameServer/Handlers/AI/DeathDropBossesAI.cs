using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Three named bosses whose whole translatable pattern is one line: something is left behind when a
/// player kills them. Retail patterns <c>FD2_FrA</c> (Menotios), <c>NLehpar_BhA</c> (RM-78c) and
/// <c>BLehpar_FhA</c> (RA-45c).
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All three were on plain <c>aggressive</c> with no
/// AI class, found by <c>tools/client-extract/audit_missing_ai.py</c>, and what each leaves behind was
/// reachable by nobody.
/// <list type="table">
/// <item><term>Menotios (251001)</term><description>an <b>aetherback titan core</b> (290116), twenty
/// seconds</description></item>
/// <item><term>RM-78c (212211)</term><description>a <b>strange creature</b> (280790), two
/// minutes</description></item>
/// <item><term>RA-45c (213764)</term><description>a <b>strange object</b> (280714), two
/// minutes</description></item>
/// </list>
/// <para>
/// <b>On <c>on_killed_by_user</c>, not <c>on_die</c>.</b> Retail distinguishes them and all three use
/// the player-kill form, so nothing is left when one of these dies to anything else. Our runtime
/// raises one death event, which is the closest we have: the difference only shows for an NPC killed
/// by another NPC, and none of the three is in a place where that happens.
/// </para>
/// <para>
/// <b>What they leave is not loot.</b> Two of the three are <c>ntrap</c> and <c>strange_creature</c>
/// — NPCs with their own behaviour, which is why they are worth reaching at all. The lifetimes are
/// retail's and they differ threefold between them, which is the sort of number a shared constant
/// would have swallowed.
/// </para>
/// <para>
/// <b>Not translated.</b> Sixty-three skill indices between the three of them, across timers that
/// carry nothing else. These are cast-driven bosses whose only index-free line is the one below.
/// </para>
/// </remarks>
[AIName("death_drop_boss")]
public class DeathDropBossAI : PatternAi
{
    /// <summary>What each boss leaves, and for how long.</summary>
    private readonly record struct Drop(int NpcId, int LiveSeconds);

    private static readonly Dictionary<int, Drop> ByBoss = new Dictionary<int, Drop>
    {
        [251001] = new Drop(290116, 20),   // FD2_FrA      — menotios
        [212211] = new Drop(280790, 120),  // NLehpar_BhA  — rm-78c
        [213764] = new Drop(280714, 120),  // BLehpar_FhA  — ra-45c
    };

    /// <summary>Retail's <c>SPAWN_ID_1</c>. Nothing ever clears it; the lifetime does.</summary>
    private const int Left = 1;

    /// <summary>Which NPC this boss leaves behind, or 0 for one that is not in the table.</summary>
    internal static int DropFor(int bossId) => ByBoss.TryGetValue(bossId, out Drop d) ? d.NpcId : 0;

    /// <summary>How long it stays.</summary>
    internal static int DropLifeFor(int bossId) => ByBoss.TryGetValue(bossId, out Drop d) ? d.LiveSeconds : 0;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnDie = Of(
            Branch(7, "", When.Always,
                Do.Custom(ai =>
                {
                    if (ByBoss.TryGetValue(ai.GetOwner().GetNpcId(), out Drop drop))
                        ai.SpawnNear(drop.NpcId, Left, count: 1, range: 0f, liveSeconds: drop.LiveSeconds);
                }))),
    };

    public DeathDropBossAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Takahan (216884), the Dredgion surkana boss. Retail pattern <c>Dread02_SurkanaNm06</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. On plain <c>aggressive</c> with no AI class, and
/// the <b>explosive trap</b> (281619) his fight is built on reachable by nobody.
/// <para>
/// One timer does it: armed at twenty-five seconds when the fight starts, it drops a trap on whoever
/// he is fighting, five metres out, and comes back every <b>six</b> seconds after that. So the first
/// one is slow and then they are relentless — which is the shape of the fight rather than a detail,
/// and a single interval would have got it wrong in both directions.
/// </para>
/// <para>
/// <b>Not translated.</b> Thirteen skill indices on timers 0, 1, 3 and 4, and the branch on timer 3
/// that re-arms timer 2 at eighteen seconds while casting — the re-arm is reproduced only through
/// timer 2's own six-second loop, because translating a branch whose sole other action is a cast
/// would put a bare re-arm in the table with nothing to justify it.
/// </para>
/// </remarks>
[AIName("takahan")]
public class TakahanAI : PatternAi
{
    /// <summary><c>BIDCTN_TrapA_55_An</c>.</summary>
    private const int ExplosiveTrap = 281619;

    private const int Traps = 1;

    private const float OnThem = 5f;

    private const int FirstTrapMillis = 25000;
    private const int TrapIntervalMillis = 6000;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timers 0, 1 and 4 here; all three are cast loops.
        OnEnterAttack = Of(
            Branch(11, "", When.Always,
                Do.ArmTimer(2, FirstTrapMillis))),

        OnBattleTimer = Of(
            Branch(2, "", [When.Timer(2)],
                Do.ArmTimer(2, TrapIntervalMillis),
                Do.SpawnOnTarget(ExplosiveTrap, Traps, count: 1, range: OnThem))),
    };

    public TakahanAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
