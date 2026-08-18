using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Masto the Ancient, translated from retail pattern <c>ND2_EhA</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// <b>These pins count target switches rather than skills.</b> Thirty-one of the pattern's actions are
/// skill indices and none of them is reachable; what is left is a scatter cadence that changes with
/// health, and a cadence is measured by watching.
/// <para>
/// <b>A random scatter can land on the player it already had</b>, so "the target changed" is a
/// one-in-three coin and not an observation. Every pin here is built on something that is not: an
/// absence of switching over a long window, where the branch under test has no switch at all; a
/// deterministic <c>SECOND_HATING</c> pick; or a count over enough windows that the odds against are
/// stated rather than hoped for. The first draft of this file asserted "it switched" three times and
/// failed three different ways on the first run.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class MastoTheAncientAiTests
{
	private const int Brusthonin = 220050000;

	private const int Masto = 213729;

	/// <summary>Long enough for the six-second opening timer and its scatter to be over.</summary>
	private const int Settle = 15;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Brusthonin).WithWorldSize(2048)
			.WithAi(typeof(MastoTheAncientAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// Masto holding a tank, with two more players on his list at strictly decreasing hate — so
	/// "most-hated", "second-most-hated" and "third" are three different people and stay that way.
	/// </summary>
	private static (BossAiHarness, Npc, Player[]) Raid(int percent)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Masto, 300f, 300f, 200f);

		Player tank = harness.SpawnPlayer(303f, 300f, 200f, race: Race.ELYOS);
		Player offTank = harness.SpawnPlayer(304f, 300f, 200f, race: Race.ELYOS);
		Player third = harness.SpawnPlayer(305f, 300f, 200f, race: Race.ELYOS);

		BossAiHarness.SetExactPercent(boss, percent);
		harness.Engage(boss, tank);

		// Hate set directly rather than through Rehate, which raises an attack event of its own and
		// would keep reshuffling the order these pins depend on.
		BossAiHarness.MakeMutuallyKnown(boss, offTank);
		BossAiHarness.MakeMutuallyKnown(boss, third);
		boss.GetAggroList().AddHate(offTank, 500);
		boss.GetAggroList().AddHate(third, 100);
		boss.SetTarget(tank);

		return (harness, boss, [tank, offTank, third]);
	}

	/// <summary>Counts the seconds on which the boss's target differs from the second before.</summary>
	private static int SwitchesOver(BossAiHarness harness, Npc boss, int seconds)
	{
		VisibleObject? last = boss.GetTarget();
		int switches = 0;
		harness.Watch(seconds, () =>
		{
			VisibleObject? now = boss.GetTarget();
			if (!ReferenceEquals(now, last))
			{
				switches++;
				last = now;
			}
		});
		return switches;
	}

	/// <summary>
	/// <b>Above eighty he settles.</b> The opening timer throws his target away six seconds in, and
	/// after that the top band's only branch is a skill on a fifteen-second timer with no switch
	/// attached — so a tank holds him for as long as the raid keeps him there.
	/// </summary>
	/// <remarks>
	/// Asserted as an absence over two minutes, which is deterministic: the band has no switching
	/// branch, so any switch at all is a failure. Asserting the *opening* scatter instead would have
	/// been a one-in-three coin — see the class remarks.
	/// </remarks>
	[Fact]
	public void AboveEightyHeSettles()
	{
		var (harness, boss, raid) = Raid(90);
		using BossAiHarness _h = harness;

		harness.Watch(Settle, null);

		Assert.Equal(0, SwitchesOver(harness, boss, 120));
	}

	/// <summary>
	/// <b>From eighty down he will not be held.</b> Every band below the top has two branches that
	/// scatter — the opener when the band is entered, and a repeat on the band's own timer.
	/// </summary>
	/// <remarks>
	/// Five minutes is about a dozen scatters in the slowest of these bands, and each has two chances
	/// in three of visibly moving him. Three observed changes out of that is not a close-run thing; the
	/// window is long because the alternative is a pin that fails one run in fifty.
	/// </remarks>
	[Theory]
	[InlineData(70)]
	[InlineData(50)]
	[InlineData(30)]
	public void BelowEightyHeKeepsThrowingHisTargetAway(int percent)
	{
		var (harness, boss, raid) = Raid(percent);
		using BossAiHarness _h = harness;

		Assert.True(SwitchesOver(harness, boss, 300) >= 3,
			"a band below the top went five minutes without scattering");
	}

	/// <summary>
	/// <b>Below twenty he stops scattering and holds the off-tank.</b> The bottom band's opener names
	/// <c>ATTACKERI_SECOND_HATING</c> rather than a random attacker, and the thirty-second timer that
	/// carries the band afterwards has no switch at all.
	/// </summary>
	/// <remarks>
	/// <b>What ends the scattering is the band's flag, not the missing timer re-arm.</b> The bottom
	/// opener is the only one that does not re-arm the opener timer, which reads like the mechanism —
	/// and a mutation putting the re-arm back changes nothing here, correctly, because that timer's
	/// fallback branch has no switch either. Recorded in the AI class rather than left as an
	/// uncaught mutation.
	/// </remarks>
	[Fact]
	public void BelowTwentyHeTurnsOnTheOffTankAndStaysThere()
	{
		var (harness, boss, raid) = Raid(15);
		using BossAiHarness _h = harness;

		harness.Watch(Settle, null);
		Assert.Same(raid[1], boss.GetTarget());

		// Long enough for the bottom band's own timer to come round three times.
		Assert.Equal(0, SwitchesOver(harness, boss, 120));
		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary>
	/// <b>And it is the off-tank every time, not a lucky pick.</b> Twelve fights, twelve identical
	/// outcomes — which a random switch over three players would manage about once in three hundred
	/// thousand tries.
	/// </summary>
	[Fact]
	public void AndItIsTheOffTankEveryTime()
	{
		for (int attempt = 0; attempt < 12; attempt++)
		{
			var (harness, boss, raid) = Raid(15);
			using BossAiHarness _h = harness;

			harness.Watch(Settle, null);

			Assert.Same(raid[1], boss.GetTarget());
		}
	}

	/// <summary>
	/// <b>Every band has its own flag, so a fight that crosses three of them opens three times.</b> One
	/// shared "have I announced" would let the first band spend it for all of them, and the boss would
	/// go quiet after eighty for the rest of the fight.
	/// </summary>
	/// <remarks>
	/// <b>Two things are measured, because the middle bands and the bottom one fail differently.</b>
	/// A middle band that never opens never arms its own repeat timer either, so the scattering simply
	/// stops — that is the first assertion, after crossing down from eighty into the band below. The
	/// bottom band's failure is visible in the target itself, since its pick is <c>SECOND_HATING</c>.
	/// <para>
	/// Asserting a scatter *per band* would have been three coin flips in a row; asserting the target
	/// alone missed a shared flag between two middle bands entirely, which is how this pin ended up
	/// with two halves.
	/// </para>
	/// </remarks>
	[Fact]
	public void EveryBandHasItsOwnFlag()
	{
		var (harness, boss, raid) = Raid(70);
		using BossAiHarness _h = harness;

		harness.Watch(Settle, null);

		BossAiHarness.SetExactPercent(boss, 50);
		Assert.True(SwitchesOver(harness, boss, 300) >= 3,
			"the band below eighty spent the next band's flag on the way past");

		BossAiHarness.SetExactPercent(boss, 30);
		harness.Watch(Settle, null);

		BossAiHarness.SetExactPercent(boss, 15);
		harness.Watch(Settle, null);

		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary>
	/// <b>The opening scatter is real, and this is how to see a coin flip.</b> Six seconds into any
	/// fight he throws his target away once — a pick among three players, so a single fight proves
	/// nothing. Across twelve fights at a health with no band to claim him, landing on the tank every
	/// single time is a one-in-half-a-million coincidence.
	/// </summary>
	[Fact]
	public void TheOpeningScatterIsReal()
	{
		bool alwaysTheTank = true;
		for (int attempt = 0; attempt < 12 && alwaysTheTank; attempt++)
		{
			var (harness, boss, raid) = Raid(20);
			using BossAiHarness _h = harness;

			harness.Watch(Settle, null);
			alwaysTheTank = ReferenceEquals(raid[0], boss.GetTarget());
		}

		Assert.False(alwaysTheTank, "he never once let go of the tank");
	}

	/// <summary>
	/// <b>And exactly twenty belongs to no band</b> — the bottom guard is <c>lower_than 20</c> and the
	/// one above it <c>larger_than 21</c>. Third boss in three entries to carry that hole, and kept
	/// every time.
	/// </summary>
	/// <remarks>
	/// <b>What twenty looks like is the top band, not the bottom one:</b> the opening scatter lands and
	/// then nothing does, because no band claims him. The absence of switching alone would not tell the
	/// two apart — the bottom band also ends quiet — so this pin turns on the other half: at twenty the
	/// target is whatever the opening scatter happened to pick, and over twelve fights it is not always
	/// the off-tank. Against the pin above, that is the whole content of the hole.
	/// </remarks>
	[Fact]
	public void AndExactlyTwentyBelongsToNoBand()
	{
		var (harness, boss, raid) = Raid(20);
		using BossAiHarness _h = harness;

		harness.Watch(Settle, null);
		Assert.Equal(0, SwitchesOver(harness, boss, 120));

		bool alwaysTheOffTank = true;
		for (int attempt = 0; attempt < 12 && alwaysTheOffTank; attempt++)
		{
			var (each, boss2, raid2) = Raid(20);
			using BossAiHarness _e = each;

			each.Watch(Settle, null);
			alwaysTheOffTank = ReferenceEquals(raid2[1], boss2.GetTarget());
		}

		Assert.False(alwaysTheOffTank, "twenty was claimed by the bottom band");
	}
}
