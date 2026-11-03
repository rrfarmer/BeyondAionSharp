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
	/// The fire lands five seconds after the rung fires, at thirty-five. Upstream 1af948baf added a
	/// single follow-up spark eight seconds after each attack, and in both bands one of those is standing
	/// whenever the wave is — so these two pins count sparks as they <i>arrive</i> over a two-second
	/// window around the wave rather than counting what stands, which is the wave's own shape and
	/// nothing else. A standing count would read the follow-up as a tenth spark.
	/// </remarks>
	[Fact]
	public void AboveHalfHealthNineSparksLand()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(34));

		Assert.Equal(9, harness.WatchNew(1, null, Spark).Total);
	}

	/// <summary>
	/// <b>And below half it is twenty-three.</b>
	/// </summary>
	/// <remarks>
	/// Eight near, ten far, and five on one random attacker — the third drop exists only in this band,
	/// which is what makes crossing half a change in kind rather than degree.
	/// <para>
	/// Counted as they arrive, for the reason given on the band above. The count is twenty-four, not
	/// twenty-three: upstream 1af948baf casts 19550 at the wave in this band, and that skill drops a
	/// spark of its own. The eight-ten-five shape is what the arithmetic below names; the sibling pin
	/// holds the third drop on a player.
	/// </para>
	/// </remarks>
	[Fact]
	public void BelowHalfHealthTwentyThreeSparksLand()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player player) = Engaged(harness);

		BossAiHarness.SetHpPercent(boss, 40);
		BossAiHarness.Rehate(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(34));

		Assert.Equal(8 + 10 + 5 + 1, harness.WatchNew(1, null, Spark).Total);
	}

	/// <summary>
	/// <b>The wounded band's third drop lands on a player.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>spawn_on_multi_target</c> puts it on the attacker rather than around the boss, so at
	/// least five of the twenty-three stand where somebody is.
	/// <para>
	/// At least, not exactly: the wave also scatters sparks within five metres of the boss and the player stands four
	/// away, so one of those can land on the player by chance. Asserting equality here failed once in a full run while
	/// passing in isolation; the claim the pin exists for is the lower bound its own remark states.
	/// </para>
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
		Assert.True(onPlayer >= 5, $"Expected at least the five attacker drops to stand on the player, found {onPlayer}.");
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
