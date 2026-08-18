using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Chief Gunner Kurmata (230851) of the Sauro Supply Base. Retail pattern
/// <c>IDVritra_Base_Drakan_Gi_Nmd</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A HERO on plain <c>aggressive</c>, and the whole
/// fight is a targeting mechanic in three parts: <b>he paints a player, the paint calls, and the
/// cannon fires at the paint.</b>
/// <list type="table">
/// <item><term>on engaging</term><description>a mark on a <b>random</b> attacker, and a call that puts
/// the flame cannon on whoever he is fighting</description></item>
/// <item><term>above sixty</term><description>a four-step loop of about thirty-nine seconds; one step
/// marks <b>whoever he is fighting</b> and another turns him onto somebody else</description></item>
/// <item><term>below sixty, once</term><description>the loop is replaced by a shorter one that marks
/// <b>two players at a time</b>, twice round, with ten times the hate on each mark</description></item>
/// </list>
/// <para>
/// <b>The marks do the work, and they do it by hating.</b> Retail spawns each with
/// <c>attack_target_after_spawn</c> and a hundred thousand hate points — a million below sixty — so a
/// mark is not scenery: it lands on a player and stays on them. That is why the fight reads as a
/// gunnery drill rather than a boss with adds.
/// </para>
/// <para>
/// <b>Below sixty he marks two, not everyone.</b> Retail's <c>spawn_on_multi_target</c> carries
/// <c>total_set_to_spawn=2</c> and <c>ORDERI_RANDOM</c> over a forty-metre reach, which is easy to read
/// as "one on each player" — the element's name says multi and the count says two.
/// </para>
/// <para>
/// <b>Not translated.</b> Eleven skill indices — every "탄환발사" and "산탄" in the branch comments is
/// one — and five shouts. The <c>on_enter_idle_state</c> race buff, whose despawn half is kept.
/// </para>
/// </remarks>
[AIName("chief_gunner_kurmata")]
public class ChiefGunnerKurmataAI : PatternAi
{
	/// <summary><c>IDVritra_Base_Drakan_Gi_Nmd_Beacon</c> — a mark, which is an npc.</summary>
	private const int Mark = 284454;

	/// <summary>Retail's <c>SPAWN_ID_1</c>, cleared on his death and on losing the fight.</summary>
	private const int Marks = 1;

	/// <summary>Retail's <c>spawn_range</c>, <c>live_time</c> and <c>valid_distance</c> on every mark.</summary>
	private const float UnderFoot = 1f;
	private const int Life = 20;
	private const float Reach = 40f;

	/// <summary>Retail's <c>hatepoints_to_add</c>: a hundred thousand, and ten times that below sixty.</summary>
	private const int Stuck = 100000;
	private const int Welded = 1000000;

	/// <summary>Retail's <c>total_set_to_spawn</c> on the two-player marks.</summary>
	private const int Pair = 2;

	/// <summary>Retail's <c>range_as_meter</c> on the call to the cannon.</summary>
	private const float Earshot = 50f;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_4</c>: the second loop opens once.</summary>
	private const int Opened = 4;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(2, "", When.Always,
				Do.ArmTimer(0, 5000),
				Do.ArmTimer(1, 14000),
				Do.Broadcast(SupplyBaseFlameCannonAI.OpenFire, Earshot, aboutTarget: true),
				Do.SpawnOnAttacker(AggroTarget.RANDOM, Mark, Marks,
					range: UnderFoot, liveSeconds: Life, attackHate: Stuck))),

		OnBattleTimer = Of(
			Branch(15, "and turns onto somebody else", [When.Timer(7)],
				Do.ArmTimer(5, 14000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(14, "two more marks", [When.Timer(6)],
				Do.ArmTimer(7, 11000),
				Do.SpawnOnEachTarget(Mark, Marks, validDistance: Reach, maxTargets: Pair,
					order: MultiTargetOrder.Random, range: UnderFoot, liveSeconds: Life,
					attackHate: Welded)),

			Branch(12, "", [When.Timer(5)],
				Do.ArmTimer(6, 8000)),

			Branch(11, "below sixty he marks two at a time", [When.Timer(0), When.HpBelow(60),
					When.FirstTime(Opened)],
				Do.ArmTimer(5, 10000),
				Do.SpawnOnEachTarget(Mark, Marks, validDistance: Reach, maxTargets: Pair,
					order: MultiTargetOrder.Random, range: UnderFoot, liveSeconds: Life,
					attackHate: Welded)),

			Branch(10, "", [When.Timer(4), When.HpBetween(61, 100)],
				Do.ArmTimer(1, 14000)),

			Branch(8, "and turns onto somebody else", [When.Timer(3), When.HpBetween(61, 100)],
				Do.ArmTimer(4, 8000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(6, "a mark on his quarry", [When.Timer(2), When.HpBetween(61, 100)],
				Do.ArmTimer(3, 9000),
				Do.SpawnOnTarget(Mark, Marks, count: 1, range: UnderFoot, liveSeconds: Life,
					attackHate: Stuck)),

			Branch(4, "", [When.Timer(1), When.HpBetween(61, 100)],
				Do.ArmTimer(2, 8000)),

			// The clock that carries him to the sixty-percent crossing, and nothing else.
			Branch(3, "", [When.Timer(0)],
				Do.ArmTimer(0, 5000))),

		OnDie = Of(
			Branch(18, "", When.Always,
				Do.Despawn(Marks))),

		OnEnterIdle = Of(
			Branch(1, "", When.Always,
				Do.Despawn(Marks))),
	};

	public ChiefGunnerKurmataAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The Sauro Supply Base flame cannon (284453). Retail pattern
/// <c>IDVritra_Base_Drakan_Gi_Nmd_Tank</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. It answers two calls and they mean different
/// things:
/// <list type="table">
/// <item><term><c>22274</c>, from the gunner</term><description>the fight has started — take whoever
/// he is fighting</description></item>
/// <item><term><c>22273</c>, from a mark</term><description>a mark has landed — <b>put hate on the
/// mark itself</b> and fire</description></item>
/// </list>
/// <para>
/// <b>The second is the mechanic, and it is written with a different object.</b> Retail hates
/// <c>OBJI_MESSAGE_SENDER</c> rather than <c>OBJI_MESSAGE_PARAM</c>: the cannon turns on <em>the thing
/// that spoke</em>, which is the mark standing on a player. A laser designator built out of two NPCs
/// and a message, because a pattern has no way to say "fire where I am pointing". That called for a new
/// action, <see cref="Do.HateMessageSender"/> — the mark names itself as its own parameter too, so the
/// outcome would have matched either way, and the mechanism would not have.
/// </para>
/// <para>
/// <b>Not translated:</b> the cast on the second branch, which is the shot itself.
/// </para>
/// </remarks>
[AIName("supply_base_flame_cannon")]
public class SupplyBaseFlameCannonAI : PatternAi
{
	/// <summary>Retail's <c>22274</c>: the gunner opening the fight.</summary>
	public const int OpenFire = 22274;

	/// <summary>Retail's <c>22273</c>: a mark announcing itself.</summary>
	public const int MarkLanded = 22273;

	/// <summary>Retail's <c>point_to_add</c> on both branches, and it settles the argument.</summary>
	private const int Decisive = 10000;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(2, "", [When.Message(OpenFire)],
				Do.HateMessageTarget(Decisive)),

			Branch(1, "fire at the mark", [When.Message(MarkLanded)],
				Do.HateMessageSender(Decisive))),
	};

	public SupplyBaseFlameCannonAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The mark Chief Gunner Kurmata plants on a player (284454). Retail pattern
/// <c>IDVritra_Base_Drakan_Gi_Nmd_Beacon</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One branch of its pattern is built: on waking it
/// announces itself within fifty metres, which is what brings the cannon round. Everything the mark
/// does after that it does to the player it is standing on, through the hate the gunner gave it.
/// <para>
/// <b>Not translated, and worth its own pass.</b> Retail gives it an <c>on_spelled</c> branch guarded
/// on <c>is_event_skill_id</c> that leaves a puff of smoke and removes the mark — a player answer to
/// the mechanic, and one that needs a skill id we cannot resolve. It also runs a counter on a battle
/// timer that ends the same way. Until those land, our marks are cleared by their twenty-second
/// lifetime and by the gunner's own despawns, and a raid cannot shoot one off.
/// </para>
/// </remarks>
[AIName("supply_base_mark")]
public class SupplyBaseMarkAI : PatternAi
{
	/// <summary>Retail's <c>range_as_meter</c> — the cannon is well inside it.</summary>
	private const float Earshot = 50f;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnWakeUp = Of(
			Branch(2, "", When.Always,
				Do.Broadcast(SupplyBaseFlameCannonAI.MarkLanded, Earshot))),
	};

	public SupplyBaseMarkAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
