using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="ExedilAI"/>, <see cref="UlanAI"/> and <see cref="Rm13bAI"/>, translated from
/// retail patterns <c>ND2_PhA</c>, <c>ND2_WhB</c> and <c>ND2_AhD</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Three named bosses that had no AI class at all. What they share is a shape worth pinning: a health
/// ladder walked on a heartbeat, where <b>every summoning rung is banded</b> — <c>is_hp_in_boundary</c>
/// rather than a single threshold — and a lower rung with no guard but the timer keeps the clock alive
/// between bands.
/// <para>
/// An earlier version of these pins asserted the opposite, because the translation under them had read
/// the banded rungs as an unguarded sequence. They are rewritten rather than relaxed: a pin that agrees
/// with the code and disagrees with the pattern is worse than no pin.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class Nd2NamedBossesAiTests
{
	private const int Brusthonin = 220040000;

	private const int Exedil = 212317;
	private const int GhostPriestOne = 280774;
	private const int GhostPriestTwo = 280775;

	private const int Ulan = 212315;
	private const int GhostWizardOne = 280806;
	private const int GhostWizardTwo = 280807;

	private const int Rm13b = 214800;
	private const int Pretorian = 281278;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Brusthonin).WithWorldSize(2048)
			.WithAi(typeof(ExedilAI), typeof(UlanAI), typeof(Rm13bAI), typeof(AggressiveNpcAI), typeof(ServantNpcAI))
			.Build();

	/// <summary>
	/// The player stands well back on purpose. Exedil's ghosts are <c>servant</c> NPCs that cast at
	/// whoever is in reach, and a cast into the harness's stand-in player takes the effect engine
	/// down — a harness limitation rather than anything about these bosses. Out of their reach, the
	/// summoning is observable for as long as a pin needs.
	/// </summary>
	private static (BossAiHarness, Npc, Player) Engaged(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(360f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static void Advance(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	// ---- Exedil ---------------------------------------------------------------------------------

	/// <summary>
	/// <b>At full health he summons nothing, however long the fight runs.</b> Both of his twenty-minute
	/// rungs are banded below eighty, so the opening of the fight is the six-second fallback and
	/// nothing else. This is the pin the previous translation could not have passed.
	/// </summary>
	[Fact]
	public void AboveEightyExedilCallsNobody()
	{
		var (harness, boss, player) = Engaged(Exedil);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, boss, player, 60);

		Assert.Equal(0, Count(harness, GhostPriestOne));
		Assert.Equal(0, Count(harness, GhostPriestTwo));
	}

	/// <summary>
	/// <b>The fallback rung is what makes the ladder reachable at all.</b> Above eighty no band
	/// matches, so the only thing keeping the six-second clock alive is the bottom branch — remove it
	/// and the first heartbeat is the last, and a boss that starts the fight at full health never
	/// summons however far he is taken down.
	/// </summary>
	[Fact]
	public void TheFallbackCarriesTheClockUntilABandMatches()
	{
		var (harness, boss, player) = Engaged(Exedil);
		using BossAiHarness _h = harness;

		// Past the opening heartbeat at ten seconds, with nothing in range of a band.
		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, boss, player, 12);
		Assert.Equal(0, Count(harness, GhostPriestOne));

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, player, 8);
		Assert.Equal(2, Count(harness, GhostPriestOne));
	}

	/// <summary>The 56–80 band calls the <em>first</em> pair — <c>PrSum1</c>, not <c>PrSum2</c>.</summary>
	[Fact]
	public void TheFirstBandCallsPriestOne()
	{
		var (harness, boss, player) = Engaged(Exedil);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, player, 12);

		Assert.Equal(2, Count(harness, GhostPriestOne));
		Assert.Equal(0, Count(harness, GhostPriestTwo));
	}

	/// <summary>
	/// <b>The hand-over.</b> Dropping into 26–55 despawns the first pair as the second arrives, so a
	/// raid never faces both twenty-minute pairs at once. Nothing else in these three fights removes an
	/// add that is still inside its lifetime.
	/// </summary>
	[Fact]
	public void TheSecondBandTakesTheFirstPairAway()
	{
		var (harness, boss, player) = Engaged(Exedil);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, player, 12);
		Assert.Equal(2, Count(harness, GhostPriestOne));

		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, boss, player, 10);

		Assert.Equal(0, Count(harness, GhostPriestOne));
		Assert.Equal(2, Count(harness, GhostPriestTwo));
	}

	/// <summary>
	/// Taken below twenty-five before the ladder has been walked he calls two permanent ghosts and then
	/// <b>never summons again</b> — that rung is the only one that does not re-arm the timer, so both
	/// twenty-minute pairs are skipped.
	/// </summary>
	[Fact]
	public void BelowTwentyFiveExedilSummonsOnceAndStops()
	{
		var (harness, boss, player) = Engaged(Exedil);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, boss, player, 11);
		Assert.Equal(2, Count(harness, GhostPriestTwo));

		// A minute later, nothing more, even though the fight has passed through no other band.
		Advance(harness, boss, player, 60);
		Assert.Equal(2, Count(harness, GhostPriestTwo));
		Assert.Equal(0, Count(harness, GhostPriestOne));
	}

	/// <summary>
	/// The deep pair carries <b>no lifetime</b> where the banded pairs carry twenty minutes — retail
	/// omits <c>live_time</c> on that one spawn only.
	/// </summary>
	[Fact]
	public void TheDeepPairIsPermanent()
	{
		var (harness, boss, player) = Engaged(Exedil);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, boss, player, 11);
		Assert.Equal(2, Count(harness, GhostPriestTwo));

		Advance(harness, boss, player, 1300);
		Assert.Equal(2, Count(harness, GhostPriestTwo));
	}

	// ---- Ulan -----------------------------------------------------------------------------------

	/// <summary>
	/// Ulan's bands run 61–80 then 36–60, and the pair that arrives <b>first</b> is the one that lasts
	/// ten minutes — the replacements last forty. The asymmetry runs the way a port would not guess.
	/// </summary>
	[Fact]
	public void UlanCallsTheShortLivedPairFirst()
	{
		var (harness, boss, player) = Engaged(Ulan);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, player, 14);
		Assert.Equal(3, Count(harness, GhostWizardOne));
		Assert.Equal(0, Count(harness, GhostWizardTwo));

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, boss, player, 9);
		Assert.Equal(0, Count(harness, GhostWizardOne));   // handed over
		Assert.Equal(3, Count(harness, GhostWizardTwo));

		// Forty minutes on the replacements: still standing well past the first pair's ten.
		Advance(harness, boss, player, 900);
		Assert.Equal(3, Count(harness, GhostWizardTwo));
	}

	/// <summary>
	/// <b>His deep rung summons nothing and ends the ladder.</b> Taken under thirty-five early, Ulan
	/// calls <em>no</em> ghosts at all — the opposite of what a hand-written ladder would do, and the
	/// clearest reason his rungs cannot be read as a sequence.
	/// </summary>
	[Fact]
	public void BelowThirtyFiveUlanCallsNothingEverAgain()
	{
		var (harness, boss, player) = Engaged(Ulan);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 30);
		Advance(harness, boss, player, 120);

		Assert.Equal(0, Count(harness, GhostWizardOne));
		Assert.Equal(0, Count(harness, GhostWizardTwo));
	}

	/// <summary>
	/// <b>His deep rung kills the clock, not just its own turn.</b> Once it has fired, healing him
	/// back into a summoning band produces nothing — it is the one rung that does not re-arm timer 0,
	/// so there is no heartbeat left to notice. Stated by walking back up because that is the only way
	/// to tell "the band no longer matches" apart from "the clock is gone".
	/// </summary>
	[Fact]
	public void OnceTheDeepRungHasFiredHealingHimBackChangesNothing()
	{
		var (harness, boss, player) = Engaged(Ulan);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 30);
		Advance(harness, boss, player, 13);

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, player, 60);

		Assert.Equal(0, Count(harness, GhostWizardOne));
		Assert.Equal(0, Count(harness, GhostWizardTwo));
	}

	/// <summary>At full health he calls nobody, as Exedil does.</summary>
	[Fact]
	public void AboveEightyUlanCallsNobody()
	{
		var (harness, boss, player) = Engaged(Ulan);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 95);
		Advance(harness, boss, player, 60);

		Assert.Equal(0, Count(harness, GhostWizardOne));
		Assert.Equal(0, Count(harness, GhostWizardTwo));
	}

	// ---- RM-13b ---------------------------------------------------------------------------------

	/// <summary>
	/// <b>He calls nothing above seventy-five.</b> Both rungs are banded, so the opening of the fight
	/// is the five-second fallback — the earlier translation had him summoning on the first heartbeat
	/// at full health.
	/// </summary>
	[Fact]
	public void AboveSeventyFiveRm13bCallsNobody()
	{
		var (harness, boss, player) = Engaged(Rm13b);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, boss, player, 60);

		Assert.Equal(0, Count(harness, Pretorian));
	}

	/// <summary>Two in the 31–75 band, three more below thirty, and both waves stand together.</summary>
	[Fact]
	public void Rm13bCallsTwoThenThree()
	{
		var (harness, boss, player) = Engaged(Rm13b);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, boss, player, 6);
		Assert.Equal(2, Count(harness, Pretorian));

		BossAiHarness.SetExactPercent(boss, 25);
		Advance(harness, boss, player, 6);
		Assert.Equal(5, Count(harness, Pretorian));
	}

	/// <summary>
	/// <b>A band that is jumped over is lost.</b> Taken straight to twenty-five, only the deep rung
	/// ever matches — 31–75 is behind him and its two pretorians never come. Banded rungs skip where a
	/// threshold ladder would queue up, which is the property all three of these bosses share and the
	/// one an unguarded reading destroys.
	/// </summary>
	[Fact]
	public void ABandJumpedOverIsLost()
	{
		var (harness, boss, player) = Engaged(Rm13b);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 25);
		Advance(harness, boss, player, 40);

		Assert.Equal(3, Count(harness, Pretorian));
	}

	/// <summary>Its pretorians last a minute, which makes them pressure rather than a standing wave.</summary>
	[Fact]
	public void ItsPretoriansLastAMinute()
	{
		var (harness, boss, player) = Engaged(Rm13b);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, boss, player, 6);
		Assert.Equal(2, Count(harness, Pretorian));

		// They landed on the first heartbeat at five seconds, so their minute is up at sixty-five.
		Advance(harness, boss, player, 55);
		Assert.Equal(2, Count(harness, Pretorian));

		Advance(harness, boss, player, 5);
		Assert.Equal(0, Count(harness, Pretorian));
	}
}
