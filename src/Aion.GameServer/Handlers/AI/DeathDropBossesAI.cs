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
/// <b>One trap, once, and only in the middle of the fight.</b> An earlier translation of this class
/// read the trap branch as a six-second loop; it carries both an <c>is_hp_in_boundary</c> band and a
/// test-and-set flag var, so it lays a single trap and only while he is between <b>36 and 70</b>
/// percent. Found by <c>tools/client-extract/audit_pattern_guards.py</c>, which exists because of it.
/// </para>
/// <para>
/// <b>What the rest of the timer-2 chain is for is <em>when</em> that trap can land.</b> Timer 2 is
/// armed at twenty-five seconds and every other branch on it only casts — but they re-arm it at
/// different delays per band, and that decides how soon after entering the band he gets his chance:
/// </para>
/// <list type="table">
/// <item><term>76–100</term><description>seventeen seconds</description></item>
/// <item><term>36–70, trap already laid</term><description>seventeen seconds</description></item>
/// <item><term>below 35</term><description>hands off to timer 3, which comes back to timer 2
/// eighteen seconds later — the one place the chain leaves its own timer</description></item>
/// <item><term>anything else, 71–75 included</term><description>six seconds</description></item>
/// </list>
/// <para>
/// Those branches are kept although their casts are not, because each re-arms at a delay the
/// fallback does not. A branch earns its place by changing what happens.
/// </para>
/// <para>
/// <b>One retail branch is dead in retail.</b> The below-35 rung is written twice — priority 10 with
/// no flag var and priority 9 with one, otherwise identical. Ten always matches first, so nine can
/// never run. Collapsed to one here rather than reproduced as a pair.
/// </para>
/// <para>
/// <b>Not translated.</b> Thirteen skill indices on timers 0, 1, 3 and 4; the <c>valid_distance</c> of
/// fifty on the trap spawn, which retail uses to skip the spawn when the target is further off than
/// that; and the <c>on_see_friend_attacked</c> / <c>on_friend_spelled</c> pair, which is a cast and a
/// target switch.
/// </para>
/// </remarks>
[AIName("takahan")]
public class TakahanAI : PatternAi
{
    /// <summary><c>BIDCTN_TrapA_55_An</c>.</summary>
    private const int ExplosiveTrap = 281619;

    private const int Traps = 1;

    private const float OnThem = 5f;

    /// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> — the trap is laid once.</summary>
    private const int TrapLaid = 1;

    private const int FirstCheckMillis = 25000;
    private const int SlowMillis = 17000;
    private const int QuickMillis = 6000;
    private const int HandBackMillis = 18000;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timers 0, 1 and 4 here; all three are cast loops.
        OnEnterAttack = Of(
            Branch(11, "", When.Always,
                Do.ArmTimer(2, FirstCheckMillis))),

        OnBattleTimer = Of(
            // Retail writes this rung twice, at 10 and at 9; the second carries a flag var and can
            // never run behind the first. It is the only branch that does not re-arm timer 2.
            Branch(10, "below 35", [When.Timer(2), When.HpBelow(35)],
                Do.ArmTimer(3, 9000)),

            Branch(8, "", [When.Timer(3)],
                Do.ArmTimer(2, HandBackMillis)),

            Branch(7, "36-70 trap", [When.Timer(2), When.HpBetween(36, 70), When.FirstTime(TrapLaid)],
                Do.ArmTimer(2, QuickMillis),
                Do.SpawnOnTarget(ExplosiveTrap, Traps, count: 1, range: OnThem)),

            Branch(6, "36-70", [When.Timer(2), When.HpBetween(36, 70)],
                Do.ArmTimer(2, SlowMillis)),

            Branch(5, "76-100", [When.Timer(2), When.HpBetween(76, 100)],
                Do.ArmTimer(2, SlowMillis)),

            Branch(1, "", [When.Timer(2)],
                Do.ArmTimer(2, QuickMillis))),
    };

    public TakahanAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
