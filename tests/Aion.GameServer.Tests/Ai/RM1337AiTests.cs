using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// RM-1337, whose fire changes shape at half health and did not.
/// </summary>
/// <remarks>
/// Retail places four sparks at five metres and five at fifteen above half health, and below half eight
/// at five, ten at fifteen, and five more on one random attacker — so crossing half turns nine into
/// twenty-three and puts some of them under a player. This class rolled eight to twelve at one spread,
/// whatever his health.
/// <para>
/// Found by <c>audit_timer_drift.py</c>, which reported 0/23000 against a pattern that has neither.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class RM1337AiTests
{
	private const int ArenaOfDiscipline = 300110000;

	private const int Rm1337 = 217593;
	private const int Spark = 282373;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(ArenaOfDiscipline).WithWorldSize(2048)
			.WithAi(typeof(RM1337AI), typeof(SparkOfDarknessAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	private static int Sparks(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Spark);

	private static (Npc Boss, Player Player) Engaged(BossAiHarness harness)
	{
		Npc boss = harness.Spawn(Rm1337, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(304f, 300f, 200f);
		harness.Engage(boss, player);
		return (boss, player);
	}

	/// <summary>
	/// <b>No fire for thirty seconds.</b> This class dropped the first ring the instant he engaged.
	/// </summary>
	[Fact]
	public void TheFireWaitsThirtySeconds()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(28));

		Assert.Equal(0, Sparks(harness));
	}

	/// <summary>
	/// <b>Above half health it is nine sparks: four near and five far.</b>
	/// </summary>
	/// <remarks>
	/// The fire lands four seconds after the rung fires, and each spark lives five, so a reading at
	/// thirty-six seconds catches the whole set standing.
	/// </remarks>
	[Fact]
	public void AboveHalfHealthNineSparksLand()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(36));

		Assert.Equal(9, Sparks(harness));
	}

	/// <summary>
	/// <b>And below half it is twenty-three.</b>
	/// </summary>
	/// <remarks>
	/// Eight near, ten far, and five on one random attacker — the third drop exists only in this band,
	/// which is what makes crossing half a change in kind rather than degree.
	/// </remarks>
	[Fact]
	public void BelowHalfHealthTwentyThreeSparksLand()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player player) = Engaged(harness);

		BossAiHarness.SetHpPercent(boss, 40);
		BossAiHarness.Rehate(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(36));

		Assert.Equal(23, Sparks(harness));
	}

	/// <summary>
	/// <b>The wounded band's third drop lands on a player.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>spawn_on_multi_target</c> puts it on the attacker rather than around the boss, so at
	/// least five of the twenty-three stand where somebody is.
	/// </remarks>
	[Fact]
	public void SomeOfTheWoundedFireLandsOnAPlayer()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player player) = Engaged(harness);

		BossAiHarness.SetHpPercent(boss, 40);
		BossAiHarness.Rehate(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(36));

		int onPlayer = harness.LiveNpcs().Count(n => n.GetNpcId() == Spark
			&& Math.Abs(n.GetX() - player.GetX()) < 0.5f
			&& Math.Abs(n.GetY() - player.GetY()) < 0.5f);
		Assert.Equal(5, onPlayer);
	}

	/// <summary>
	/// <b>The fire comes back sooner when he is wounded, not later.</b>
	/// </summary>
	/// <remarks>
	/// Retail re-arms the rung at sixty seconds above half and fifty below. This class used a flat sixty,
	/// so the wounded half of the fight was no more dangerous than the first.
	/// </remarks>
	[Fact]
	public void TheWoundedBandBringsFireSooner()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player player) = Engaged(harness);

		// Wounded before the first rung fires, not after: the re-arm uses whichever band matches at the
		// moment the rung runs, so wounding him afterwards leaves the sixty-second delay already chosen.
		BossAiHarness.SetHpPercent(boss, 40);
		BossAiHarness.Rehate(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(36));

		// Fifty-second rung: the next set lands at 30+50+4 = 84. A sixty-second rung would put it at 94.
		BossAiHarness.Watched seen = harness.WatchNew(
			52, () => BossAiHarness.SetHpPercent(boss, 40), Spark);

		Assert.True(seen.Total > 0, "no second set of fire inside the fifty-second rung");
	}
}
