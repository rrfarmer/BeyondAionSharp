using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Kuhara the Volatile, whose cycle ran at about half retail's rate.
/// </summary>
/// <remarks>
/// Retail alternates his two halves on a fifteen-second beat: barrels at twenty-five seconds, bombs
/// fifteen after that, barrels fifteen after the bombs. This class opened at fifty seconds, waited
/// fourteen for the bombs and eleven before resuming — about seventy-five seconds against retail's
/// forty.
/// <para>
/// Found by <c>audit_timer_drift.py</c>, which reported 8000/14000/50000 against a pattern that has
/// none of them.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KuharaTheVolatileAiTests
{
	private const int RentusBase = 300230000;

	private const int Kuhara = 217311;
	private const int Barrel = 282394;
	private const int Bomb = 282396;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(RentusBase).WithWorldSize(2048)
			.WithAi(typeof(KuharaTheVolatileAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static Npc Engaged(BossAiHarness harness)
	{
		Npc boss = harness.Spawn(Kuhara, 140f, 255f, 209.8f);
		Player player = harness.SpawnPlayer(144f, 255f, 209.8f);
		harness.Engage(boss, player);
		return boss;
	}

	/// <summary>
	/// <b>The first barrels are twenty-five seconds in, not fifty.</b>
	/// </summary>
	[Fact]
	public void TheFirstBarrelsArriveAtTwentyFiveSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(24));
		Assert.Equal(0, Count(harness, Barrel));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(2, Count(harness, Barrel));
	}

	/// <summary>
	/// <b>The bombs follow one beat later, as the barrels expire.</b>
	/// </summary>
	/// <remarks>
	/// The fifteen seconds on a barrel is not an arbitrary lifetime — it is exactly the beat, so retail's
	/// barrels go as the bombs land. At the old fourteen-second gap they expired a second early.
	/// </remarks>
	[Fact]
	public void TheBombsFollowOneBeatLater()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		// Barrels at 25, bombs at 40.
		harness.Clock.Advance(TimeSpan.FromSeconds(39));
		Assert.Equal(0, Count(harness, Bomb));
		Assert.Equal(2, Count(harness, Barrel));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(8, Count(harness, Bomb));
		Assert.Equal(0, Count(harness, Barrel));
	}

	/// <summary>
	/// <b>And the next barrels come one beat after the bombs.</b>
	/// </summary>
	/// <remarks>
	/// Retail arms the barrel timer again from the bomb rung, so the two hand off to each other rather
	/// than each running a clock of its own. This class ran the barrels on a fixed fifty-second rate and
	/// the bombs off each barrel wave, so the cycle drifted from retail's in both halves.
	/// </remarks>
	[Fact]
	public void TheBarrelsReturnOneBeatAfterTheBombs()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		// Barrels 25, bombs 40, barrels again at 55.
		harness.Clock.Advance(TimeSpan.FromSeconds(54));
		Assert.Equal(0, Count(harness, Barrel));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(2, Count(harness, Barrel));
	}

	/// <summary>
	/// <b>Two barrels, and they come from one of the four points.</b>
	/// </summary>
	/// <remarks>
	/// Retail spawns <c>num_to_spawn=2</c> at a single chosen point with <c>spawn_range=3</c>. Asserted
	/// as a pair standing close together rather than by naming a point, because which of the four is a
	/// roll.
	/// </remarks>
	[Fact]
	public void TheBarrelsArriveInPairs()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(26));

		Npc[] barrels = harness.LiveNpcs().Where(n => n.GetNpcId() == Barrel).ToArray();
		Assert.Equal(2, barrels.Length);
		Assert.True(Math.Abs(barrels[0].GetX() - barrels[1].GetX()) < 12f,
			"the two barrels of a wave came from different points");
	}
}
