using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The four drakan magisters of Tiamat's Stronghold — 219370, 219375, 219386 and 219399. Retail
/// patterns <c>IDTiamat_R2_NobleDrakanWi_60_Ae</c>, <c>IDTiamat_S1_…</c>, <c>IDTiamat_S3_Sardha…</c>
/// and <c>IDTiamat_S5_Tiamat…</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All four were on plain <c>aggressive</c>, and the
/// four patterns are identical branch for branch — a rare case where a family really is one thing
/// four times.
/// <para>
/// <b>Two hands, and only two.</b> Ten seconds into the fight a slot starts ticking; the first time it
/// finds the mage below eighty percent it puts a <b>mystical tyrhund</b> (282989) down at its own
/// feet for a minute, and the first time below thirty it does it again. Both rungs carry a flag var,
/// so a fight spent in either band pays once — and the slot keeps ticking either way, every six or
/// seven seconds, which is what makes the second hand land promptly rather than whenever the next
/// crossing happens to be noticed.
/// </para>
/// <para>
/// <b>A near miss worth recording.</b> Three of the four patterns first read as having no flag vars at
/// all, which would have meant a hand every seven seconds from eighty percent down — a completely
/// different fight. That was a scratch <c>grep</c> filter dropping the <c>set_flag_var</c> lines,
/// not the data. The rule this work already has — <i>a scratch regex is fine for finding candidates
/// and not for stating facts</i> — earned its keep again, and the difference was one filter away from
/// being written into the log as a retail quirk.
/// </para>
/// <para>
/// <b>Not translated:</b> three skill indices — the area attack on a seven-second loop and the
/// heal-reduction on a fifteen-second one, both of which are casts and nothing else.
/// </para>
/// </remarks>
[AIName("tiamat_drakan_mage")]
public class TiamatDrakanMageAI : PatternAi
{
    /// <summary><c>TiamatDrakan_MagicHand</c> — the mystical tyrhund.</summary>
    private const int MagicHand = 282989;

    /// <summary>Retail's <c>SPAWN_ID_1</c>, its <c>spawn_range</c> and its <c>live_time</c>.</summary>
    private const int Hands = 1;
    private const float Reach = 1f;
    private const int HandLife = 60;

    private const int Hand = 2;

    // Retail's ALPHA_1 and ALPHA_2.
    private const int Below80 = 1;
    private const int Below30 = 2;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(6, "", When.Always,
                Do.ArmTimer(Hand, 10000))),

        OnBattleTimer = Of(
            Branch(3, "below 80", [When.Timer(Hand), When.HpBelow(80), When.FirstTime(Below80)],
                Do.ArmTimer(Hand, 7000),
                Do.SpawnNear(MagicHand, Hands, count: 1, range: Reach, liveSeconds: HandLife)),

            Branch(2, "below 30", [When.Timer(Hand), When.HpBelow(30), When.FirstTime(Below30)],
                Do.ArmTimer(Hand, 7000),
                Do.SpawnNear(MagicHand, Hands, count: 1, range: Reach, liveSeconds: HandLife)),

            Branch(1, "", [When.Timer(Hand)],
                Do.ArmTimer(Hand, 6000))),
    };

    public TiamatDrakanMageAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The terath magisters of the Dreadgion — Captain Anusa (233371), the thaumaturge (233354) and the
/// worldwarper (233358). Retail patterns <c>IDDreadgion_03_DrakanWi_Boss_Ae</c>,
/// <c>…_Noble_60_Ae</c> and <c>…_Tiamat_60_Ae</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All three were on plain <c>aggressive</c>. Their
/// patterns are identical except for when they clear up, which is why one class covers them.
/// <para>
/// <b>Every fifteen seconds a great magical barrier lands on somebody.</b>
/// <c>spawn_on_target_by_attacker_indicator</c> with a random attacker, eight seconds of life, and
/// <c>attack_target_after_spawn</c> with a single hate point. That barrier is itself a hazard engine;
/// see <see cref="GreatMagicalBarrierAI"/>.
/// </para>
/// <para>
/// <b>The hate point is carried and is not observable here.</b> A barrier is an aggressive npc landing
/// on top of a player, so it engages that player whether or not retail seeded a point of hate first;
/// removing <c>attack_target_after_spawn</c> changes nothing our harness can read, and it is left as a
/// deliberate mutation survivor rather than given a pin that cannot fail. What <em>is</em> pinned is
/// that the barrier picks a <b>random attacker</b> rather than the tank, measured by which player each
/// one lands nearest to over ten of them.
/// </para>
/// <para>
/// <b>And below thirty a hand lands on the tank.</b> Once: the rung does not re-arm its slot, so the
/// six-second clock that carries it is over as soon as it fires. Unlike the krall escape two entries
/// ago this one is observable, because there is no flag var doing the same job — remove the missing
/// re-arm and hands rain every six seconds.
/// </para>
/// <para>
/// <b>They clear up at different moments, and that is the only difference between the three.</b>
/// Captain Anusa despawns the group <c>on_wake_up</c> — so a second pull starts clean — while the
/// other two do it <c>on_die</c>. Ported as written; the asymmetry is retail's.
/// </para>
/// <para>
/// <b>Not translated:</b> three skill indices — the stumble attack on a six-second loop, the ranged
/// attack that accompanies the barrier, and the area fire on a fifteen-second one. Also
/// <c>IDDreadgion_03_DrakanWi_Vil_60_Ae</c> (233350, terath magician), whose whole pattern is casts
/// and re-arms with no spawn in it at all — recorded so it is not mistaken for a gap.
/// </para>
/// </remarks>
[AIName("dreadgion_drakan_mage")]
public class DreadgionDrakanMageAI : PatternAi
{
    /// <summary><c>TiamatDrakan_AntiMagicalArea</c> — the great magical barrier.</summary>
    private const int Barrier = 282984;

    /// <summary><c>TiamatDrakan_MagicHand</c> — the mystical tyrhund.</summary>
    private const int MagicHand = 282989;

    /// <summary>Retail's <c>SPAWN_ID_1</c>: barrier and hand share one group, so one despawn clears both.</summary>
    private const int Placed = 1;

    private const int BarrierLife = 8;
    private const int HandLife = 30;
    private const float HandReach = 5f;

    /// <summary>Retail's <c>hatepoints_to_add</c>: one, which is enough to fix it on who it landed on.</summary>
    private const int BarrierHate = 1;

    private const int Ranged = 1;
    private const int Hand = 3;

    /// <summary>Captain Anusa, who clears up on waking rather than on dying.</summary>
    private const int Anusa = 233371;

    private static PatternBranch[] Timers => Of(
        Branch(4, "a barrier on somebody", [When.Timer(Ranged)],
            Do.SpawnOnAttacker(AggroTarget.RANDOM, Barrier, Placed, liveSeconds: BarrierLife,
                attackHate: BarrierHate),
            Do.ArmTimer(Ranged, 15000)),

        // Does not re-arm timer 3, so this happens once however long the last third lasts.
        Branch(2, "a hand on the tank", [When.HpBelow(30), When.Timer(Hand)],
            Do.SpawnOnTarget(MagicHand, Placed, count: 1, range: HandReach, liveSeconds: HandLife)),

        Branch(1, "", [When.Timer(Hand)],
            Do.ArmTimer(Hand, 6000)));

    private static readonly AiPattern OnDying = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(6, "", When.Always,
                Do.ArmTimer(Ranged, 7000),
                Do.ArmTimer(Hand, 6000))),

        OnBattleTimer = Timers,

        OnDie = Of(
            Branch(7, "", When.Always,
                Do.Despawn(Placed))),
    };

    private static readonly AiPattern OnWaking = new AiPattern
    {
        OnWakeUp = Of(
            Branch(7, "", When.Always,
                Do.Despawn(Placed))),

        OnEnterAttack = Of(
            Branch(6, "", When.Always,
                Do.ArmTimer(Ranged, 7000),
                Do.ArmTimer(Hand, 6000))),

        OnBattleTimer = Timers,
    };

    private readonly AiPattern pattern;

    public DreadgionDrakanMageAI(Npc owner)
        : base(owner)
    {
        pattern = owner.GetNpcId() == Anusa ? OnWaking : OnDying;
    }

    protected override AiPattern Pattern => pattern;
}

/// <summary>
/// The great magical barrier (282984) the Dreadgion magisters drop on people. Retail pattern
/// <c>IDYun_Temp_65</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>It is not a debuff, it is a pulse.</b> One second after something engages it, and every two
/// seconds after that, it leaves an <b>invisible</b> copy of itself (282985) where it stands for two
/// seconds. So the ground under it is being re-hazarded continuously for the eight seconds it lives,
/// which is how retail builds a standing area effect out of npcs rather than out of an aura.
/// </para>
/// <para>
/// <b>And it takes them with it.</b> <c>on_despawn</c> clears the group, so the pulses stop the moment
/// the barrier's eight seconds are up rather than lingering two seconds past it.
/// </para>
/// <para>
/// <b>Not translated:</b> the cast it opens with, and retail's <c>set_idle_timer delay=0</c> in the
/// despawn branch — an idle slot being cleared on the way out, which our runtime resets anyway.
/// </para>
/// </remarks>
[AIName("great_magical_barrier")]
public class GreatMagicalBarrierAI : PatternAi
{
    /// <summary><c>TiamatDrakan_AntiMagicalArea_invisible</c>.</summary>
    private const int Pulse = 282985;

    private const int Pulses = 1;
    private const int PulseLife = 2;
    private const int Beat = 0;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(2, "", When.Always,
                Do.ArmTimer(Beat, 1000))),

        OnBattleTimer = Of(
            Branch(1, "", [When.Timer(Beat)],
                Do.SpawnNear(Pulse, Pulses, count: 1, liveSeconds: PulseLife),
                Do.ArmTimer(Beat, 2000))),

        OnDespawn = Of(
            Branch(3, "", When.Always,
                Do.Despawn(Pulses))),
    };

    public GreatMagicalBarrierAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
