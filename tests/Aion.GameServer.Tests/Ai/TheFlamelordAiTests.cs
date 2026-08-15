using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="TheFlamelordAI"/>, ported from retail pattern Raksha_Firemage_Nmd
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// This boss is the first of the timer-driven group. aionemu had rendered its 25s delivery rotation as
/// an HP ladder — one executor at 40%, two at 30%, three at 20%, four at 10% — so the tests that matter
/// are about cadence rather than thresholds: executors arrive on the clock, in rotation, and the wave
/// thickens only near the end. It also never spawned Torment Blaze at all.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TheFlamelordAiTests
{
	private const int RaksangRuins = 300610000;
	private const int Flamelord = 217451;
	private const int TormentBlaze = 282459;

	private const int BlazingCut = 19923;
	private const int FlameBurst = 19925;

	private static readonly int[] Executors = { 282451, 282452, 282453, 282454 };

	private static BossAiHarness NewHarness() => BossAiHarness.For(RaksangRuins)
		.WithAi(typeof(TheFlamelordAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI), typeof(ScaldingExecutorAI))
		.Build();

	private static int ExecutorCount(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => Executors.Contains(n.GetNpcId()));

	[Fact]
	public void OpensWithBlazingCutAndRepeatsItEveryNineSeconds()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Flamelord);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		BossAiHarness.QueuedCast opener = Assert.Single(BossAiHarness.DrainQueuedSkills(boss));
		Assert.Equal(BlazingCut, opener.SkillId);

		// Three beats in 27s, and nothing else on this timer.
		harness.Clock.Advance(TimeSpan.FromSeconds(27));
		Assert.Equal(3, BossAiHarness.DrainQueuedSkills(boss).Count(c => c.SkillId == BlazingCut));
	}

	[Fact]
	public void DeliversOneExecutorEveryTwentyFiveSecondsInRotation()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Flamelord);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		// Nothing has been delivered before the first tick falls due.
		harness.Clock.Advance(TimeSpan.FromSeconds(24));
		Assert.Equal(0, ExecutorCount(harness));

		// Four ticks send the four executors, one each, in order.
		var order = new List<int>();
		for (int i = 0; i < 4; i++)
		{
			int before = ExecutorCount(harness);
			harness.Clock.Advance(TimeSpan.FromSeconds(25));
			Assert.Equal(before + 1, ExecutorCount(harness));
			order.Add(harness.LiveNpcs().Select(n => n.GetNpcId()).Last(id => Executors.Contains(id)));
		}
		Assert.Equal(Executors, order);
	}

	[Fact]
	public void ThickensTheDeliveryWaveNearTheEnd()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Flamelord);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		// Above the threshold a tick sends one.
		harness.Clock.Advance(TimeSpan.FromSeconds(25));
		Assert.Equal(1, ExecutorCount(harness));

		BossAiHarness.SetHpPercent(boss, 20);
		int before = ExecutorCount(harness);
		harness.Clock.Advance(TimeSpan.FromSeconds(25));

		// Retail's low-HP delivery branches send several at once rather than one.
		Assert.Equal(before + 3, ExecutorCount(harness));
	}

	[Fact]
	public void BurstsOnceAtEachOfItsThreeHpSteps()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Flamelord);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);
		BossAiHarness.DrainQueuedSkills(boss);

		var burstsAt = new List<int>();
		foreach (int hp in new[] { 80, 74, 60, 49, 30, 24, 20 })
		{
			BossAiHarness.SetHpPercent(boss, hp);
			BossAiHarness.Rehate(boss, player);
			int observed = boss.GetLifeStats().GetHpPercentage();
			harness.Clock.Advance(TimeSpan.FromSeconds(7));
			if (BossAiHarness.DrainQueuedSkills(boss).Any(c => c.SkillId == FlameBurst))
				burstsAt.Add(observed);
		}

		// Retail's three one-shot steps are 75/50/25; each fires on the first tick below it and never
		// again. The sampled points sit just under each, so the observed HP is one lower.
		Assert.Equal(3, burstsAt.Count);
		Assert.True(burstsAt[0] < 75 && burstsAt[1] < 50 && burstsAt[2] < 25,
			$"bursts fired at {string.Join(",", burstsAt)}");
	}

	[Fact]
	public void SpawnsTormentBlazeOnItsFlameTimer()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Flamelord);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		int Blazes() => harness.LiveNpcs().Count(n => n.GetNpcId() == TormentBlaze);
		Assert.Equal(0, Blazes());

		// Nothing spawned this NPC before this class existed; retail brings one every 20s.
		harness.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.Equal(1, Blazes());

		harness.Clock.Advance(TimeSpan.FromSeconds(40));
		Assert.Equal(3, Blazes());
	}

	[Fact]
	public void StopsEveryTimerWhenItDies()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(Flamelord);
		Player player = harness.SpawnPlayer();
		int beforeEngage = harness.Clock.ArmedTimerCount;
		harness.Engage(boss, player);
		int engaged = harness.Clock.ArmedTimerCount;

		// The fight arms four: the 9s beat, the 7s burst, the 20s flame and the 25s delivery.
		Assert.Equal(beforeEngage + 4, engaged);

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		// Asserted on the timers themselves rather than on their effects: every tick body already bails
		// on IsDead(), so a leaked repeating task is invisible from the outside while still running
		// forever. The count is relative because the engine arms timers of its own alongside the AI's.
		Assert.Equal(engaged - 4, harness.Clock.ArmedTimerCount);
	}
}
