using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="ResearcherTeselikAI"/> and <see cref="ShebanMysticalTyrhundAI"/>,
/// translated from retail patterns <c>IDVritra_Base_Drakan_Wi_Nmd</c> and its <c>_Sum</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The fight is a counter: the boss asks "are my hands all dead?" at every branch point and either
/// summons a wave or detonates the one he has. Most of these pins are about that counter staying
/// truthful — including the retail quirk where phase two eats its own one-shot flag and never fires.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ResearcherTeselikAiTests
{
	private const int SauroSupplyBase = 301130000;
	private const int Teselik = 230850;
	private const int Hand = 284455;
	private const int BurnZone = 284687;

	private const int BlessingOfBlood = 20701;
	private const int FlameBolt = 17335;
	private const int FireBurst = 21288;
	private const int SummoningRitual = 20657;
	private const int SelfDestructCommand = 20708;

	private const int LiveHands = 0;

	/// <summary>The first branch point of the healthy chain: 6s + 8 + 8 + 8, with a second of slack.</summary>
	private const int FirstBranchPoint = 31;

	private static (BossAiHarness, Npc, Player) Engaged(int hpPercent = 100)
	{
		BossAiHarness harness = BossAiHarness.For(SauroSupplyBase).WithWorldSize(2048)
			.WithAi(typeof(ResearcherTeselikAI), typeof(ShebanMysticalTyrhundAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(Teselik, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(boss, hpPercent);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static List<int> CastsOver(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		var cast = new List<int>();
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			cast.AddRange(BossAiHarness.DrainQueuedSkills(boss).Select(c => c.SkillId));
		}
		return cast;
	}

	private static List<BossAiHarness.QueuedCast> CastsWithTargets(BossAiHarness harness, Npc boss,
		Player player, int seconds)
	{
		var cast = new List<BossAiHarness.QueuedCast>();
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			cast.AddRange(BossAiHarness.DrainQueuedSkills(boss));
		}
		return cast;
	}

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	private static int Count(Npc boss) => ((ResearcherTeselikAI)boss.GetAi()).Counter(LiveHands);

	[Fact]
	public void EnteringCombatSummonsTwoHandsAndSetsTheCount()
	{
		var (harness, boss, _) = Engaged();
		using BossAiHarness _h = harness;

		Assert.Equal(2, Live(harness, Hand).Count);
		Assert.Equal(2, Count(boss));
	}

	[Fact]
	public void TheHealthyChainRunsBoltBoltBurstThenBranches()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(boss);

		List<int> cast = CastsOver(harness, boss, player, FirstBranchPoint);

		// Timer 1 at 6s, timer 2 at 14s, timer 3 at 22s, timer 4 at 30s.
		Assert.Equal([FlameBolt, FlameBolt, FireBurst, SelfDestructCommand],
			cast.Where(c => c != SummoningRitual).ToList());
	}

	[Fact]
	public void HandsStandingGetTheOrderInsteadOfAFreshWave()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		List<int> cast = CastsOver(harness, boss, player, FirstBranchPoint);

		Assert.Contains(SelfDestructCommand, cast);
		// One ritual only — the one he cast on engaging. The branch point did not add another.
		Assert.Equal(1, cast.Count(c => c == SummoningRitual));
	}

	[Fact]
	public void TheOrderClearsTheHandsAndLeavesABurnZoneWhereEachStood()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		CastsOver(harness, boss, player, FirstBranchPoint);

		Assert.Empty(Live(harness, Hand));
		Assert.Equal(2, Live(harness, BurnZone).Count);
		Assert.Equal(0, Count(boss));
	}

	[Fact]
	public void ADyingHandTakesOneOffTheCount()
	{
		var (harness, boss, _) = Engaged();
		using BossAiHarness _h = harness;

		Live(harness, Hand)[0].GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(1, Count(boss));
	}

	[Fact]
	public void WithEveryHandDeadTheBranchPointSummonsAgainInsteadOfOrdering()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		foreach (Npc hand in Live(harness, Hand))
			hand.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(0, Count(boss));
		BossAiHarness.DrainQueuedSkills(boss);

		List<int> cast = CastsOver(harness, boss, player, FirstBranchPoint);

		Assert.Contains(SummoningRitual, cast);
		Assert.DoesNotContain(SelfDestructCommand, cast);
		Assert.InRange(Count(boss), 2, 3);
	}

	/// <summary>
	/// One hand left is still "some alive", and it is the state players actually produce by focusing
	/// the adds down one at a time. It is also the only count that tells "below one" apart from "at
	/// most one", so the boundary is worth its own pin.
	/// </summary>
	[Fact]
	public void OneHandLeftStandingIsStillEnoughToGetTheOrder()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		Live(harness, Hand)[0].GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(1, Count(boss));
		BossAiHarness.DrainQueuedSkills(boss);

		List<int> cast = CastsOver(harness, boss, player, FirstBranchPoint);

		Assert.Contains(SelfDestructCommand, cast);
		Assert.DoesNotContain(SummoningRitual, cast);
	}

	[Fact]
	public void ALateDeathReportCannotDriveTheCountNegative()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		List<Npc> hands = Live(harness, Hand);
		foreach (Npc hand in hands)
			hand.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(0, Count(boss));
		BossAiHarness.DrainQueuedSkills(boss);

		// A hand can report its death after the boss has already written the count back to zero — the
		// order he gives at a branch point sets it to zero outright, whatever is still falling over.
		// Without the clamp those late reports would take the count negative and "are they all gone"
		// would never pass again, so he would stop summoning for the rest of the fight.
		var listener = (Aion.GameServer.Ai.INpcMessageListener)boss.GetAi();
		listener.OnNpcMessage(hands[0], ResearcherTeselikAI.HandDied, null);
		listener.OnNpcMessage(hands[0], ResearcherTeselikAI.HandDied, null);
		Assert.Equal(0, Count(boss));

		Assert.Contains(SummoningRitual, CastsOver(harness, boss, player, FirstBranchPoint));
	}

	[Fact]
	public void PhaseTwoBlessesHimAndDetonatesTheHands()
	{
		var (harness, boss, player) = Engaged(hpPercent: 60);
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(boss);

		List<int> cast = CastsOver(harness, boss, player, 8);

		Assert.Contains(BlessingOfBlood, cast);
		Assert.Contains(SelfDestructCommand, cast);
	}

	[Fact]
	public void PhaseTwoFiresOnlyOnce()
	{
		var (harness, boss, player) = Engaged(hpPercent: 60);
		using BossAiHarness _h = harness;

		List<int> cast = CastsOver(harness, boss, player, 90);

		Assert.Equal(1, cast.Count(c => c == BlessingOfBlood));
	}

	/// <summary>
	/// The retail quirk, pinned so nobody quietly "fixes" it. The one-shot flag is tested before the
	/// count, so when the hands are already gone the branch that wants them alive spends the flag and
	/// then fails — and the summoning variant beneath it can never match again.
	/// </summary>
	[Fact]
	public void PhaseTwoIsSkippedEntirelyWhenNoHandIsStanding()
	{
		var (harness, boss, player) = Engaged(hpPercent: 60);
		using BossAiHarness _h = harness;
		foreach (Npc hand in Live(harness, Hand))
			hand.GetAi().OnGeneralEvent(AiEventType.Died);
		BossAiHarness.DrainQueuedSkills(boss);

		List<int> cast = CastsOver(harness, boss, player, 90);

		Assert.DoesNotContain(BlessingOfBlood, cast);
	}

	/// <summary>
	/// Retail casts index 3 on itself in the healthy chain and at the current target in the low one.
	/// Same skill, two regimes, two targets — and nothing else in these pins would notice the swap.
	/// </summary>
	[Fact]
	public void HeBreathesFireAtHimselfWhileHealthyAndAtTheTargetBelowSixtyFive()
	{
		var (healthy, healthyBoss, healthyPlayer) = Engaged();
		using (healthy)
		{
			List<BossAiHarness.QueuedCast> cast = CastsWithTargets(healthy, healthyBoss, healthyPlayer, 24);
			Assert.Equal(NpcSkillTargetAttribute.ME, cast.Single(c => c.SkillId == FireBurst).Target);
		}

		var (low, lowBoss, lowPlayer) = Engaged(hpPercent: 60);
		using (low)
		{
			List<BossAiHarness.QueuedCast> cast = CastsWithTargets(low, lowBoss, lowPlayer, 24);
			Assert.Equal(NpcSkillTargetAttribute.MOST_HATED, cast.Single(c => c.SkillId == FireBurst).Target);
		}
	}

	[Fact]
	public void LeavingTheFightClearsTheHandsAndTheCount()
	{
		var (harness, boss, _) = Engaged();
		using BossAiHarness _h = harness;

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Empty(Live(harness, Hand));
		Assert.Equal(0, Count(boss));
	}

	[Fact]
	public void AHandKnocksItsTargetAboutOnItsOwnTimer()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;
		Npc hand = Live(harness, Hand)[0];
		harness.Engage(hand, player);
		BossAiHarness.DrainQueuedSkills(hand);

		var cast = new List<int>();
		for (int i = 0; i < 40; i++)
		{
			BossAiHarness.Rehate(hand, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			cast.AddRange(BossAiHarness.DrainQueuedSkills(hand).Select(c => c.SkillId));
		}

		// 16791 is the only skill our data gives it, and only the coin-flip branch casts it, so over
		// forty seconds of ticking it should land at least once and never anything else.
		Assert.NotEmpty(cast);
		Assert.All(cast, c => Assert.Equal(16791, c));
	}
}
