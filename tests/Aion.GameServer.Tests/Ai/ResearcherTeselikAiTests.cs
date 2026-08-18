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

	/// <summary>
	/// <b>A hand that self-destructs still reports in.</b> Retail ends that branch with a suicide
	/// skill, which kills the hand and runs its <c>on_die</c> — so the boss hears <c>HandDied</c>
	/// whether the players killed it or it blew itself up.
	/// </summary>
	/// <remarks>
	/// <b>It did not, and no audit of missing pieces could have found that.</b> Our npc_skills does not
	/// carry the suicide skill, so the branch was written as a despawn — and a despawn is not a death,
	/// so the notice was silently lost on that path. Only a hand killed by players ever reported in.
	/// Found by asking the reverse question: what do we do that retail does not.
	/// <para>
	/// Observed through a throwaway listener rather than the boss, whose own answer is a counter
	/// decrement that clamps at zero and shows nothing until its hands have been counted up first.
	/// </para>
	/// </remarks>
	[Fact]
	public void AHandThatSelfDestructsStillReportsIn()
	{
		BossAiHarness harness = BossAiHarness.For(SauroSupplyBase).WithWorldSize(2048)
			.WithAi(typeof(ResearcherTeselikAI), typeof(ShebanMysticalTyrhundAI),
				typeof(HandDeathListenerProbeAI), typeof(AggressiveNpcAI))
			.Build();
		using BossAiHarness _h = harness;

		Npc boss = harness.Spawn(Teselik, 300f, 300f, 200f);
		Npc hand = harness.SpawnWithAi(Hand, "sheban_mystical_tyrhund", 303f, 300f, 200f);
		Npc listener = harness.SpawnWithAi(Hand, "hand_death_probe", 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(hand, listener);

		Assert.Contains(listener, harness.LiveNpcs());

		((Aion.GameServer.Ai.INpcMessageListener)hand.GetAi())
			.OnNpcMessage(boss, ResearcherTeselikAI.SelfDestructOrder, null);

		Assert.DoesNotContain(listener, harness.LiveNpcs());
	}

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

	/// <summary>
	/// A hand knocks its target about on its own timer, on a coin flip, and casts nothing else.
	/// </summary>
	/// <remarks>
	/// <b>The hand is spawned on its own here, and that is the fix for a flake this pin had twice.</b>
	/// Taking one out of the boss's wave looks more realistic and is what made it unreliable: his
	/// timer-4 and timer-7 branches give the self-destruct order within the first minute, so that
	/// particular hand is gone after one or two flips however long the window is. The first attempt at
	/// a fix stretched the window from forty seconds to a hundred and fifty on the theory that it would
	/// buy a dozen ticks. It bought none — a census over twelve runs showed one to two casts either
	/// way, and a zero in both samples.
	/// <para>
	/// A hand that no boss is about to detonate flips every seven or fifteen seconds for the whole
	/// window: six to nine casts across twelve runs, never zero. That is the difference between a pin
	/// that fails one run in ten and one that fails about one in eight thousand.
	/// </para>
	/// </remarks>
	[Fact]
	public void AHandKnocksItsTargetAboutOnItsOwnTimer()
	{
		BossAiHarness harness = BossAiHarness.For(SauroSupplyBase).WithWorldSize(2048)
			.WithAi(typeof(ResearcherTeselikAI), typeof(ShebanMysticalTyrhundAI), typeof(AggressiveNpcAI))
			.Build();
		using BossAiHarness _h = harness;
		Npc hand = harness.Spawn(Hand, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		harness.Engage(hand, player);
		BossAiHarness.DrainQueuedSkills(hand);

		var cast = new List<int>();
		for (int i = 0; i < 150; i++)
		{
			BossAiHarness.Rehate(hand, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			cast.AddRange(BossAiHarness.DrainQueuedSkills(hand).Select(c => c.SkillId));
		}

		// 16791 is the only skill our data gives it, and only the coin-flip branch casts it.
		Assert.NotEmpty(cast);
		Assert.All(cast, c => Assert.Equal(16791, c));
	}
}

/// <summary>Despawns when it hears a hand report in, so the notice is observable.</summary>
[Aion.GameServer.Ai.AIName("hand_death_probe")]
public class HandDeathListenerProbeAI : Aion.GameServer.Ai.Pattern.PatternAi
{
	private static readonly Aion.GameServer.Ai.Pattern.AiPattern Pattern_ =
		new Aion.GameServer.Ai.Pattern.AiPattern
		{
			OnMessage = Aion.GameServer.Ai.Pattern.AiPattern.Of(
				Aion.GameServer.Ai.Pattern.AiPattern.Branch(1, "a hand died",
					[Aion.GameServer.Ai.Pattern.When.Message(ResearcherTeselikAI.HandDied)],
					Aion.GameServer.Ai.Pattern.Do.DespawnSelf())),
		};

	public HandDeathListenerProbeAI(Npc owner)
		: base(owner)
	{
	}

	protected override Aion.GameServer.Ai.Pattern.AiPattern Pattern => Pattern_;
}
