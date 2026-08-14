using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="StormwingAI"/>, ported from retail patterns IDCT_Rudra /
/// IDCTH_Rudra (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Before that class existed this boss summoned nothing at all and his four twister NPCs sat in
/// npc_templates spawned by nothing, so everything asserted here is behaviour that did not previously
/// happen. The band timer and the escalation timer both run on the harness's virtual clock, which is
/// what makes a fight with a 10s beat and a 30s escalation assertable in milliseconds.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class StormwingAiTests
{
	private const int BeshmundirTemple = 300170000;
	private const int Stormwing = 216183;

	private const int SharpTwister = 281796;
	private const int RootTwister = 281794;
	private const int SharpTwisterElite = 281797;
	private const int RootTwisterElite = 281795;

	private const int ThreshingWind = 18613;
	private const int MidnightWind = 18614;

	private static BossAiHarness NewHarness() => BossAiHarness.For(BeshmundirTemple)
		.WithWorldSize(2048)
		.WithAi(typeof(StormwingAI), typeof(AggressiveNpcAI))
		.Build();

	private static Npc SpawnBoss(BossAiHarness harness) =>
		harness.Spawn(Stormwing, 558.306f, 1369.02f, 224.795f, 70);

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Drops HP just under <paramref name="percent"/> and lets one band tick elapse.</summary>
	private static void TickBandAt(BossAiHarness harness, Npc boss, Player player, int percent)
	{
		BossAiHarness.SetHpPercent(boss, percent);
		boss.SetTarget(player);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void OpensOnTheAggroTarget()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		BossAiHarness.QueuedCast opener = Assert.Single(BossAiHarness.DrainQueuedSkills(boss));
		Assert.Equal(MidnightWind, opener.SkillId);
	}

	[Fact]
	public void SummonsFourTwistersOnEachHpBandExactlyOnce()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);
		BossAiHarness.DrainQueuedSkills(boss);

		// Retail bands: 95/80/65/50/35/20/5, each firing once. Below 50 the escalation timer also
		// runs, so this only walks the bands above it; the escalation has its own test.
		foreach (int band in new[] { 95, 80, 65 })
		{
			int sharp = Count(harness, SharpTwister);
			int root = Count(harness, RootTwister);

			TickBandAt(harness, boss, player, band);

			Assert.Equal(sharp + 2, Count(harness, SharpTwister));
			Assert.Equal(root + 2, Count(harness, RootTwister));
			Assert.Contains(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == ThreshingWind);
		}

		// A band already crossed must not fire again on a later tick.
		int before = Count(harness, SharpTwister) + Count(harness, RootTwister);
		harness.Clock.Advance(TimeSpan.FromSeconds(30));
		Assert.Equal(before, Count(harness, SharpTwister) + Count(harness, RootTwister));
	}

	[Fact]
	public void ScattersTwistersOnAlternatingBands()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		// Band 0 (95) scatters to the four diagonals; band 1 (80) drops them on top of him.
		TickBandAt(harness, boss, player, 95);
		var scattered = harness.LiveNpcs()
			.Where(n => n.GetNpcId() is SharpTwister or RootTwister)
			.Select(n => (n.GetX(), n.GetY())).Distinct().Count();
		Assert.Equal(4, scattered);

		int before = harness.LiveNpcs().Count(n => n.GetNpcId() is SharpTwister or RootTwister);
		TickBandAt(harness, boss, player, 80);
		var stacked = harness.LiveNpcs()
			.Where(n => n.GetNpcId() is SharpTwister or RootTwister)
			.Select(n => (n.GetX(), n.GetY())).Distinct().Count();

		// Four more appeared, all sharing the boss's own point, so the distinct-position count
		// grows by exactly one.
		Assert.Equal(before + 4, harness.LiveNpcs().Count(n => n.GetNpcId() is SharpTwister or RootTwister));
		Assert.Equal(scattered + 1, stacked);
	}

	[Fact]
	public void EscalatesBelowHalfHealthSharpThenRoot()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		// Above half health the escalation timer ticks and does nothing.
		harness.Clock.Advance(TimeSpan.FromSeconds(60));
		Assert.Equal(0, Count(harness, SharpTwisterElite));
		Assert.Equal(0, Count(harness, RootTwisterElite));

		BossAiHarness.SetHpPercent(boss, 40);
		boss.SetTarget(player);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

		// Four waves, 30s apart: sharp twice, then root twice. Hard mode sends eight per wave.
		var waves = new List<(int Sharp, int Root)>();
		for (int i = 0; i < 4; i++)
		{
			int sharp = Count(harness, SharpTwisterElite);
			int root = Count(harness, RootTwisterElite);
			harness.Clock.Advance(TimeSpan.FromSeconds(30));
			waves.Add((Count(harness, SharpTwisterElite) - sharp, Count(harness, RootTwisterElite) - root));
		}
		Assert.Equal([(8, 0), (8, 0), (0, 8), (0, 8)], waves);

		// The escalation is four waves and then stops.
		int total = Count(harness, SharpTwisterElite) + Count(harness, RootTwisterElite);
		harness.Clock.Advance(TimeSpan.FromMinutes(2));
		Assert.Equal(total, Count(harness, SharpTwisterElite) + Count(harness, RootTwisterElite));
	}

	[Fact]
	public void StopsEveryTimerWhenItDies()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		TickBandAt(harness, boss, player, 95);
		Assert.True(harness.Clock.ArmedTimerCount > 0, "the fight should have live timers before death");

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		// Asserting on the timers themselves, not on their effects. Both timer bodies already bail on
		// IsDead(), so a leaked repeating task looks identical from the outside while still running
		// forever — an earlier version of this test passed with the cancellation removed entirely.
		Assert.Equal(0, harness.Clock.ArmedTimerCount);

		BossAiHarness.DrainQueuedSkills(boss);
		int twisters = harness.LiveNpcs().Count(n => n.GetNpcId() is SharpTwister or RootTwister);
		harness.Clock.Advance(TimeSpan.FromMinutes(2));

		Assert.Empty(BossAiHarness.DrainQueuedSkills(boss));
		Assert.Equal(twisters, harness.LiveNpcs().Count(n => n.GetNpcId() is SharpTwister or RootTwister));
	}
}
