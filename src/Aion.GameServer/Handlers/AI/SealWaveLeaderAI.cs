using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Drakenspire Depths' five wave leaders — retail <c>IDSeal_Wave1..5_Leader_Lv3</c>, npcs 236239-236243.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>All five ran <see cref="WaveAttackerAI"/>, and they are five different bosses.</b> Not variants of
/// one pattern the way the wave's rank and file are — five distinct timer chains, at five different
/// cadences, three of which spawn something. Sharing one class between them was the reason none of it
/// happened.
/// </para>
/// <para>
/// <b>They are the voice the wave was waiting for.</b> Every one of them broadcasts <b>22750</b> the
/// moment it enters combat and again on its command rung — that is the buff message all nine wave
/// patterns listen for. Four of them also run a six-second health check that once, below seventy, sends
/// <b>22757</b>, the reserved-heal request that only the wave's priest leader answers. Neither message
/// was being sent by anything, which is why both rungs on the hearing side looked like dead branches.
/// </para>
/// <para>
/// <b>The alternation is the shape of every chain.</b> Timers 0, 1 and 2 hand off in a ring, and when
/// the ring closes on timer 2 two rungs compete for it: one guarded by <c>set_flag_var</c> and one by
/// <c>unset_flag_var</c>. Since the first is test-and-set and the second test-and-unset, they take turns
/// — a plain rung one time round and the command rung, with its broadcast and its add, the next. Leader
/// 4 does it differently and splits on health rather than on a flag; leader 5 adds a third rung below
/// fifty that pre-empts both.
/// </para>
/// <para>
/// <b>The health check stops itself.</b> Retail rearms timer 4 on a low-priority rung and the heal
/// request sits above it <em>without</em> a rearm, so the first time the leader drops under seventy the
/// request fires and the six-second heartbeat simply ends. That is not a mistake to tidy: a leader asks
/// to be healed once and then stops asking.
/// </para>
/// <para>
/// <b>The add.</b> <c>BIDSeal_Wave_Arrow_Target</c> (855923) lands on the leader's current target with
/// ten million hate and a fifteen-second life, and <c>despawn_at_attack_state</c> means it is furniture
/// that leaves the moment anything engages it. Leader 5's below-fifty rung places <b>three</b> of them
/// at once, on the three highest attackers, for ten seconds each.
/// </para>
/// <para>
/// <b>Not translated:</b> every <c>use_skill</c> — the buff itself (index 0, on all five), the strikes
/// that hang off each timer rung, and the healer-protect and mez answers to 22755 on leaders 1, 2 and 5.
/// So the chains keep their timing and their voice but not their blows. Also untranslated:
/// <c>set_condition_spawn_variable WAVE_LEADER modify=1</c> on both death handlers, which is the wave
/// progression counter and has no equivalent here; every <c>say_to_all</c>; and leader 5's
/// <c>points_to_add=150000</c> on its target switch, which this port's <see cref="Do.SwitchTarget"/>
/// cannot carry.
/// </para>
/// </remarks>
[AIName("seal_wave_leader")]
public class SealWaveLeaderAI : PatternAi
{
	/// <summary>The command buff. All nine wave patterns listen for it; nothing was sending it.</summary>
	public const int CommandBuff = 22750;

	/// <summary>The reserved-heal request. Only the wave's priest leader answers it.</summary>
	public const int HealRequest = 22757;

	/// <summary>Broadcast by the forward guards. The leaders take it every time.</summary>
	public const int GuardTaunt = 22760;

	public const int TauntHate = 10000;

	/// <summary><c>BIDSeal_Wave_Arrow_Target</c> — tribe <c>IDSEAL_WAVETARGET</c>, and furniture.</summary>
	public const int ArrowTarget = 855923;

	/// <summary><c>IDSeal_FOBJ_Mind_Control_Q</c>, "ominous darkness" — leader 5 leaves it where it dies.</summary>
	public const int OminousDarkness = 702769;

	/// <summary>Retail's <c>hatepoints_to_add</c> on the arrow target: it is meant to be unmissable.</summary>
	public const int ArrowHate = 10_000_000;

	private const int ArrowGroup = 1;
	private const float ArrowScatter = 5f;
	private const float ArrowReach = 50f;
	private const int ArrowLife = 15;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c> — the alternator, set by one rung and unset by the other.</summary>
	private const int Alternator = 1;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_3</c> — the heal request, once per fight.</summary>
	private const int HealAsked = 3;

	private const float Reach = 100f;

	/// <summary>Every leader answers the forward guards, and none of them rolls for it.</summary>
	private static readonly PatternBranch Taunt =
		Branch(1, "take the guard's shout personally", [When.Message(GuardTaunt)],
			Do.HateMessageSender(TauntHate));

	/// <summary>Retail's <c>spawn_on_target</c> row, identical on leaders 1, 4 and 5.</summary>
	private static PatternAction ArrowOnTarget() =>
		Do.SpawnOnTarget(ArrowTarget, ArrowGroup, count: 1, range: ArrowScatter,
			liveSeconds: ArrowLife, attackHate: ArrowHate, validDistance: ArrowReach);

	/// <summary>
	/// The six-second health check and the one request it makes. The request deliberately does not rearm
	/// the timer, so the heartbeat ends with it.
	/// </summary>
	private static readonly PatternBranch AskForAHeal =
		Branch(26, "ask to be healed, once", [When.Timer(4), When.HpBelow(70), When.FirstTime(HealAsked)],
			Do.BroadcastAboutSelf(HealRequest, Reach));

	private static readonly PatternBranch KeepCheckingHealth =
		Branch(19, "keep checking", [When.Timer(4)], Do.ArmTimer(4, 6000));

	// Leader 1: a seven-second ring, and the command rung carries the add.
	private static readonly AiPattern Wave1 = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(20, "arm the ring and buff the wave", When.Always,
				Do.ArmTimer(0, 7000),
				Do.ArmTimer(4, 6000),
				// The only leader whose enter-combat buff names its target rather than itself.
				Do.Broadcast(CommandBuff, Reach, aboutTarget: true))),

		OnBattleTimer = Of(
			// Retail arms timer 0 here as well as sending the request; leaders 2, 3 and 5 do not.
			Branch(26, "ask to be healed, once",
				[When.Timer(4), When.HpBelow(70), When.FirstTime(HealAsked)],
				Do.ArmTimer(0, 6000),
				Do.BroadcastAboutSelf(HealRequest, Reach)),

			Branch(25, "command: buff the wave and put an arrow on the tank",
				[When.Timer(2), When.Consuming(Alternator)],
				Do.ArmTimer(0, 15000),
				Do.BroadcastAboutSelf(CommandBuff, Reach),
				ArrowOnTarget()),

			Branch(23, "the other turn", [When.Timer(2), When.FirstTime(Alternator)],
				Do.ArmTimer(0, 15000)),

			Branch(22, "", [When.Timer(1)], Do.ArmTimer(2, 7000)),
			Branch(21, "", [When.Timer(0)], Do.ArmTimer(1, 7000)),
			KeepCheckingHealth),

		OnMessage = [Taunt],
	};

	// Leader 2: a ten-second ring with a fourth timer on the command side.
	private static readonly AiPattern Wave2 = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(10, "arm the ring and buff the wave", When.Always,
				Do.ArmTimer(0, 10000),
				Do.ArmTimer(4, 6000),
				Do.BroadcastAboutSelf(CommandBuff, Reach))),

		OnBattleTimer = Of(
			AskForAHeal,
			KeepCheckingHealth,

			Branch(14, "command: buff the wave", [When.Timer(3)],
				Do.ArmTimer(0, 8000),
				Do.BroadcastAboutSelf(CommandBuff, Reach)),

			// Retail gives these two the SAME priority and orders them by their place in the file, the
			// set-flag rung first. Kept in that order, with distinct priorities, which is the same ladder.
			Branch(13, "one turn", [When.Timer(2), When.FirstTime(Alternator)],
				Do.ArmTimer(0, 10000)),

			Branch(12, "the other, which hands off to the command timer",
				[When.Timer(2), When.Consuming(Alternator)],
				Do.ArmTimer(3, 12000)),

			Branch(11, "", [When.Timer(1)], Do.ArmTimer(2, 10000)),
			Branch(10, "", [When.Timer(0)], Do.ArmTimer(1, 10000))),

		OnMessage = [Taunt],
	};

	// Leader 3: the plainest of the five. A ten-second ring, no add at all.
	private static readonly AiPattern Wave3 = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(10, "arm the ring and buff the wave", When.Always,
				Do.ArmTimer(0, 7000),
				Do.ArmTimer(4, 6000),
				Do.BroadcastAboutSelf(CommandBuff, Reach))),

		OnBattleTimer = Of(
			AskForAHeal,
			KeepCheckingHealth,

			Branch(15, "command: buff the wave", [When.Timer(2), When.Consuming(Alternator)],
				Do.ArmTimer(0, 15000),
				Do.BroadcastAboutSelf(CommandBuff, Reach)),

			Branch(13, "the other turn", [When.Timer(2), When.FirstTime(Alternator)],
				Do.ArmTimer(0, 10000)),

			Branch(12, "", [When.Timer(1)], Do.ArmTimer(2, 10000)),
			Branch(11, "", [When.Timer(0)], Do.ArmTimer(1, 10000))),

		OnMessage = [Taunt],
	};

	// Leader 4: no ring and no health check. One thirteen-second beat, and a thirty-second command timer
	// that splits on health rather than on a flag -- both halves spawn, so the split is about the skills
	// this port cannot cast and the add comes either way.
	private static readonly AiPattern Wave4 = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(10, "arm both beats and buff the wave", When.Always,
				Do.ArmTimer(0, 6000),
				Do.ArmTimer(3, 30000),
				Do.BroadcastAboutSelf(CommandBuff, Reach))),

		OnBattleTimer = Of(
			Branch(13, "command, wounded", [When.Timer(3), When.HpBelow(50)],
				Do.ArmTimer(3, 30000),
				Do.BroadcastAboutSelf(CommandBuff, Reach),
				ArrowOnTarget()),

			// is_hp_in_boundary is exclusive at both ends, so larger_than=50 less_than=100 is 51 to 99.
			Branch(12, "command, healthy", [When.Timer(3), When.HpBetween(51, 99)],
				Do.ArmTimer(3, 30000),
				Do.BroadcastAboutSelf(CommandBuff, Reach),
				ArrowOnTarget()),

			Branch(11, "", [When.Timer(0)], Do.ArmTimer(0, 13000))),

		OnMessage = [Taunt],
	};

	// Leader 5: leader 1's ring with a third rung below fifty that pre-empts both turns, throws three
	// arrows instead of one, and picks a new player to hit.
	private static readonly AiPattern Wave5 = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(10, "arm the ring and buff the wave", When.Always,
				Do.ArmTimer(0, 7000),
				Do.ArmTimer(4, 6000),
				Do.BroadcastAboutSelf(CommandBuff, Reach))),

		OnBattleTimer = Of(
			Branch(27, "ask to be healed, once",
				[When.Timer(4), When.HpBelow(70), When.FirstTime(HealAsked)],
				Do.BroadcastAboutSelf(HealRequest, Reach)),

			Branch(26, "wounded: three arrows and a new player to hit",
				[When.Timer(2), When.HpBelow(50)],
				Do.ArmTimer(0, 23000),
				Do.BroadcastAboutSelf(CommandBuff, Reach),
				Do.SpawnOnEachTarget(ArrowTarget, 0, validDistance: ArrowReach, maxTargets: 3,
					order: MultiTargetOrder.Descending, range: 3f, liveSeconds: 10, attackHate: ArrowHate),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(24, "command: buff the wave and put an arrow on the tank",
				[When.Timer(2), When.Consuming(Alternator)],
				Do.ArmTimer(0, 18000),
				Do.BroadcastAboutSelf(CommandBuff, Reach),
				ArrowOnTarget()),

			Branch(23, "the other turn", [When.Timer(2), When.FirstTime(Alternator)],
				Do.ArmTimer(0, 15000)),

			Branch(22, "", [When.Timer(1)], Do.ArmTimer(2, 7000)),
			Branch(21, "", [When.Timer(0)], Do.ArmTimer(1, 7000)),
			KeepCheckingHealth),

		// Retail runs the same rung on on_killed_by_user and on_killed_by_npc; this port has one death.
		OnDie = Of(
			Branch(99, "leave the darkness where it fell", When.Always,
				Do.SpawnNear(OminousDarkness, 0))),

		OnMessage = [Taunt],
	};

	public SealWaveLeaderAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => GetOwner().GetNpcId() switch
	{
		236239 => Wave1,
		236240 => Wave2,
		236241 => Wave3,
		236242 => Wave4,
		_ => Wave5,
	};
}
