using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Darkblade Ovanuka (233256) of the Sauro Supply Base. Retail pattern
/// <c>IDVritra_Base_Drakan_As_IU_Nmd</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A HERO on plain <c>aggressive</c> whose fight is
/// three phases of a long timer chain. What survives translation is the turning and the one order he
/// gives:
/// <list type="table">
/// <item><term>above eighty</term><description>a four-step loop of thirty seconds, one step of which
/// takes a <b>random</b> attacker</description></item>
/// <item><term>crossing eighty</term><description><b>he calls his bladesmen onto whoever he is
/// fighting</b> and turns himself</description></item>
/// <item><term>below thirty-five</term><description>a shorter loop that turns twice more before it
/// runs out</description></item>
/// </list>
/// <para>
/// <b>Two phases of his fight hang off wandering, and we cannot wander.</b> Retail's phase two and the
/// second half of phase three are reached through <c>random_move</c> and the
/// <c>on_stop_to_random_move</c> event it raises: timers 5, 6, 7 and 10 are armed there and nowhere
/// else. Our runtime has neither, so those branches are dead, and <c>audit_timer_reach.py</c> now says
/// so — this pattern is what added <c>on_stop_to_random_move</c> to its unreachable set beside
/// <c>on_arrived_at_waypoint</c>.
/// </para>
/// <para>
/// <b>His second call goes with them.</b> <c>22271</c> — the soft one, which his bladesmen answer one
/// time in three with a turn rather than a charge — is broadcast only from timer 10, so nothing can
/// reach it. Neither half is built: not the call, and not his own thirty-percent answer to it. Recorded
/// rather than dropped, because both come back the day <c>random_move</c> does.
/// </para>
/// <para>
/// <b>Phase three ends early, and faithfully.</b> The branch on timer 9 is a two-way toggle on one
/// flag: the first turn arms timer 11 and takes a random attacker, and the second wanders. So our
/// version of the last phase turns twice and then the chain stops, exactly where retail's would have
/// walked away.
/// </para>
/// <para>
/// <b>Not translated.</b> Twenty-two skill indices, four shouts, three <c>random_move</c>s, and
/// <c>set_condition_spawn_variable ITEMNAMED_SUM</c> — the phase-three subordinate wave, which retail
/// hands to the instance rather than to the pattern and which belongs in
/// <c>SauroSupplyBaseInstance</c> if it is ever built.
/// </para>
/// </remarks>
[AIName("darkblade_ovanuka")]
public class DarkbladeOvanukaAI : PatternAi
{
	/// <summary>Retail's <c>range_as_meter</c> on the order.</summary>
	private const float Earshot = 30f;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> and <c>ALPHA_2</c>: the two phase crossings.</summary>
	private const int Below80 = 1;
	private const int Below35 = 2;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(2, "", When.Always,
				Do.ArmTimer(0, 5000),
				Do.ArmTimer(1, 6000))),

		OnBattleTimer = Of(
			Branch(20, "and turns again", [When.Timer(11)],
				Do.ArmTimer(8, 8500),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			// Retail toggles one flag here: the first turn goes this way, the second wanders off.
			Branch(18, "the last turn", [When.Timer(9), When.Consuming(Below80)],
				Do.ArmTimer(11, 10000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(15, "", [When.Timer(8)],
				Do.ArmTimer(9, 8000)),

			Branch(14, "below thirty-five", [When.Timer(0), When.HpBelow(35), When.FirstTime(Below35)],
				Do.ArmTimer(8, 10000)),

			Branch(8, "crossing eighty he calls them", [When.Timer(0), When.HpBelow(80),
					When.FirstTime(Below80)],
				Do.ArmTimer(0, 5000),
				Do.Broadcast(ShebanBladesmanAI.GoForThisOne, Earshot, aboutTarget: true),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(7, "", [When.Timer(4), When.HpBetween(81, 100)],
				Do.ArmTimer(1, 6000)),

			Branch(6, "", [When.Timer(3), When.HpBetween(81, 100)],
				Do.ArmTimer(4, 6000)),

			Branch(5, "and takes somebody at random", [When.Timer(2), When.HpBetween(81, 100)],
				Do.ArmTimer(3, 12000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(4, "", [When.Timer(1), When.HpBetween(81, 100)],
				Do.ArmTimer(2, 6000)),

			Branch(3, "", [When.Timer(0)],
				Do.ArmTimer(0, 5000))),
	};

	public DarkbladeOvanukaAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The sheban bladesmen (233286) that answer Ovanuka. Retail pattern
/// <c>IDVritra_Base_Drakan_As_IU_Sum2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One branch is built: <b>when Ovanuka crosses
/// eighty and names a player, they take that player and go.</b>
/// <para>
/// <b>And the base alarm.</b> <c>22251</c> goes out from Brigade General Sheba (230858) and Guard
/// Captain Ahuradim (230857) as they engage, and the bladesmen answer it exactly as they answer
/// Ovanuka — three thousand hate on the player the boss named. See <see cref="Ai.CombatAlarm"/> for how
/// two Java-parity boss classes came to send it.
/// </para>
/// <para>
/// <b>Not built:</b> <c>22271</c>, Ovanuka's soft call, which they take one time in three and which
/// nothing can reach — see <see cref="DarkbladeOvanukaAI"/>.
/// </para>
/// <para>
/// <b>Not translated:</b> the casts on all three branches, the self-buffs on waking, and the
/// <c>goto_waypoint</c> that walks them to their post.
/// </para>
/// </remarks>
[AIName("sheban_bladesman")]
public class ShebanBladesmanAI : PatternAi
{
	/// <summary>Retail's <c>22270</c>: Ovanuka naming a player at the eighty-percent crossing.</summary>
	public const int GoForThisOne = 22270;

	/// <summary>Retail's <c>22251</c>: a Sauro Supply Base boss being pulled.</summary>
	public const int BaseAlarm = 22251;

	/// <summary>Retail's <c>point_to_add</c> on both of the bladesman's orders.</summary>
	private const int Ordered = 3000;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(11, "the base alarm", [When.Message(BaseAlarm)],
				Do.HateMessageTarget(Ordered)),

			Branch(2, "", [When.Message(GoForThisOne)],
				Do.HateMessageTarget(Ordered))),
	};

	public ShebanBladesmanAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The sheban legion ambushers (233277). Retail pattern <c>IDVritra_Base_Drakan_As_Hide</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One branch: they answer the base alarm and take
/// whoever the boss named. They put <b>a thousand</b> hate on that player where the bladesmen put three
/// — the same order, weighted differently, which is the only thing separating the two guard kinds in
/// retail's data.
/// <para>
/// <b>Not translated:</b> six skill indices, and the <c>goto_waypoint</c> they walk on waking and on
/// leaving a fight, which is how retail returns them to their post.
/// </para>
/// </remarks>
[AIName("sheban_ambusher")]
public class ShebanAmbusherAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c>, a third of what a bladesman brings.</summary>
	private const int Ordered = 1000;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(2, "", [When.Message(ShebanBladesmanAI.BaseAlarm)],
				Do.HateMessageTarget(Ordered))),
	};

	public ShebanAmbusherAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
