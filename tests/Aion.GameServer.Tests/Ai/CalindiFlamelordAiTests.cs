using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="DarkPoetaCalindiFlamelordAI"/>, translated from retail pattern
/// <c>Dragon_G2</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Tahabata's twin, in the same arena and on the same marks, and she had the same two faults: no
/// rotation, and an enrage that started counting the moment she spawned.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CalindiFlamelordAiTests
{
	private const int DarkPoeta = 300040000;
	private const int Calindi = 215281;
	private const int FlameCenter = 281270;
	private const int WormSpot = 281271;
	private const int DrakanSpot = 281272;
	private const int Worm = 281267;
	private const int Drakan = 281268;

	/// <summary>The enrage she casts when the ten minutes run out.</summary>
	private const int YouAreUnworthy = 19679;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(DarkPoetaCalindiFlamelordAI), typeof(CalindiSummonSpotAI),
				typeof(CalindiDrakanSpotAI), typeof(CalindiSlaveAI), typeof(CalindiDrakanAI),
				typeof(AggressiveNpcAI)).Build();

	/// <summary>Engaged at a chosen health, with the quarry kept out of her aggro range.</summary>
	private static (BossAiHarness, Npc, Player) EngagedAt(int hpPercent)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Calindi, 1180f, 1235f, 143f);
		Player player = harness.SpawnPlayer(1600f, 1600f, 143f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		BossAiHarness.SetHpPercent(boss, hpPercent);
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
	/// Retail arms the A-rank clock in <c>on_enter_attack_state</c>. The aionemu class armed it in
	/// <c>HandleSpawned</c>, so a group that spent four minutes reaching her arrived with six.
	/// </summary>
	[Fact]
	public void TheClockDoesNotStartUntilSheIsEngaged()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		Npc boss = harness.Spawn(Calindi, 1180f, 1235f, 143f);

		harness.Clock.Advance(TimeSpan.FromSeconds(660));

		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == YouAreUnworthy);
		Assert.True(boss.IsSpawned(), "she should not have wiped the room while unengaged");
	}

	/// <summary>Ten minutes from the pull.</summary>
	[Fact]
	public void TheEnrageComesAtTenMinutesFromThePull()
	{
		var (harness, boss, player) = EngagedAt(100);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 560);
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == YouAreUnworthy);

		Advance(harness, boss, player, 60);

		Assert.Contains(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == YouAreUnworthy);
	}

	/// <summary>
	/// Her healthiest band places nothing. Watched every second: each marker lives ten seconds, so a
	/// ring dropped on an eighteen-second step would be gone again before a count at the end.
	/// </summary>
	[Fact]
	public void AboveEightyShePlacesNothing()
	{
		var (harness, boss, player) = EngagedAt(90);
		using BossAiHarness _h = harness;

		int seen = 0;
		for (int i = 0; i < 90; i++)
		{
			Advance(harness, boss, player, 1);
			seen += Count(harness, FlameCenter) + Count(harness, WormSpot) + Count(harness, DrakanSpot);
		}

		Assert.Equal(0, seen);
	}

	/// <summary>The 61-80 handover rings the arena with four flame centers, on the shared marks.</summary>
	[Fact]
	public void TheSecondBandRingsTheArenaWithFlames()
	{
		var (harness, boss, player) = EngagedAt(70);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 10);

		Npc[] flames = harness.LiveNpcs().Where(n => n.GetNpcId() == FlameCenter).ToArray();
		Assert.Equal(4, flames.Length);
		Assert.Equal(4, flames.Select(f => (f.GetX(), f.GetY())).Distinct().Count());
	}

	/// <summary>
	/// The 31-60 band puts four worm spots out and each calls up a worm. The old class spawned worms
	/// directly off a cast, at four coordinates of aionemu's own choosing.
	/// </summary>
	[Fact]
	public void TheThirdBandCallsUpWormsThroughSummonSpots()
	{
		var (harness, boss, player) = EngagedAt(50);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 10);

		Assert.Equal(4, Count(harness, WormSpot));
		Assert.Equal(4, Count(harness, Worm));
	}

	/// <summary>
	/// Below 30 she places <b>two</b> drakan spots, not four — the one place where her table and
	/// Tahabata's genuinely differ rather than merely renaming things.
	/// </summary>
	/// <remarks>
	/// Measured at thirty-five seconds because that is where the branch lands: T0 hands over at six,
	/// T5 at thirteen, and T6 is the step that places them, seventeen seconds later. The spots
	/// themselves are gone ten seconds after that, which is why this cannot be counted at leisure.
	/// </remarks>
	[Fact]
	public void BelowThirtyItIsTwoDrakanSpotsNotFour()
	{
		var (harness, boss, player) = EngagedAt(25);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 35);

		Assert.Equal(2, Count(harness, DrakanSpot));
		Assert.Equal(2, Count(harness, Drakan));

		// And nothing from the bands she has left behind.
		Assert.Equal(0, Count(harness, WormSpot));
		Assert.Equal(0, Count(harness, FlameCenter));
	}

	/// <summary>
	/// The low chain is entered below 30 and guarded at <b>45</b>, so healing her back into the
	/// thirties does not hand the fight back to the worm band — the drakan keep coming.
	/// </summary>
	/// <remarks>
	/// This is the one asymmetry in the table that reads like a typo and is not. Writing all five
	/// steps as <c>HpBelow(30)</c> looks right, matches the entry guard, and quietly ends the fight's
	/// last chain the moment anyone heals her — which is exactly what a mutation to that effect did
	/// without failing anything, until this pin existed.
	/// </remarks>
	[Fact]
	public void HealingHerBackIntoTheThirtiesDoesNotStopTheDrakan()
	{
		var (harness, boss, player) = EngagedAt(25);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 35);
		Assert.Equal(2, Count(harness, DrakanSpot));

		BossAiHarness.SetHpPercent(boss, 40);

		// Let the pair already standing expire first, or watching would count it as a second placement.
		Advance(harness, boss, player, 10);
		Assert.Equal(0, Count(harness, DrakanSpot));

		// A full turn of the chain: T7 at seventeen, T8 at twelve, T5 at seven, T6 at seventeen.
		bool placedAgain = false;
		for (int i = 0; i < 60; i++)
		{
			Advance(harness, boss, player, 1);
			placedAgain |= Count(harness, DrakanSpot) > 0;
		}

		Assert.True(placedAgain, "the low chain is guarded at 45, so it should still be running at 40%");
	}

	/// <summary>
	/// Each call clears its own kind. A fresh ring of worm spots sends the standing worms away and
	/// leaves any drakan alone, which is what stops the two bands' waves from stacking.
	/// </summary>
	/// <remarks>
	/// Both slaves are placed and introduced by hand: the harness has no known-list sweep, so one that
	/// arrived through a spawn is not in her known list and the call cannot reach it.
	/// </remarks>
	[Fact]
	public void EachCallClearsOnlyItsOwnKind()
	{
		var (harness, boss, player) = EngagedAt(50);
		using BossAiHarness _h = harness;
		Npc leftoverWorm = harness.Spawn(Worm, 1183f, 1238f, 143f);
		Npc leftoverDrakan = harness.Spawn(Drakan, 1184f, 1239f, 143f);
		BossAiHarness.MakeMutuallyKnown(boss, leftoverWorm);
		BossAiHarness.MakeMutuallyKnown(boss, leftoverDrakan);

		Advance(harness, boss, player, 10);

		Assert.False(leftoverWorm.IsSpawned(), "the worm call should have sent the worm away");
		Assert.True(leftoverDrakan.IsSpawned(), "the worm call is not the drakan's call");
	}

	/// <summary>Dying takes her markers with her. She leaves no dragon behind — Tahabata does.</summary>
	[Fact]
	public void DyingClearsTheMarkers()
	{
		var (harness, boss, player) = EngagedAt(50);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 10);
		Assert.Equal(4, Count(harness, WormSpot));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, WormSpot));
		Assert.Equal(0, Count(harness, FlameCenter));
	}
}
