using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Celestius, whose summons had no guards at all.
/// </summary>
/// <remarks>
/// Retail's <c>Elim_ComadAe</c> calls his three walkers only while his health is above sixty-one per
/// cent and his current target is more than ten metres away. This class called three every twenty-five
/// seconds from the first hit until it died, so neither half of the mechanic — push him down and they
/// stop, close on him and he fights instead — existed.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CelestiusAiTests
{
	private const int TalocsHollow = 300190000;
	private const int Celestius = 215488;
	private const int Summon = 281514;

	/// <summary>The three spawn points, which are also where the three walker routes begin.</summary>
	private const float BossX = 548f;
	private const float BossY = 811f;
	private const float BossZ = 1375f;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TalocsHollow).WithWorldSize(2048).WithWalkerRoutes()
			.WithAi(typeof(CelestiusAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	private static int Summons(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Summon);

	/// <summary>Engages him with a player at <paramref name="metres"/> and drops him to <paramref name="hp"/>.</summary>
	private static BossAiHarness Engaged(int hp, float metres)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Celestius, BossX, BossY, BossZ);
		Player player = harness.SpawnPlayer(BossX + metres, BossY, BossZ);
		// Exact, not approximate: the guard's edge is at sixty-one and SetHpPercent lands near
		// a percentage rather than on it, which is enough to move a boundary pin off the boundary.
		BossAiHarness.SetExactPercent(boss, hp);
		harness.Engage(boss, player);
		boss.SetTarget(player);
		// The wave timer is armed from HandleAttack, which Engage alone does not raise.
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		return harness;
	}

	/// <summary>
	/// <b>The first wave lands at six seconds, not one.</b>
	/// </summary>
	[Fact]
	public void TheFirstWaveLandsAtSixSeconds()
	{
		using BossAiHarness harness = Engaged(hp: 90, metres: 30f);

		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.Equal(0, Summons(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(3, Summons(harness));
	}

	/// <summary>
	/// <b>And there are never more than three on the floor.</b>
	/// </summary>
	/// <remarks>
	/// A summon lives thirty seconds against a twenty-five second cycle, so without retail's despawn of
	/// the previous wave the counts overlap: six stood for five seconds of every cycle. Sampled just
	/// after the second wave lands, which is exactly where the old ones would still be.
	/// </remarks>
	[Fact]
	public void AndThereAreNeverMoreThanThreeOnTheFloor()
	{
		using BossAiHarness harness = Engaged(hp: 90, metres: 30f);

		harness.Clock.Advance(TimeSpan.FromSeconds(32));

		Assert.Equal(3, Summons(harness));
	}

	/// <summary>
	/// <b>Below sixty-one per cent he stops calling them.</b>
	/// </summary>
	/// <remarks>
	/// Retail's guard is <c>is_hp_in_boundary larger_than=61</c>. Counted as they arrive, because a
	/// summon lives thirty seconds and a two-minute window would be empty either way.
	/// </remarks>
	[Fact]
	public void BelowSixtyOnePerCentHeStopsCallingThem()
	{
		using BossAiHarness harness = Engaged(hp: 55, metres: 30f);

		Assert.Equal(0, harness.WatchNew(120, null, Summon).Total);
	}

	/// <summary>
	/// <b>And at sixty-two he still does.</b> The floor is a number, not "wounded".
	/// </summary>
	/// <remarks>
	/// Without this the guard's <i>value</i> is unpinned: a floor set anywhere above sixty-two would
	/// satisfy the pin above just as well.
	/// </remarks>
	[Fact]
	public void AndAtSixtyTwoHeStillDoes()
	{
		using BossAiHarness harness = Engaged(hp: 62, metres: 30f);

		harness.Clock.Advance(TimeSpan.FromSeconds(7));

		Assert.Equal(3, Summons(harness));
	}

	/// <summary>
	/// <b>A raid standing on him gets no summons either.</b>
	/// </summary>
	/// <remarks>
	/// Retail's other guard is <c>is_distance_longer_than distance=10</c>: at close range the rung below
	/// it casts instead. This is the half of the mechanic a melee group would feel.
	/// </remarks>
	[Fact]
	public void ARaidStandingOnHimGetsNoSummonsEither()
	{
		using BossAiHarness harness = Engaged(hp: 90, metres: 5f);

		Assert.Equal(0, harness.WatchNew(120, null, Summon).Total);
	}

	/// <summary>
	/// <b>And one at eleven metres does.</b> The distance is ten, not "in melee".
	/// </summary>
	[Fact]
	public void AndOneAtElevenMetresDoes()
	{
		using BossAiHarness harness = Engaged(hp: 90, metres: 11f);

		harness.Clock.Advance(TimeSpan.FromSeconds(7));

		Assert.Equal(3, Summons(harness));
	}

	/// <summary>
	/// <b>And a wave every twenty-five seconds, not more often.</b>
	/// </summary>
	/// <remarks>
	/// Counted as they arrive rather than as they stand, and that is the whole reason this pin exists:
	/// with the previous wave despawned there are always exactly three on the floor, so <b>halving the
	/// cycle is invisible to every count-based pin above</b>. Thirty seconds holds one wave at retail's
	/// rate and three at twice it.
	/// </remarks>
	[Fact]
	public void AndAWaveEveryTwentyFiveSeconds()
	{
		using BossAiHarness harness = Engaged(hp: 90, metres: 30f);

		// Waves land at 6s and 31s. One window cannot pin the period: thirty seconds holds three at
		// retail's rate and nine at twice it, but also three at half it, because the second wave falls
		// outside either way. The second window is what separates 25 from 50.
		Assert.Equal(3, harness.WatchNew(30, null, Summon).Total);
		Assert.Equal(3, harness.WatchNew(15, null, Summon).Total);
	}
}
