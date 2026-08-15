using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Rules of the shared pattern runtime, asserted directly rather than through a boss that happens to
/// exercise them.
/// </summary>
/// <remarks>
/// Every translated boss inherits these, so a mistake here is a mistake in all of them at once — which
/// is the cost of sharing the machinery, and the reason it gets its own tests. The probe below records
/// which branches ran, so the assertions are about evaluation order and guard consumption rather than
/// about any encounter's mechanics.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PatternAiTests
{
	private const int RentusBase = 300280000;

	/// <summary>Any NPC will do; the probe AI is attached by name, not by template.</summary>
	private const int SomeNpc = 217309;

	private static BossAiHarness NewHarness() => BossAiHarness.For(RentusBase)
		.WithAi(typeof(PatternProbeAI), typeof(AggressiveNpcAI))
		.Build();

	private static (BossAiHarness Harness, PatternProbeAI Ai, Npc Boss, Player Player) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.SpawnWithAi(SomeNpc, PatternProbeAI.Name);
		Player player = harness.SpawnPlayer();
		var ai = (PatternProbeAI)boss.GetAi();
		harness.Engage(boss, player);
		return (harness, ai, boss, player);
	}

	[Fact]
	public void RunsTheHighestPriorityBranchThatMatchesAndStopsThere()
	{
		var (harness, ai, boss, _) = Engaged();
		using (harness)
		{
			// Both the 90 and the 50 branch guard timer 1, and at 40% HP both match.
			BossAiHarness.SetHpPercent(boss, 40);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));

			Assert.Equal(["below-90"], ai.Ran);
		}
	}

	[Fact]
	public void LeavesAFlagUnconsumedWhenAnEarlierGuardFails()
	{
		var (harness, ai, boss, _) = Engaged();
		using (harness)
		{
			// The once-only branch is gated on HP *before* its flag, so ticking above the threshold must
			// not spend the flag. Getting this wrong loses the step entirely rather than delaying it,
			// which is invisible from the outside until someone plays the fight.
			BossAiHarness.SetHpPercent(boss, 95);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			Assert.DoesNotContain("once-at-30", ai.Ran);

			BossAiHarness.SetHpPercent(boss, 20);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			Assert.Contains("once-at-30", ai.Ran);
		}
	}

	[Fact]
	public void RunsAOnceOnlyBranchExactlyOnceHoweverManyTimesItsThresholdIsCrossed()
	{
		var (harness, ai, boss, _) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 20);
			for (int i = 0; i < 5; i++)
				harness.Clock.Advance(TimeSpan.FromSeconds(1));

			Assert.Single(ai.Ran, label => label == "once-at-30");
		}
	}

	[Fact]
	public void ReplaysItsStepsAfterAReset()
	{
		var (harness, ai, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 20);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			Assert.Contains("once-at-30", ai.Ran);

			boss.GetAi().OnGeneralEvent(AiEventType.BACK_HOME);
			ai.Ran.Clear();
			BossAiHarness.SetHpPercent(boss, 100);
			harness.Engage(boss, player);
			BossAiHarness.SetHpPercent(boss, 20);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));

			// A reset boss fights from the top, so its one-shot steps have to be available again.
			Assert.Contains("once-at-30", ai.Ran);
		}
	}

	[Fact]
	public void StopsTheChainWhenATickMatchesNothing()
	{
		var (harness, ai, boss, _) = Engaged();
		using (harness)
		{
			// Only a branch can re-arm a slot, so a tick that matches nothing ends that chain until
			// something else arms it. This is retail behaviour and the reason patterns carry a catch-all;
			// the probe's is on timer 1, so timer 2 has none.
			ai.ArmTimer(2, 1000);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			int armed = harness.Clock.ArmedTimerCount;

			harness.Clock.Advance(TimeSpan.FromMinutes(1));

			// Timer 1 keeps re-arming itself and timer 2 is gone, so the count held steady rather than
			// growing or emptying.
			Assert.Equal(armed, harness.Clock.ArmedTimerCount);
		}
	}

	[Fact]
	public void DoesNotRunBattleTimersOutsideCombat()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.SpawnWithAi(SomeNpc, PatternProbeAI.Name);
		var ai = (PatternProbeAI)boss.GetAi();

		// Never engaged, so nothing armed the timers in the first place.
		harness.Clock.Advance(TimeSpan.FromMinutes(1));
		Assert.Empty(ai.Ran);
		Assert.Equal(0, harness.Clock.ArmedTimerCount);
	}

	[Fact]
	public void ArmingASlotThatIsAlreadyArmedReplacesItRatherThanStacking()
	{
		var (harness, ai, boss, _) = Engaged();
		using (harness)
		{
			int armed = harness.Clock.ArmedTimerCount;
			ai.ArmTimer(1, 5000);
			ai.ArmTimer(1, 5000);
			ai.ArmTimer(1, 5000);

			// Retail has one timer per slot. If re-arming stacked, a chain that re-arms itself every tick
			// would double its own rate on every pass.
			Assert.Equal(armed, harness.Clock.ArmedTimerCount);
		}
	}

	[Fact]
	public void DespawnsBySpawnIdAndLetsLifetimesExpireOnTheirOwn()
	{
		var (harness, ai, boss, _) = Engaged();
		using (harness)
		{
			ai.SpawnNear(PatternProbeAI.Add, spawnId: 1, count: 2, range: 0f, liveSeconds: 0);
			ai.SpawnNear(PatternProbeAI.Add, spawnId: 2, count: 1, range: 0f, liveSeconds: 10);
			Assert.Equal(3, harness.LiveNpcs().Count(n => n.GetNpcId() == PatternProbeAI.Add));

			ai.DespawnGroup(1);
			Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == PatternProbeAI.Add));

			harness.Clock.Advance(TimeSpan.FromSeconds(10));
			Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == PatternProbeAI.Add));
		}
	}

	[Fact]
	public void StopsEveryTimerWhenTheOwnerDies()
	{
		var (harness, ai, boss, _) = Engaged();
		using (harness)
		{
			Assert.True(harness.Clock.ArmedTimerCount > 0);

			boss.GetAi().OnGeneralEvent(AiEventType.Died);

			// The chain goes with the fight, so nothing this one armed can fire into the next.
			ai.Ran.Clear();
			harness.Clock.Advance(TimeSpan.FromMinutes(5));
			Assert.Empty(ai.Ran);
		}
	}

	[Fact]
	public void LetsASpawnOutliveItsSpawnerUntilItsOwnLifetimeRunsOut()
	{
		var (harness, ai, boss, _) = Engaged();
		using (harness)
		{
			ai.SpawnNear(PatternProbeAI.Add, spawnId: 1, count: 1, range: 0f, liveSeconds: 30);
			boss.GetAi().OnGeneralEvent(AiEventType.Died);

			// The lifetime belongs to the add, not to whoever spawned it. Tying it to the spawner would
			// strand every add whose group no branch despawns -- the probe's pattern has no on_die branch,
			// so this one is exactly that case.
			harness.Clock.Advance(TimeSpan.FromSeconds(20));
			Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == PatternProbeAI.Add));

			harness.Clock.Advance(TimeSpan.FromSeconds(10));
			Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == PatternProbeAI.Add));
		}
	}

	[Fact]
	public void RunsTheIdleTimerWhetherOrNotItIsFighting()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.SpawnWithAi(SomeNpc, PatternProbeAI.Name);
		var ai = (PatternProbeAI)boss.GetAi();

		// Never engaged. A battle timer would do nothing here; the idle timer is the one that runs
		// around a fight rather than in it, and half its uses are on NPCs that never fight.
		ai.SetIdleTimer(2000);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Empty(ai.Ran);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(["idle"], ai.Ran);
	}

	[Fact]
	public void KeepsTheIdleTimerGoingWhenItsBranchSetsItAgain()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.SpawnWithAi(SomeNpc, PatternProbeAI.Name);
		var ai = (PatternProbeAI)boss.GetAi();

		ai.SetIdleTimer(2000);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		harness.Clock.Advance(TimeSpan.FromSeconds(12));

		// Fires at 2s and then every 4s: 2, 6, 10, 14.
		Assert.Equal(4, ai.Ran.Count(label => label == "idle"));
	}

	[Fact]
	public void KeepsOnlyOneIdleTimerHoweverOftenItIsSet()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.SpawnWithAi(SomeNpc, PatternProbeAI.Name);
		var ai = (PatternProbeAI)boss.GetAi();

		int idle = harness.Clock.ArmedTimerCount;
		ai.SetIdleTimer(5000);
		ai.SetIdleTimer(5000);
		ai.SetIdleTimer(5000);

		// One slot, not thirty. Setting it again replaces what was there rather than stacking.
		Assert.Equal(idle + 1, harness.Clock.ArmedTimerCount);
	}

	[Fact]
	public void StopsTheIdleTimerWhenTheOwnerDies()
	{
		var (harness, ai, boss, _) = Engaged();
		using (harness)
		{
			ai.SetIdleTimer(5000);
			ai.Ran.Clear();

			boss.GetAi().OnGeneralEvent(AiEventType.Died);
			harness.Clock.Advance(TimeSpan.FromMinutes(1));

			Assert.DoesNotContain("idle", ai.Ran);
		}
	}

	[Fact]
	public void IgnoresATimerThatComesDueOutsideCombat()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.SpawnWithAi(SomeNpc, PatternProbeAI.Name);
		var ai = (PatternProbeAI)boss.GetAi();

		// Never engaged, so nothing can put it back into combat -- an engaged boss cannot be held out of
		// one, because its attack task re-enters FIGHT on every swing. Health is set below a threshold a
		// branch records at, so a timer that does fire is visible rather than silently matching nothing.
		BossAiHarness.SetHpPercent(boss, 40);
		ai.ArmTimer(1, 1000);
		harness.Clock.Advance(TimeSpan.FromMinutes(1));

		// Retail battle timers only run in battle, so this one comes due and does nothing.
		Assert.Empty(ai.Ran);
	}
}

/// <summary>A pattern with no encounter behind it, recording which branches the runtime chose.</summary>
[AIName(PatternProbeAI.Name)]
public sealed class PatternProbeAI : PatternAi
{
	public const string Name = "pattern_runtime_probe";

	/// <summary>Any spawnable NPC; only its identity matters, for counting.</summary>
	public const int Add = 282606;

	public List<string> Ran { get; } = new List<string>();

	private static PatternAction Record(string label) => ai => ((PatternProbeAI)ai).Ran.Add(label);

	private static readonly AiPattern Probe = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(9, "start", When.Always, Do.ArmTimer(1, 1000))),

		OnBattleTimer = Of(
			Branch(5, "below-90", [When.HpBelow(90), When.Timer(1)],
				Record("below-90"), Do.ArmTimer(1, 1000)),

			// Deliberately lower priority than the branch above and matching at the same time, so the
			// first-match-wins rule is observable rather than assumed.
			Branch(4, "below-50", [When.HpBelow(50), When.Timer(1)],
				Record("below-50"), Do.ArmTimer(1, 1000)),

			// HP guard ahead of the flag, the order retail writes these in.
			Branch(8, "once-at-30", [When.HpBelow(30), When.Timer(1), When.FirstTime(1)],
				Record("once-at-30"), Do.ArmTimer(1, 1000)),

			// The catch-all re-arm every real pattern carries. Without one a tick that matches nothing
			// ends the chain, since only a branch can re-arm a slot.
			Branch(1, "repeat", [When.Timer(1)], Do.ArmTimer(1, 1000))),

		// A heartbeat that keeps itself going, the shape controllers and orbs use.
		OnIdleTimer = Of(
			Branch(1, "idle", When.Always,
				Record("idle"), Do.SetIdleTimer(4000))),
	};

	public PatternProbeAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Probe;
}
