using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Popuchin's two bomb mechanics, which were one alternating chain instead of two timers.
/// </summary>
/// <remarks>
/// Retail's <c>Station_FlightNM</c> puts the guided bombs on <c>BTIMERI_INDEX_0</c> (opening 7500,
/// repeat 40000, above half health) and the scattered ones on <c>BTIMERI_INDEX_3</c> (opening 2500,
/// repeat 25000, below half health). This class ran one 15500 chain that picked a branch by health, so
/// the guided bombs came twice as often as retail's and the scattered ones at less than half the rate.
/// <para>
/// The salvos are separated by a wind-up of 4500ms that belongs to this port, not retail, so every
/// window below is measured from the timer firing and includes it.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PopuchinAiTests
{
	private const int AturamSkyFortress = 300350000;
	private const int Popuchin = 217373;

	/// <summary>The wind-up this port plays before either salvo.</summary>
	private const int WindUpSeconds = 5;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AturamSkyFortress).WithWorldSize(2048)
			.WithAi(typeof(PopuchinAI), typeof(ShulackGuidedBombAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI)).Build();

	private static int Guided(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == PopuchinAI.GuidedBomb);

	private static int Scattered(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == PopuchinAI.ScatteredBomb);

	/// <summary>
	/// <b>The first pair of guided bombs is out inside eight seconds.</b> It used to take fifteen and a
	/// half before the wind-up even began.
	/// </summary>
	[Fact]
	public void TheFirstGuidedPairIsOutInsideEightSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Popuchin, 300f, 300f, 200f);
		harness.Engage(boss, harness.SpawnPlayer(305f, 300f, 200f));

		harness.Clock.Advance(TimeSpan.FromSeconds(7));
		Assert.Equal(0, Guided(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(WindUpSeconds + 1));
		Assert.Equal(PopuchinAI.GuidedCount, Guided(harness));
	}

	/// <summary>
	/// <b>And the next pair is forty seconds after that, not twenty.</b>
	/// </summary>
	/// <remarks>
	/// Halving the gap on a two-bomb-per-cycle mechanic doubles everything a group has to deal with for
	/// the whole first half of the fight.
	/// </remarks>
	[Fact]
	public void AndTheNextPairIsFortySecondsAfterThat()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Popuchin, 300f, 300f, 200f);
		harness.Engage(boss, harness.SpawnPlayer(305f, 300f, 200f));

		// Counted as they arrive, not as they stand: a guided bomb detonates and is gone
		// within thirteen seconds of waking, so nothing accumulates to count later.
		BossAiHarness.Watched firstCycle = harness.WatchNew(45, null, PopuchinAI.GuidedBomb);
		Assert.Equal(PopuchinAI.GuidedCount, firstCycle.Total);

		BossAiHarness.Watched secondCycle = harness.WatchNew(20, null, PopuchinAI.GuidedBomb);
		Assert.Equal(PopuchinAI.GuidedCount, secondCycle.Total);
	}

	/// <summary>
	/// <b>He throws no guided bombs at all below half health.</b>
	/// </summary>
	/// <remarks>
	/// Retail's rung is <c>is_hp_in_boundary larger_than=50</c> and nothing re-arms that timer once it
	/// fails, so the guided bombs stop for good rather than alternating with the scattered ones.
	/// </remarks>
	[Fact]
	public void HeThrowsNoGuidedBombsBelowHalfHealth()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Popuchin, 300f, 300f, 200f);
		BossAiHarness.SetHpPercent(boss, 40);
		harness.Engage(boss, harness.SpawnPlayer(305f, 300f, 200f));

		// Counted as they arrive: both bomb npcs are long gone by the end of a two-minute
		// window, so counting what still stands passes whether or not he threw any.
		Assert.Equal(0, harness.WatchNew(120, null, PopuchinAI.GuidedBomb).Total);
	}

	/// <summary>
	/// <b>Below half health the scattered salvo lands inside eight seconds.</b>
	/// </summary>
	/// <remarks>
	/// Retail's cycling timer ticks every 2500 the whole time he is healthy, so the moment his health
	/// crosses the line the rung matches on the next tick. The old chain made a player wait out whatever
	/// was left of a twenty-second cycle.
	/// </remarks>
	[Fact]
	public void BelowHalfHealthTheScatteredSalvoLandsInsideEightSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Popuchin, 300f, 300f, 200f);
		BossAiHarness.SetHpPercent(boss, 40);
		harness.Engage(boss, harness.SpawnPlayer(305f, 300f, 200f));

		harness.Clock.Advance(TimeSpan.FromSeconds(WindUpSeconds + 3));

		Assert.Equal(PopuchinAI.ScatterCount, Scattered(harness));
	}

	/// <summary>
	/// <b>And repeats every twenty-five seconds.</b>
	/// </summary>
	[Fact]
	public void AndTheScatteredSalvoRepeatsEveryTwentyFiveSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Popuchin, 300f, 300f, 200f);
		BossAiHarness.SetHpPercent(boss, 40);
		harness.Engage(boss, harness.SpawnPlayer(305f, 300f, 200f));

		// First salvo lands at 7s (2.5s timer plus the 4.5s wind-up), the second at 36.5s.
		// A twenty-second repeat would put the second at 31.5s, which is what the middle
		// window is for — two twenty-second windows cannot tell the two apart.
		Assert.Equal(PopuchinAI.ScatterCount, harness.WatchNew(20, null, PopuchinAI.ScatteredBomb).Total);
		Assert.Equal(0, harness.WatchNew(15, null, PopuchinAI.ScatteredBomb).Total);
		Assert.Equal(PopuchinAI.ScatterCount, harness.WatchNew(5, null, PopuchinAI.ScatteredBomb).Total);
	}

	/// <summary>
	/// <b>He throws no scattered bombs above half health.</b>
	/// </summary>
	[Fact]
	public void HeThrowsNoScatteredBombsAboveHalfHealth()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Popuchin, 300f, 300f, 200f);
		harness.Engage(boss, harness.SpawnPlayer(305f, 300f, 200f));

		Assert.Equal(0, harness.WatchNew(120, null, PopuchinAI.ScatteredBomb).Total);
	}

	/// <summary>
	/// <b>The scatter covers the platform, not his feet.</b> Retail's <c>spawn_range</c> is 35.
	/// </summary>
	/// <remarks>
	/// This port used 12, so ten bombs meant to spread across the fight landed in a huddle around him —
	/// which is not a hazard a group has to move for. Pinned on the furthest bomb rather than the
	/// constant, so it fails if the range stops reaching the spawns.
	/// </remarks>
	[Fact]
	public void TheScatterCoversThePlatform()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Popuchin, 300f, 300f, 200f);
		BossAiHarness.SetHpPercent(boss, 40);
		harness.Engage(boss, harness.SpawnPlayer(305f, 300f, 200f));
		harness.Clock.Advance(TimeSpan.FromSeconds(WindUpSeconds + 3));

		double furthest = harness.LiveNpcs()
			.Where(n => n.GetNpcId() == PopuchinAI.ScatteredBomb)
			.Max(n => Math.Sqrt(Math.Pow(n.GetX() - boss.GetX(), 2) + Math.Pow(n.GetY() - boss.GetY(), 2)));

		Assert.True(furthest > 12, $"furthest bomb was {furthest:F1} units away, inside the old radius");
	}
}
