using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Grand Chieftain Saendukal (211040) and his Beluslan twin (280338). Retail pattern <c>ND2_RnI</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Both were on plain <c>aggressive</c>, and the fight
/// is a <b>peel ladder and nothing else</b>: four health bands, each of which opens a relay that never
/// closes, and each relay peels by a different rule.
/// <list type="table">
/// <item><term>on engaging</term><description>a call at fifty metres and a turn onto a <b>random</b>
/// attacker</description></item>
/// <item><term>crossing eighty</term><description>the <b>weakest</b> player, and again every forty
/// seconds</description></item>
/// <item><term>crossing sixty-five</term><description>the weakest again, on a <b>second</b> relay at
/// thirty-five seconds</description></item>
/// <item><term>crossing fifty</term><description>the <b>second</b>-most-hated, on a third relay at
/// thirty-six seconds</description></item>
/// <item><term>below twenty</term><description>the <b>third</b>-most-hated, on a fourth at
/// thirty-five</description></item>
/// </list>
/// <para>
/// <b>The relays do not stack, and that is worth saying because they look as though they should.</b>
/// Each band opens a relay on its own timer, and every one of those relays carries the band's own
/// health guard as well — so dropping out of a band silences its relay even though the timer is still
/// going round. The Akairun of Medeus is the boss that <em>does</em> stack, and the difference between
/// the two is one <c>is_hp_in_boundary</c> per relay branch.
/// </para>
/// <para>
/// <b>The last one is the exception.</b> The relay below twenty carries no health guard at all, so it
/// runs to the end of the fight — and since the rung that opens it does not re-arm the heartbeat, it is
/// also the only relay left by then.
/// </para>
/// <para>
/// <b>Two bands peel the same way and are still two bands.</b> Eighty and sixty-five both take the
/// weakest player, on separate timers at forty and thirty-five seconds — so crossing sixty-five does
/// not change what he does, it doubles how often he does it. A class that noticed the repetition and
/// merged them would halve the pressure of the second half of the fight.
/// </para>
/// <para>
/// <b>The ladder stops below twenty.</b> That rung does not re-arm the heartbeat, so the four relays
/// are all there will be.
/// </para>
/// <para>
/// <b>Not translated.</b> Thirty-one skill indices and five shouts — this is a boss who casts a great
/// deal and whose casting we cannot say. The <c>1001</c> broadcast on engaging is sent: it is answered
/// across the dump by patterns that add hate and attack, which is what a chieftain calling his camp
/// looks like. His <c>on_enter_idle_state</c> flag housekeeping does nothing our runtime can observe.
/// </para>
/// </remarks>
[AIName("grand_chieftain_saendukal")]
public class GrandChieftainSaendukalAI : PatternAi
{
	/// <summary>Retail's <c>1001</c> at fifty metres, naming whoever pulled him.</summary>
	private const int CallTheCamp = 1001;
	private const float Earshot = 50f;

	// Retail's FLAGVARI_ALPHA_1..3 and GAMMA_1: one per band crossing.
	private const int Below80 = 1;
	private const int Below65 = 2;
	private const int Below50 = 3;
	private const int Below20 = 4;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(15, "", When.Always,
				Do.ArmTimer(0, 13000),
				Do.ArmTimer(1, 12000),
				Do.Broadcast(CallTheCamp, Earshot, aboutTarget: true),
				Do.SwitchTarget(AggroTarget.RANDOM))),

		OnBattleTimer = Of(
			Branch(14, "and keeps taking the third", [When.Timer(7)],
				Do.ArmTimer(7, 30000),
				Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

			// Below twenty the heartbeat is not re-armed: four relays are all there will be.
			Branch(13, "below twenty, the third-most-hated", [When.Timer(0), When.HpBelow(20),
					When.FirstTime(Below20)],
				Do.ArmTimer(7, 35000),
				Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

			Branch(12, "and keeps taking the second", [When.Timer(4), When.HpBetween(21, 50)],
				Do.ArmTimer(4, 36000),
				Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

			Branch(9, "crossing fifty, the second-most-hated", [When.Timer(0), When.HpBetween(21, 50),
					When.FirstTime(Below50)],
				Do.ArmTimer(0, 11000),
				Do.ArmTimer(4, 36000),
				Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

			Branch(8, "and keeps taking the weakest", [When.Timer(3), When.HpBetween(51, 65)],
				Do.ArmTimer(3, 35000),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			Branch(6, "crossing sixty-five, the weakest again", [When.Timer(0), When.HpBetween(51, 65),
					When.FirstTime(Below65)],
				Do.ArmTimer(0, 10000),
				Do.ArmTimer(3, 35000),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			Branch(5, "and keeps taking the weakest", [When.Timer(2), When.HpBetween(66, 80)],
				Do.ArmTimer(2, 40000),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			Branch(3, "crossing eighty, the weakest", [When.Timer(0), When.HpBetween(66, 80),
					When.FirstTime(Below80)],
				Do.ArmTimer(0, 10000),
				Do.ArmTimer(2, 40000),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			// Above eighty, timer 1 is a cast loop and nothing else; its cadence is kept.
			Branch(2, "", [When.Timer(1), When.HpBetween(81, 100)],
				Do.ArmTimer(1, 20000)),

			Branch(1, "", [When.Timer(0)],
				Do.ArmTimer(0, 6000))),
	};

	public GrandChieftainSaendukalAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
