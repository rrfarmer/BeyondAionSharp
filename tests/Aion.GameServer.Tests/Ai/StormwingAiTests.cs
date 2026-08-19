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

	/// <summary>Retail's <c>BIDCTN_SumLightning_55_Ae</c>, which this fight never summoned.</summary>
	private const int Lightning = 281798;

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

	/// <summary>
	/// <b>Two twisters a band above thirty-five per cent</b>, once each — this is the hard variant.
	/// </summary>
	/// <remarks>
	/// This pin asserted four, which was the class's old flat count for both modes. Retail gives hard
	/// mode <i>fewer</i> per band than normal, not more: two in the top four bands and three in the
	/// bottom three, against normal's four throughout. The harness boss is 216183, the hard one, which
	/// is the variant the instance handler actually spawns.
	/// </remarks>
	[Fact]
	public void SummonsTwoTwistersOnEachUpperHpBandExactlyOnce()
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

			// Two per band up here: one sharp, one root, alternating from index zero.
			Assert.Equal(sharp + 1, Count(harness, SharpTwister));
			Assert.Equal(root + 1, Count(harness, RootTwister));
			Assert.Contains(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == ThreshingWind);
		}

		// A band already crossed must not fire again on a later tick. Counted as arrivals rather than
		// as a standing total: the twisters now expire on retail's own thirty seconds, so a live count
		// thirty seconds later measures the lifetime and not the guard.
		BossAiHarness.Watched later = harness.WatchNew(
			30, () => BossAiHarness.Rehate(boss, player), SharpTwister, RootTwister);
		Assert.Equal(0, later.Total);
	}

	[Fact]
	public void ScattersTwistersOnAlternatingBands()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		// Band 0 (95) scatters to its diagonals; band 1 (80) drops them on top of him. Two per band in
		// hard mode's upper four, so two distinct positions and then one more.
		TickBandAt(harness, boss, player, 95);
		var scattered = harness.LiveNpcs()
			.Where(n => n.GetNpcId() is SharpTwister or RootTwister)
			.Select(n => (n.GetX(), n.GetY())).Distinct().Count();
		Assert.Equal(2, scattered);

		int before = harness.LiveNpcs().Count(n => n.GetNpcId() is SharpTwister or RootTwister);
		TickBandAt(harness, boss, player, 80);
		var stacked = harness.LiveNpcs()
			.Where(n => n.GetNpcId() is SharpTwister or RootTwister)
			.Select(n => (n.GetX(), n.GetY())).Distinct().Count();

		// Two more appeared, both sharing the boss's own point, so the distinct-position count
		// grows by exactly one.
		Assert.Equal(before + 2, harness.LiveNpcs().Count(n => n.GetNpcId() is SharpTwister or RootTwister));
		Assert.Equal(scattered + 1, stacked);
	}

	/// <summary>
	/// <b>The escalation never stops</b>, and it alternates route sets rather than kinds.
	/// </summary>
	/// <remarks>
	/// This pin used to assert four waves -- sharp twice, root twice -- and then silence for the rest of
	/// the fight. That was the class's reading, not retail's. The four timer-1 branches are two pairs,
	/// bleed and root, each pair holding a test-and-set and a test-and-unset copy of one flag: on every
	/// tick the bleed pair is tried at seventy per cent and the root pair takes what is left, and
	/// whichever fires flips the flag so the next wave takes the other route set.
	/// <para>
	/// <b>Which kind arrives is a coin toss, so nothing here asserts it.</b> What is deterministic is
	/// that hard mode summons on every tick -- its root pair is unconditional, so the "nothing happens"
	/// outcome is impossible -- and that consecutive waves take disjoint routes.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheEscalationKeepsComingAndAlternatesItsRoutes()
	{
		using var harness = BossAiHarness.For(BeshmundirTemple)
			.WithWorldSize(2048)
			.WithWalkerRoutes()
			.WithAi(typeof(StormwingAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(Stormwing, 558.306f, 1369.02f, 224.795f, 70);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		// Above half health the escalation timer ticks and does nothing.
		harness.Clock.Advance(TimeSpan.FromSeconds(60));
		Assert.Equal(0, Count(harness, SharpTwisterElite));
		Assert.Equal(0, Count(harness, RootTwisterElite));

		BossAiHarness.SetHpPercent(boss, 40);
		boss.SetTarget(player);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

		// Six waves, thirty seconds apart. The elites live fifteen seconds in hard mode, so each wave
		// has gone before the next lands and every sample is one wave on its own.
		var routeSets = new List<HashSet<string>>();
		for (int i = 0; i < 6; i++)
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(30));
			List<Npc> wave = harness.LiveNpcs()
				.Where(n => n.GetNpcId() is SharpTwisterElite or RootTwisterElite).ToList();

			// THE ASSERTION THAT PINS THE CHANGE: the fifth and sixth waves matter as much as the
			// first. Under the old four-wave cap these were empty.
			Assert.Equal(8, wave.Count);
			routeSets.Add(wave.Select(n => n.GetSpawn().GetWalkerId()!).ToHashSet());
		}

		Assert.All(routeSets, s => Assert.All(s, r => Assert.StartsWith("NPCPathPath_RudraWind_", r)));

		// Consecutive waves take the other half of the sixteen routes, which is the flag doing its work.
		for (int i = 1; i < routeSets.Count; i++)
			Assert.Empty(routeSets[i].Intersect(routeSets[i - 1]));
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
		//
		// Checked after the wait below rather than here, because the twisters standing at his death keep
		// their own despawn timers and those are meant to outlive him. A leaked repeating task re-arms
		// itself across that wait, so asking afterwards is the stronger question, not the weaker one.

		BossAiHarness.DrainQueuedSkills(boss);

		// Held as the twisters themselves rather than as a count. They now expire on retail's own
		// timers, so a count taken two minutes later drops whether or not anything new was summoned --
		// the question this pin asks is whether a dead boss summons, and only identity answers it.
		var standing = harness.LiveNpcs()
			.Where(n => n.GetNpcId() is SharpTwister or RootTwister)
			.ToHashSet();
		harness.Clock.Advance(TimeSpan.FromMinutes(2));

		Assert.Equal(0, harness.Clock.ArmedTimerCount);
		Assert.Empty(BossAiHarness.DrainQueuedSkills(boss));
		Assert.DoesNotContain(harness.LiveNpcs(),
			n => n.GetNpcId() is SharpTwister or RootTwister && !standing.Contains(n));
	}

	/// <summary>
	/// <b>The lightning, which was missing entirely.</b> It lands only below fifty per cent.
	/// </summary>
	/// <remarks>
	/// Retail runs two battle timers that hand back and forth, and only the bottom two rungs of the
	/// timer-3 ladder summon anything — above fifty the chain still runs and drops nothing. So a fight
	/// held at full health should show none however long it goes on, which is the half of the guard a
	/// "does it ever appear" test would miss.
	/// <para>
	/// One add, not a wave: the below-thirty rung reads <c>spawn_on_multi_target</c> and carries
	/// <c>total_set_to_spawn=1</c>.
	/// </para>
	/// </remarks>
	[Fact]
	public void NoLightningAboveFiftyPercent()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);
		BossAiHarness.SetHpPercent(boss, 90);

		// Two minutes is four turns of the twenty-second hand-off at this health.
		harness.Clock.Advance(TimeSpan.FromMinutes(2));

		Assert.Equal(0, Count(harness, Lightning));
	}

	/// <summary><b>And below fifty it arrives, one at a time.</b></summary>
	/// <remarks>
	/// <b>Each band is sampled inside its own lifetime, and the two are not the same.</b> Timer 2 fires
	/// at fifteen seconds and arms timer 3 at twenty-five in the 31-50 band and fifteen below thirty, so
	/// the lightning lands at forty and thirty seconds respectively -- and then lives fifteen seconds
	/// against seven. A single sample point suits neither: forty-five seconds catches the 45%% one and
	/// misses the 25%% one entirely, which is what the first version of this pin did.
	/// </remarks>
	[Theory]
	[InlineData(45, 45)]
	[InlineData(25, 33)]
	public void BelowFiftyPercentOneLightningLands(int hpPercent, int sampleAtSeconds)
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);
		BossAiHarness.SetHpPercent(boss, hpPercent);

		harness.Clock.Advance(TimeSpan.FromSeconds(sampleAtSeconds));

		Assert.Equal(1, Count(harness, Lightning));
	}

	/// <summary>
	/// <b>The twisters walk the routes retail names</b>, rather than standing where they appeared.
	/// </summary>
	/// <remarks>
	/// Every twister spawn in the pattern carries a <c>pathname</c> and this class started none of them,
	/// so all four stood at their offsets. Asserting the walker id rather than a position: the harness
	/// has no mover, so the route is the only thing that can be observed here — but it is the thing that
	/// was missing, and a twister with no route cannot sweep whatever the mover does.
	/// </remarks>
	[Fact]
	public void TheTwistersTakeTheirRetailRoutes()
	{
		using var harness = BossAiHarness.For(BeshmundirTemple)
			.WithWorldSize(2048)
			.WithWalkerRoutes()
			.WithAi(typeof(StormwingAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(Stormwing, 558.306f, 1369.02f, 224.795f, 70);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		TickBandAt(harness, boss, player, 94);

		List<Npc> twisters = harness.LiveNpcs()
			.Where(n => n.GetNpcId() == SharpTwister || n.GetNpcId() == RootTwister)
			.ToList();
		Assert.Equal(2, twisters.Count);

		// The ninety-five band scatters, so it takes the wide set — and all four differ, which is what
		// rules out every twister being handed the same route.
		var routes = twisters.Select(n => n.GetSpawn().GetWalkerId()).ToList();
		Assert.All(routes, r => Assert.StartsWith("NPCPathPath_RudraWindC", r));
		Assert.All(routes, r => Assert.DoesNotContain("_1", r));
		Assert.Equal(2, routes.Distinct().Count());
	}

	/// <summary>
	/// <b>The lifetimes were reversed</b>: the opening band held its twisters for eighty seconds and
	/// the last for thirty, when retail says the opposite.
	/// </summary>
	/// <remarks>
	/// They were transcribed in retail's branch order — p40 down to p34, which is 5% first — and then
	/// indexed by a band array running 95% first. The class's own remark stated the order correctly and
	/// the array applied it backwards, so <b>reading the comment was not enough to catch this</b>;
	/// nothing pinned the lifetimes at all until now.
	/// </remarks>
	[Fact]
	public void TheOpeningBandsTwistersLeaveAtThirtySeconds()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		TickBandAt(harness, boss, player, 94);
		var opening = harness.LiveNpcs()
			.Where(n => n.GetNpcId() is SharpTwister or RootTwister).ToHashSet();
		Assert.NotEmpty(opening);

		// TickBandAt advances ten seconds and the band fires on that tick, so the twisters are new here.
		// Twenty-eight seconds on they are all still standing...
		harness.Clock.Advance(TimeSpan.FromSeconds(28));
		Assert.All(opening, tw => Assert.True(tw.IsSpawned(), "gone before thirty seconds"));

		// ...and a little past thirty they are gone. Under the reversed table they stood for eighty.
		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.All(opening, tw => Assert.False(tw.IsSpawned(), "still standing past thirty seconds"));
	}

	/// <summary>
	/// <b>Hard mode's two extra rungs, which only exist between thirty-one and fifty per cent.</b>
	/// </summary>
	/// <remarks>
	/// These are the only twisters in the fight aimed at a player rather than at a route: a bleed
	/// twister planted on whoever he is facing for thirty seconds, and a root twister scattered five
	/// metres off a random attacker for five. Normal mode has neither, so the band is a different fight
	/// in the two modes rather than the same one scaled.
	/// <para>
	/// Counted as arrivals over a window, because the five-second one is gone almost as soon as it
	/// lands and a standing count would miss it on most samples.
	/// </para>
	/// </remarks>
	[Fact]
	public void HardModeAddsTwoTwistersInTheMiddleBand()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		// Every band above forty has to be spent before the window opens: the ladder spawns the same two
		// npc ids, so a window that still had bands to cross would count those instead. THE FIRST
		// VERSION OF THIS PIN DID NOT DO THIS, and it passed with both rungs deleted -- it was
		// measuring the band ladder and asserting nothing at all about what it was named for.
		foreach (int band in new[] { 94, 79, 64, 49 })
			TickBandAt(harness, boss, player, band);

		BossAiHarness.SetHpPercent(boss, 40);

		// Two full turns of the chain. One turn is not enough: the chain was already part-way through
		// its above-fifty cadence when the health dropped, so the first rung inside a short window can
		// be either of the two.
		BossAiHarness.Watched seen = harness.WatchNew(
			120, () => BossAiHarness.Rehate(boss, player), SharpTwister, RootTwister);

		Assert.True(seen.Total >= 2,
			$"the middle band should plant a bleed and a root twister: saw {seen.Total}");
	}

	/// <summary><b>And above the band it plants neither.</b></summary>
	[Fact]
	public void OutsideTheMiddleBandThereAreNoPlantedTwisters()
	{
		using var harness = NewHarness();
		Npc boss = SpawnBoss(harness);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.795f);
		harness.Engage(boss, player);

		// The band ladder spawns the same two npc ids, so the bands above sixty have to be spent before
		// the window opens or it would count them instead. The first version of this pin did exactly
		// that and read three band waves as planted twisters.
		foreach (int band in new[] { 94, 79, 64 })
			TickBandAt(harness, boss, player, band);

		// Sixty per cent: past every band it can still cross, and above the 31-50 rungs.
		BossAiHarness.SetHpPercent(boss, 60);
		BossAiHarness.Watched seen = harness.WatchNew(
			45, () => BossAiHarness.Rehate(boss, player), SharpTwister, RootTwister);

		Assert.Equal(0, seen.Total);
	}
}
