using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Invincible Shabokan, whose two mechanics were a coin flip on one clock.
/// </summary>
/// <remarks>
/// Retail arms the earthquake at thirty seconds and re-arms it at fifty, and the sink at twenty and
/// twenty-two — two rungs on their own timers. This class ran one task from five seconds every thirty
/// and tossed a coin between them.
/// <para>
/// Found by <c>audit_timer_drift.py</c>'s opening check, which reported 5000 against a pattern that arms
/// 6000, 15000, 20000 and 30000 on entering combat.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class InvincibleShabokanAiTests
{
	private const int TiamatStronghold = 300510000;

	private const int Shabokan = 219352;
	private const int Sink = 283083;
	private const int SinkDamage = 283084;

	/// <summary>The earthquake's FX and the damage it drops.</summary>
	private const int QuakeFx = 283081;
	private const int QuakeDamage = 283082;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(InvincibleShabokanAI), typeof(SinkingSandAI), typeof(EarthQuakeAI),
				typeof(ShabokanEarthquakeFxAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Engages him with more players than retail's sink can reach.</summary>
	private static (Npc Boss, List<Player> Raid) Engaged(BossAiHarness harness, int players)
	{
		Npc boss = harness.Spawn(Shabokan, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < players; i++)
		{
			Player p = harness.SpawnPlayer(304f + i, 300f, 200f);
			raid.Add(p);
			if (i == 0)
				harness.Engage(boss, p);
			else
				boss.GetAggroList().AddHate(p, 10 + i);
		}

		return (boss, raid);
	}

	/// <summary>
	/// <b>No sink for twenty seconds.</b> This class could throw one at five.
	/// </summary>
	[Fact]
	public void TheSinkWaitsTwentySeconds()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness, 8);

		harness.Clock.Advance(TimeSpan.FromSeconds(18));

		Assert.Equal(0, Count(harness, Sink));
	}

	/// <summary>
	/// <b>And it takes six of the raid, not all of it.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>total_set_to_spawn</c> is six. This class put one on every player it could see inside
	/// thirty metres, so a large raid took more sinks than a small one.
	/// </remarks>
	[Fact]
	public void TheSinkTakesSixOfTheRaid()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness, 8);

		harness.Clock.Advance(TimeSpan.FromSeconds(21));

		Assert.Equal(6, Count(harness, Sink));
	}

	/// <summary>
	/// <b>One damage twin per target, not two.</b>
	/// </summary>
	/// <remarks>
	/// Retail spawns only the sink; the sink's own pattern places the <c>SinkDMG</c>. Shabokan used to
	/// place both himself, and both ids cast — so every target took two casts where retail has one. Six
	/// sinks now mean six twins, not twelve.
	/// </remarks>
	[Fact]
	public void OneDamageTwinPerTarget()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness, 8);

		harness.Clock.Advance(TimeSpan.FromSeconds(21));

		Assert.Equal(6, Count(harness, SinkDamage));
	}

	/// <summary>
	/// <b>The sink comes back every twenty-two seconds.</b>
	/// </summary>
	/// <remarks>
	/// Its own rung, not a share of a thirty-second coin flip — which gave it a turn about once a minute
	/// on average, less than half retail's rate.
	/// </remarks>
	[Fact]
	public void TheSinkReturnsEveryTwentyTwoSeconds()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, _) = Engaged(harness, 8);

		// Twenty to the first, then one every twenty-two: three sets inside seventy seconds.
		BossAiHarness.Watched seen = harness.WatchNew(
			70, () => BossAiHarness.SetHpPercent(boss, 90), Sink);

		Assert.Equal(18, seen.Total);
	}

	/// <summary>
	/// <b>Below sixteen per cent both rungs stop.</b>
	/// </summary>
	/// <remarks>
	/// Retail guards each with <c>is_hp_in_boundary larger_than=16</c>; this class had no health guard on
	/// either.
	/// </remarks>
	[Fact]
	public void BelowSixteenPerCentTheRungsStop()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, _) = Engaged(harness, 8);

		BossAiHarness.SetHpPercent(boss, 12);
		BossAiHarness.Watched seen = harness.WatchNew(
			70, () => BossAiHarness.SetHpPercent(boss, 12), Sink);

		Assert.Equal(0, seen.Total);
	}

	/// <summary>
	/// <b>The earthquake places the FX, and the FX drops the damage.</b>
	/// </summary>
	/// <remarks>
	/// This class placed the damage npc directly and never the FX at all — 283081 was bound to
	/// <c>general</c> and spawned by nothing.
	/// </remarks>
	[Fact]
	public void TheEarthquakePlacesItsFx()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness, 2);

		// Thirty seconds to the rung, and the FX drops its first damage a second later.
		harness.Clock.Advance(TimeSpan.FromSeconds(31));
		Assert.Equal(1, Count(harness, QuakeFx));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.True(Count(harness, QuakeDamage) > 0, "the FX dropped no damage npc");
	}

	/// <summary>
	/// <b>And it is a train, two seconds apart — four of the five rungs reach the ground.</b>
	/// </summary>
	/// <remarks>
	/// Retail writes five rungs on the FX's idle timer, each with its own flag var, at one, three, five,
	/// seven and nine seconds — and gives the FX itself <b>eight</b>. So the fifth is cut off by the FX's
	/// own lifetime and four land. That is retail's arithmetic, not a rounding here, and the pin asserts
	/// what reaches the ground rather than what the pattern writes.
	/// <para>
	/// The old code dropped one npc per earthquake, so the ground shook a quarter as much as it should.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheEarthquakeIsATrainOfFive()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness, 2);

		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		// Ticks at 1, 3, 5 and 7 seconds after the FX lands; the ninth-second rung never runs.
		BossAiHarness.Watched seen = harness.WatchNew(12, null, QuakeDamage);
		Assert.Equal(4, seen.Total);
	}

	/// <summary>
	/// <b>A sink stands for a minute and drops its own damage.</b>
	/// </summary>
	/// <remarks>
	/// Retail's sink lives sixty seconds and its <c>SinkDMG</c> twin six. Both ids ran the same
	/// three-second cast and four-second self-delete here, so the field a raid is meant to walk around
	/// was a flash.
	/// </remarks>
	[Fact]
	public void ASinkStandsForAMinuteAndDropsItsOwnDamage()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness, 8);

		harness.Clock.Advance(TimeSpan.FromSeconds(21));
		Assert.Equal(6, Count(harness, Sink));
		Assert.Equal(6, Count(harness, SinkDamage));

		// The twin casts on waking and removes itself; the sink stands on. Its six-second live_time is a
		// backstop the npc's own clock always beats, so deleting that lifetime is not something this pin
		// can see -- the same shape recorded for Terath's gravity pair.
		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(0, Count(harness, SinkDamage));
		Assert.Equal(6, Count(harness, Sink));
	}
}
