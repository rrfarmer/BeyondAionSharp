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

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(InvincibleShabokanAI), typeof(SinkingSandAI), typeof(EarthQuakeAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
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
	/// <b>One npc per target, not two.</b>
	/// </summary>
	/// <remarks>
	/// Retail spawns only the sink and lets the sink's own pattern place its <c>SinkDMG</c> twin. Both
	/// ids run <c>SinkingSandAI</c> here, which casts once and removes itself, so spawning the pair meant
	/// two casts where retail has one.
	/// </remarks>
	[Fact]
	public void OnlyTheSinkIsPlaced()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness, 8);

		harness.Clock.Advance(TimeSpan.FromSeconds(21));

		Assert.Equal(0, Count(harness, SinkDamage));
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
}
