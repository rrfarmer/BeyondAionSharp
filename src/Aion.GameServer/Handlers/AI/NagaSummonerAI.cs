using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Heiron's two naga field bosses — High Mage Brashuna (212310) and Commander Gitimuka (212307).
/// Retail patterns <c>Naga_WrF2</c> and <c>Naga_WrF3</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Both were HEROes on plain <c>aggressive</c>. They
/// are the same fight written twice — six health bands, each opened once by the ladder timer and then
/// held by a relay of its own — so they share one builder:
/// <list type="table">
/// <item><term>91–100</term><description>nothing but a slower tick: the ladder re-arms at ten seconds
/// instead of six</description></item>
/// <item><term>76–90</term><description>he comes off the tank and goes for <b>whoever is closest to
/// dying</b>, and a relay does it again every fifteen or twenty seconds</description></item>
/// <item><term>61–75</term><description>the same, on its own relay</description></item>
/// <item><term>41–60</term><description><b>three faithful subordinates land on the player he is
/// fighting</b>, an order sends them after that player, and a relay adds one more every thirty seconds
/// and re-issues the order</description></item>
/// <item><term>21–40</term><description>he <b>dismisses the subordinates</b> — one broadcast and every
/// one of them deletes itself two seconds later — takes a random attacker, and starts a
/// forty-five-second peel that runs for the rest of the fight</description></item>
/// <item><term>below 20</term><description>the ladder stops and he goes for the <b>third</b>-most-hated
/// instead, again and again</description></item>
/// </list>
/// <para>
/// <b>The wave lands on the player rather than on the boss.</b> Retail's <c>spawn_on_target</c> puts the
/// three within seven metres of whoever he is fighting, and the relay's extra one within three. That is
/// what makes them dangerous — they arrive already on top of somebody — and it is also why the order
/// that follows them is easy to mistake for working when it is not: they are <c>aggressive</c> and
/// would have found that player unaided. The pins use a decoy to separate the two.
/// </para>
/// <para>
/// <b>Third encounter to want the opposite of the same-branch broadcast rule</b> measured for RM-56c
/// (after the anuhart casters' pet and Anuhart's own subordinates): the three spawned by the 41–60 step
/// do not hear the order issued in that same branch, and wait for the relay thirty seconds later. Here
/// it costs almost nothing, because they land on a player and aggro on their own — unlike Anuhart's,
/// which land on marks away from the raid and really do stand idle. Our measured behaviour is kept.
/// </para>
/// <para>
/// <b>The two bosses differ in three delays and one npc id</b>, and in nothing else: Brashuna's peels
/// come at fifteen and thirty seconds and her rage relay at forty, Gitimuka's at twenty, twenty and
/// twenty-five. Retail also swaps one cast's attacker indicator between them, which is blocked anyway.
/// </para>
/// <para>
/// <b>Not translated.</b> Thirty-three skill indices and five shouts. Retail's timer 1 — armed by the
/// 91–100 rung and re-armed by its own relay every twenty seconds — carries a single cast and nothing
/// else, so both the arm and the relay are dropped: no other branch uses that slot, so nothing else
/// depends on it. The 91–100 rung itself is kept, because its ladder re-arm is four seconds longer than
/// the fallback's and that is a real difference in cadence.
/// </para>
/// </remarks>
[AIName("naga_summoner")]
public class NagaSummonerAI : PatternAi
{
	/// <summary>Retail's <c>SPAWN_ID_1</c>, cleared on both of his exits.</summary>
	private const int Group = 1;

	/// <summary>Retail's <c>live_time</c> — twenty minutes, so nothing expires inside a fight.</summary>
	private const int Life = 1200;

	/// <summary>Retail's <c>spawn_range</c>: seven for the wave of three, three for the relay's one.</summary>
	private const float WaveRing = 7f;
	private const float SingleRing = 3f;

	/// <summary>Retail's <c>range_as_meter</c> on both broadcasts.</summary>
	private const float Reach = 50f;

	// Retail's battle timer indices. Index 1 is retail's cast loop and is not built.
	private const int Ladder = 0;
	private const int Peel76 = 2;
	private const int Peel61 = 3;
	private const int Wave41 = 4;
	private const int Peel21 = 5;
	private const int Rage = 6;

	// Retail's ALPHA_1..ALPHA_5 and BETA_1.
	private const int Full = 1;
	private const int Below90 = 2;
	private const int Below75 = 3;
	private const int Below60 = 4;
	private const int Below40 = 5;
	private const int Below20 = 6;

	/// <summary>
	/// Which boss calls which subordinate, and the three delays retail gives each of them: the 76–90
	/// peel, the 61–75 peel, and the relay below twenty.
	/// </summary>
	private static readonly Dictionary<int, (int Summon, int Peel76, int Peel61, int Rage)> Bosses = new()
	{
		[212310] = (280797, 15000, 30000, 40000),   // High Mage Brashuna
		[212307] = (280799, 20000, 20000, 25000),   // Commander Gitimuka
	};

	private static readonly Dictionary<int, AiPattern> Patterns =
		Bosses.ToDictionary(e => e.Key, e => Build(e.Value.Summon, e.Value.Peel76, e.Value.Peel61, e.Value.Rage));

	private static AiPattern Build(int summon, int peel76, int peel61, int rage) => new AiPattern
	{
		OnEnterAttack = Of(
			Branch(13, "", When.Always,
				Do.ArmTimer(Ladder, 10000))),

		OnBattleTimer = Of(
			Branch(13, "and keeps taking the third", [When.Timer(Rage), When.HpBelow(20)],
				Do.ArmTimer(Rage, rage),
				Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

			// Does not re-arm the ladder: below twenty there are no bands left, only this relay.
			Branch(12, "below 20 goes for the third-most-hated", [When.Timer(Ladder), When.HpBelow(20),
					When.FirstTime(Below20)],
				Do.ArmTimer(Rage, 25000),
				Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

			Branch(11, "and peels for the rest of the fight", [When.Timer(Peel21), When.HpBetween(21, 100)],
				Do.ArmTimer(Peel21, 45000),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			Branch(10, "below 40 dismisses them", [When.Timer(Ladder), When.HpBetween(21, 40),
					When.FirstTime(Below40)],
				Do.ArmTimer(Ladder, 15000),
				Do.ArmTimer(Peel21, 45000),
				Do.Broadcast(NagaSubordinateAI.Disperse, Reach, aboutTarget: true),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(9, "and calls one more", [When.Timer(Wave41), When.HpBetween(41, 60)],
				Do.ArmTimer(Wave41, 30000),
				Do.SpawnOnTarget(summon, Group, count: 1, range: SingleRing, liveSeconds: Life),
				Do.Broadcast(NagaSubordinateAI.GoForThisOne, Reach, aboutTarget: true),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			Branch(8, "41-60 drops three on his quarry", [When.Timer(Ladder), When.HpBetween(41, 60),
					When.FirstTime(Below60)],
				Do.ArmTimer(Ladder, 15000),
				Do.ArmTimer(Wave41, 30000),
				Do.SpawnOnTarget(summon, Group, count: 3, range: WaveRing, liveSeconds: Life),
				Do.Broadcast(NagaSubordinateAI.GoForThisOne, Reach, aboutTarget: true),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			Branch(7, "", [When.Timer(Peel61), When.HpBetween(61, 75)],
				Do.ArmTimer(Peel61, peel61),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			Branch(6, "61-75 keeps peeling", [When.Timer(Ladder), When.HpBetween(61, 75),
					When.FirstTime(Below75)],
				Do.ArmTimer(Ladder, 10000),
				Do.ArmTimer(Peel61, 30000),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			Branch(5, "", [When.Timer(Peel76), When.HpBetween(76, 90)],
				Do.ArmTimer(Peel76, peel76),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			Branch(4, "76-90 comes off the tank", [When.Timer(Ladder), When.HpBetween(76, 90),
					When.FirstTime(Below90)],
				Do.ArmTimer(Ladder, 10000),
				Do.ArmTimer(Peel76, 30000),
				Do.SwitchTarget(AggroTarget.LOWEST_HP)),

			// All this rung has left is its own slower tick, and that is reason enough to keep it.
			Branch(2, "", [When.Timer(Ladder), When.HpBetween(91, 100), When.FirstTime(Full)],
				Do.ArmTimer(Ladder, 10000)),

			Branch(1, "", [When.Timer(Ladder)],
				Do.ArmTimer(Ladder, 6000))),

		OnLeaveAttack = Of(
			Branch(15, "", When.Always,
				Do.Despawn(Group))),

		OnDie = Of(
			Branch(14, "", When.Always,
				Do.Despawn(Group))),
	};

	private readonly AiPattern pattern;

	public NagaSummonerAI(Npc owner)
		: base(owner)
	{
		pattern = Patterns[owner.GetNpcId()];
	}

	protected override AiPattern Pattern => pattern;
}

/// <summary>
/// The naga bosses' faithful subordinates (280797 and 280799). Retail pattern <c>Naga_Sum_WrF2</c>,
/// which both npcs share.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two messages and a two-second fuse between them:
/// <list type="table">
/// <item><term><c>6186</c></term><description>ten hate on whoever the boss named, and go</description></item>
/// <item><term><c>6185</c></term><description>arm a two-second timer, and when it fires
/// <b>delete yourself</b></description></item>
/// </list>
/// <para>
/// <b>The dismissal is the point, and it is easy to read as a cast.</b> Retail's <c>6185</c> branch is a
/// timer arm and a self-cast, which looks like the cast-only branches this log has dropped elsewhere —
/// but the timer it arms leads to <c>despawn_self</c>, so the branch is exactly how the boss clears his
/// own summons on the way past forty. The cast is the animation; the despawn is the mechanic.
/// </para>
/// <para>
/// <b>A subordinate that never joined the fight is never dismissed</b>, because retail hangs the fuse on
/// a battle timer and battle timers do not tick outside combat. Kept as written: our runtime has the
/// same rule for the same reason, and the case barely arises when the summons land on a player.
/// </para>
/// </remarks>
[AIName("naga_subordinate")]
public class NagaSubordinateAI : PatternAi
{
	/// <summary>Retail's two orders: go, and go away.</summary>
	public const int GoForThisOne = 6186;
	public const int Disperse = 6185;

	/// <summary>Retail's <c>point_to_add</c> on this one, which is not the bare default.</summary>
	private const int OrderHate = 10;

	/// <summary>Retail's <c>BTIMERI_INDEX_0</c> and its two-second <c>delay</c>.</summary>
	private const int Fuse = 0;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(2, "", [When.Message(Disperse)],
				Do.ArmTimer(Fuse, 2000)),

			Branch(1, "", [When.Message(GoForThisOne)],
				Do.HateMessageTarget(OrderHate))),

		OnBattleTimer = Of(
			Branch(3, "", [When.Timer(Fuse)],
				Do.DespawnSelf())),
	};

	public NagaSubordinateAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
