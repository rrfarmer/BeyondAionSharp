using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="BollvigBlackheartAI"/> and his family, translated from retail patterns
/// <c>ND2_WhD</c>, <c>ND2_Sum_WhD1</c> and <c>ND2_WhDSum</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// A LEGENDARY boss that had no AI class at all. The shape worth pinning is that his bats are not a
/// wave that grows but a wave that <em>changes</em>, and that the vampire loop is bounded by its band
/// at both ends.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class BollvigBlackheartAiTests
{
	private const int Heiron = 210040000;

	private const int Bollvig = 212314;
	private const int Bloodwing = 280802;
	private const int CruelVampire = 280804;
	private const int ViciousBloodwing = 280803;
	private const int Relic = 204655;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(4096)
			.WithAi(typeof(BollvigBlackheartAI), typeof(BollvigBloodwingAI), typeof(BollvigRelicAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// The quarry stands forty-five metres out: his bats and vampires are aggressive, and a player
	/// beside him is one they find without being sent.
	/// </summary>
	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Bollvig, 1000f, 2800f, 236f);
		Player quarry = harness.SpawnPlayer(1045f, 2800f, 236f);
		harness.Engage(boss, quarry);
		return (harness, boss, quarry);
	}

	private static void Advance(BossAiHarness harness, Npc boss, Player quarry, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, quarry);
			BossAiHarness.KeepAlive(quarry);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Above eighty he calls nobody, however long the fight runs.</summary>
	[Fact]
	public void AboveEightyHeCallsNobody()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, boss, quarry, 60);

		Assert.Equal(0, Count(harness, Bloodwing));
	}

	/// <summary>Two bats at 61–80, and two more into the same group at 41–60.</summary>
	[Fact]
	public void EachOfTheTwoUpperBandsCallsTwoBats()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 22);
		Assert.Equal(2, Count(harness, Bloodwing));

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, boss, quarry, 10);
		Assert.Equal(4, Count(harness, Bloodwing));
	}

	/// <summary>
	/// <b>The wave changes rather than grows.</b> Entering 21–40 turns every bat still alive into a
	/// cruel vampire where it stands — four bats become four vampires in one beat.
	/// </summary>
	[Fact]
	public void EnteringTheThirdBandTurnsEveryBatIntoAVampire()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 22);
		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, boss, quarry, 10);
		Assert.Equal(4, Count(harness, Bloodwing));

		BossAiHarness.SetExactPercent(boss, 30);
		Advance(harness, boss, quarry, 10);

		Assert.Equal(0, Count(harness, Bloodwing));
		Assert.Equal(4, Count(harness, CruelVampire));
	}

	/// <summary>
	/// And from then on one more lands on his quarry every thirty-five seconds. Seventeen after the
	/// band opens, then thirty-five apart.
	/// </summary>
	[Fact]
	public void TheVampireLoopKeepsAddingOne()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 30);
		Advance(harness, boss, quarry, 22);
		Assert.Equal(0, Count(harness, CruelVampire));

		Advance(harness, boss, quarry, 18);
		Assert.Equal(1, Count(harness, CruelVampire));

		Advance(harness, boss, quarry, 36);
		Assert.Equal(2, Count(harness, CruelVampire));
	}

	/// <summary>
	/// <b>Below twenty both the ladder and the loop are over.</b> The deep rung arms timer 6 and not
	/// timer 0, and the loop's own branch is guarded on 21–40 — so a raid that pushes him through
	/// gets nothing more, where one that lingers in the band keeps paying.
	/// </summary>
	[Fact]
	public void BelowTwentyNothingMoreArrives()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 15);
		Advance(harness, boss, quarry, 120);

		Assert.Equal(0, Count(harness, Bloodwing));
		Assert.Equal(0, Count(harness, CruelVampire));
	}

	/// <summary>Killing a bloodwing leaves a vicious one for fifteen seconds.</summary>
	[Fact]
	public void AKilledBloodwingLeavesAViciousOne()
	{
		using BossAiHarness harness = NewHarness();
		Npc bat = harness.Spawn(Bloodwing, 1000f, 2800f, 236f);

		bat.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(1, Count(harness, ViciousBloodwing));

		harness.Clock.Advance(TimeSpan.FromSeconds(17));
		Assert.Equal(0, Count(harness, ViciousBloodwing));
	}

	/// <summary>Dying leaves the relic on its mark and takes every add with it.</summary>
	[Fact]
	public void DyingLeavesTheRelicAndClearsTheRoom()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 22);
		Assert.Equal(2, Count(harness, Bloodwing));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, Bloodwing));
		Npc relic = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == Relic));
		Assert.Equal(1001f, relic.GetX(), 1);
		Assert.Equal(2828f, relic.GetY(), 1);
	}

	/// <summary>
	/// <b>And waking clears the last kill's relic.</b> Without it a second pull finds the first
	/// reward still standing.
	/// </summary>
	/// <remarks>
	/// Driven by spawning him rather than by sending the message, because the message is only half of
	/// it — sending it directly passes whether or not his own <c>on_wake_up</c> carries the broadcast,
	/// which survived a mutation sweep until this changed. A boss that has just spawned has an empty
	/// known list, so <c>NpcMessageBus</c> takes its region-scan fallback and the relic hears him.
	/// </remarks>
	[Fact]
	public void WakingClearsTheRelicLeftByTheLastKill()
	{
		using BossAiHarness harness = NewHarness();
		Npc relic = harness.Spawn(Relic, 1001f, 2828f, 235.66f);
		Assert.True(relic.IsSpawned());

		harness.Spawn(Bollvig, 1000f, 2828f, 236f);

		Assert.False(relic.IsSpawned());
	}

	/// <summary>
	/// <b>The fallback is what carries the clock while he is above eighty.</b> No band matches there,
	/// so without the bottom rung the opening heartbeat is the last and he never calls anything
	/// however far he is taken down.
	/// </summary>
	[Fact]
	public void TheFallbackCarriesTheClockUntilABandMatches()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, boss, quarry, 25);
		Assert.Equal(0, Count(harness, Bloodwing));

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 8);
		Assert.Equal(2, Count(harness, Bloodwing));
	}

	/// <summary>
	/// <b>Once the deep rung has fired, healing him back into a band changes nothing.</b> It arms
	/// timer 6 and never timer 0, so there is no heartbeat left to notice — the only way to tell
	/// "the band no longer matches" apart from "the clock is gone".
	/// </summary>
	[Fact]
	public void OnceTheDeepRungHasFiredHealingHimBackChangesNothing()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 15);
		Advance(harness, boss, quarry, 25);

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, quarry, 60);

		Assert.Equal(0, Count(harness, Bloodwing));
	}

	/// <summary>
	/// <b>And the vampire loop is bounded below as well as above.</b> Its timer keeps running once
	/// armed, so the guard on its own branch is the only thing that stops the vampires when he is
	/// pushed under twenty — driven through the band first, which is the only way the loop is live.
	/// </summary>
	[Fact]
	public void TheVampireLoopStopsWhenHeLeavesItsBand()
	{
		var (harness, boss, quarry) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 30);
		Advance(harness, boss, quarry, 40);
		Assert.Equal(1, Count(harness, CruelVampire));

		BossAiHarness.SetExactPercent(boss, 15);
		Advance(harness, boss, quarry, 90);

		Assert.Equal(1, Count(harness, CruelVampire));
	}
}
