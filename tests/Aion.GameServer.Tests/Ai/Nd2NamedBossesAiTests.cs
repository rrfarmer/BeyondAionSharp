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
/// Three named bosses that had no AI class at all. What they share is a shape worth pinning: summon
/// branches with <b>no health guard</b>, ordered by priority and a flag var each, so they fire as a
/// sequence rather than as a ladder.
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

	/// <summary>
	/// Exedil's two unguarded steps fire in sequence — the second ghost first, then the first — and
	/// each once. Neither carries a health guard; priority and a flag var are the whole ordering.
	/// </summary>
	[Fact]
	public void ExedilSummonsTwoPairsInSequence()
	{
		var (harness, boss, player) = Engaged(Exedil);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 11);
		Assert.Equal(2, Count(harness, GhostPriestTwo));
		Assert.Equal(0, Count(harness, GhostPriestOne));

		Advance(harness, boss, player, 9);
		Assert.Equal(2, Count(harness, GhostPriestOne));
		Assert.Equal(2, Count(harness, GhostPriestTwo));

		// And no more: both flags are spent.
		Advance(harness, boss, player, 40);
		Assert.Equal(2, Count(harness, GhostPriestOne));
		Assert.Equal(2, Count(harness, GhostPriestTwo));
	}

	/// <summary>
	/// Taken below twenty-five before his first heartbeat he calls two permanent ghosts and then
	/// <b>never summons again</b> — that branch is the only one that does not re-arm the timer.
	/// </summary>
	/// <remarks>
	/// Retail's own doing, reproduced rather than tidied: the branch that stops the clock is as much
	/// part of the fight as the ones that keep it running.
	/// </remarks>
	[Fact]
	public void BelowTwentyFiveExedilSummonsOnceAndStops()
	{
		var (harness, boss, player) = Engaged(Exedil);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, boss, player, 11);
		Assert.Equal(2, Count(harness, GhostPriestTwo));

		// A minute later, nothing more — the two sequenced pairs are skipped entirely.
		Advance(harness, boss, player, 60);
		Assert.Equal(2, Count(harness, GhostPriestTwo));
		Assert.Equal(0, Count(harness, GhostPriestOne));
	}

	/// <summary>Ulan calls three at a time, and the two steps differ in how long they stay.</summary>
	[Fact]
	public void UlansTwoStepsDifferInLifetime()
	{
		var (harness, boss, player) = Engaged(Ulan);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 13);
		Assert.Equal(3, Count(harness, GhostWizardTwo));

		Advance(harness, boss, player, 8);
		Assert.Equal(3, Count(harness, GhostWizardOne));

		// The short pair is ten minutes; the long one forty. At eleven minutes only one is left.
		Advance(harness, boss, player, 600);
		Assert.Equal(0, Count(harness, GhostWizardOne));
		Assert.Equal(3, Count(harness, GhostWizardTwo));
	}

	/// <summary>RM-13b calls two on its opening step and three below thirty.</summary>
	[Fact]
	public void Rm13bCallsTwoThenThree()
	{
		var (harness, boss, player) = Engaged(Rm13b);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 6);
		Assert.Equal(2, Count(harness, Pretorian));

		BossAiHarness.SetExactPercent(boss, 25);
		Advance(harness, boss, player, 6);
		Assert.Equal(5, Count(harness, Pretorian));
	}

	/// <summary>Each of its steps fires once, however long the fight sits in the band.</summary>
	[Fact]
	public void Rm13bsStepsFireOnce()
	{
		var (harness, boss, player) = Engaged(Rm13b);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 25);
		Advance(harness, boss, player, 40);

		// Two steps, five pretorians, and their sixty seconds have not run out yet.
		Assert.Equal(5, Count(harness, Pretorian));
	}

	/// <summary>Its pretorians last a minute, which makes them pressure rather than a standing wave.</summary>
	[Fact]
	public void ItsPretoriansLastAMinute()
	{
		var (harness, boss, player) = Engaged(Rm13b);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 6);
		Assert.Equal(2, Count(harness, Pretorian));

		// The opening pair landed on the first heartbeat at five seconds, so their minute is up at
		// sixty-five.
		Advance(harness, boss, player, 55);
		Assert.Equal(2, Count(harness, Pretorian));

		Advance(harness, boss, player, 5);
		Assert.Equal(0, Count(harness, Pretorian));
	}
}
