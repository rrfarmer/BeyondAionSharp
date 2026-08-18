using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Queen Serusia's eggs, translated from retail patterns <c>NeutQueen_N_65_Ah</c>,
/// <c>NeutQueenSumEgg_N_65_e</c> and <c>GhostRun_Sum_As_N_65_Ae</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class QueenSerusiaAiTests
{
	private const int IdianDepths = 210090000;

	private const int Queen = 231003;
	private const int Egg = 284273;
	private const int Larva = 284278;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(IdianDepths).WithWorldSize(2048)
			.WithAi(typeof(QueenSerusiaAI), typeof(SerusiaEggAI), typeof(SerusiaLarvaAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	private static void Strike(Npc target, Creature attacker) =>
		target.GetAi().OnCreatureEvent(AiEventType.Attack, attacker);

	/// <summary>
	/// <b>An egg that lives out its fifteen seconds is a larva.</b> Retail arms the timer in the same
	/// branch that lays the egg, and this is the whole of the mechanic that was missing.
	/// </summary>
	[Fact]
	public void AnEggThatLivesFifteenSecondsIsALarva()
	{
		using BossAiHarness harness = NewHarness();
		Npc queen = harness.Spawn(Queen, 528f, 612f, 559f);
		Player raider = harness.SpawnPlayer(530f, 612f, 559f, race: Race.ASMODIANS);
		harness.Engage(queen, raider);

		BossAiHarness.SetExactPercent(queen, 74);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		Assert.Single(Live(harness, Egg));
		Assert.Empty(Live(harness, Larva));

		harness.Clock.Advance(TimeSpan.FromMilliseconds(15000));

		Assert.Empty(Live(harness, Egg));
		Assert.Single(Live(harness, Larva));
	}

	/// <summary>
	/// <b>An egg killed first is nothing.</b> The other half of the same mechanic, and the reason the
	/// timer matters rather than merely existing.
	/// </summary>
	[Fact]
	public void AnEggKilledFirstIsNothing()
	{
		using BossAiHarness harness = NewHarness();
		Npc queen = harness.Spawn(Queen, 528f, 612f, 559f);
		Player raider = harness.SpawnPlayer(530f, 612f, 559f, race: Race.ASMODIANS);
		harness.Engage(queen, raider);

		BossAiHarness.SetExactPercent(queen, 74);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		foreach (Npc egg in Live(harness, Egg).ToList())
			egg.GetController().Delete();

		harness.Clock.Advance(TimeSpan.FromMilliseconds(15000));

		Assert.Empty(Live(harness, Larva));
	}

	/// <summary>
	/// <b>Retail's three numbers are decoration.</b> One listener answers all three, so whichever call
	/// lands first hatches every egg standing — including the two laid at fifty percent whose own
	/// timer has not run out. A raid that pushes her quickly gets all three at once.
	/// </summary>
	[Fact]
	public void WhicheverCallLandsFirstHatchesEverythingStanding()
	{
		using BossAiHarness harness = NewHarness();
		Npc queen = harness.Spawn(Queen, 528f, 612f, 559f);
		Player raider = harness.SpawnPlayer(530f, 612f, 559f, race: Race.ASMODIANS);
		harness.Engage(queen, raider);

		BossAiHarness.SetExactPercent(queen, 74);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));

		// Five seconds into the first egg's incubation, two more are laid.
		harness.Clock.Advance(TimeSpan.FromMilliseconds(5000));
		BossAiHarness.SetExactPercent(queen, 49);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		Assert.Equal(3, Live(harness, Egg).Count);

		// The 75% call comes due first and takes all three.
		harness.Clock.Advance(TimeSpan.FromMilliseconds(9000));

		Assert.Empty(Live(harness, Egg));
		Assert.Equal(3, Live(harness, Larva).Count);
	}

	/// <summary>
	/// <b>One blow that crosses all three thresholds lays all six eggs at once.</b> That is
	/// <see cref="SummonerAI"/>'s <c>CheckPercentage</c>, which walks every threshold in one pass; a
	/// blow taking her from full to a quarter fires 75%, 50% and 25% together — 1 + 2 + 3.
	/// </summary>
	/// <remarks>
	/// <b>Retail spreads them across three blows instead.</b> Its three branches are separate
	/// priorities in one <c>on_attacked</c> handler, and retail's handlers are first-match-wins, so the
	/// 75% branch answers the first blow alone and its <c>increase_intvar</c> guard then steps aside for
	/// the 50% branch on the next.
	/// <para>
	/// Left as it is, and pinned as it is, because <c>CheckPercentage</c> is aionemu's and fifty-one
	/// npcs share <c>summoner</c>. The difference only shows when one hit crosses more than one
	/// threshold; a fight that descends normally lays 1, 2 and 3 on separate blows either way. Recorded
	/// in docs/retail-ai-fidelity.md rather than fixed here.
	/// </para>
	/// </remarks>
	[Fact]
	public void OneBlowPastEveryThresholdLaysAllSix()
	{
		using BossAiHarness harness = NewHarness();
		Npc queen = harness.Spawn(Queen, 528f, 612f, 559f);
		Player raider = harness.SpawnPlayer(530f, 612f, 559f, race: Race.ASMODIANS);
		harness.Engage(queen, raider);

		BossAiHarness.SetExactPercent(queen, 24);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));

		Assert.Equal(6, Live(harness, Egg).Count);
	}

	/// <summary>
	/// <b>Descending a threshold at a time lays one, then two, then three</b> — retail's counts, and
	/// the shape a real fight takes.
	/// </summary>
	[Fact]
	public void DescendingAThresholdAtATimeLaysOneThenTwoThenThree()
	{
		using BossAiHarness harness = NewHarness();
		Npc queen = harness.Spawn(Queen, 528f, 612f, 559f);
		Player raider = harness.SpawnPlayer(530f, 612f, 559f, race: Race.ASMODIANS);
		harness.Engage(queen, raider);

		BossAiHarness.SetExactPercent(queen, 74);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		Assert.Single(Live(harness, Egg));

		BossAiHarness.SetExactPercent(queen, 49);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		Assert.Equal(3, Live(harness, Egg).Count);

		BossAiHarness.SetExactPercent(queen, 24);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		Assert.Equal(6, Live(harness, Egg).Count);
	}

	/// <summary>
	/// <b>A dead queen takes her eggs with her</b>, before any of them can hatch. That is
	/// <see cref="SummonerAI"/>'s <c>RemoveAndResetHelperSpawns</c>, and it is retail's three
	/// <c>despawn SPAWN_ID_1</c> branches by a different route.
	/// </summary>
	/// <remarks>
	/// The <c>IsDead</c> check inside the scheduled call is belt-and-braces and is deliberately not
	/// claimed as pinned: by the time it could matter the eggs are already gone, so no test can tell
	/// a guarded call from an unguarded one. Ours is a scheduled task where retail's is a battle timer
	/// that stops with the fight, which is why the check is there at all.
	/// </remarks>
	[Fact]
	public void ADeadQueenTakesHerEggsWithHer()
	{
		using BossAiHarness harness = NewHarness();
		Npc queen = harness.Spawn(Queen, 528f, 612f, 559f);
		Player raider = harness.SpawnPlayer(530f, 612f, 559f, race: Race.ASMODIANS);
		harness.Engage(queen, raider);

		BossAiHarness.SetExactPercent(queen, 74);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		Assert.Single(Live(harness, Egg));

		queen.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Empty(Live(harness, Egg));
		harness.Clock.Advance(TimeSpan.FromMilliseconds(15000));
		Assert.Empty(Live(harness, Larva));
	}

	/// <summary>
	/// <b>The fifty-percent call hatches on its own.</b> Every other pin here lets the seventy-five
	/// call arrive first and take everything standing, which hides whether the other two branches are
	/// wired at all — so this one clears the board before the second clutch is laid.
	/// </summary>
	[Fact]
	public void TheFiftyPercentCallHatchesOnItsOwn()
	{
		using BossAiHarness harness = NewHarness();
		Npc queen = harness.Spawn(Queen, 528f, 612f, 559f);
		Player raider = harness.SpawnPlayer(530f, 612f, 559f, race: Race.ASMODIANS);
		harness.Engage(queen, raider);

		// Lay one at 75% and kill it, then let its call go by with nothing to hatch.
		BossAiHarness.SetExactPercent(queen, 74);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		foreach (Npc egg in Live(harness, Egg))
			egg.GetController().Delete();
		harness.Clock.Advance(TimeSpan.FromMilliseconds(15000));
		Assert.Empty(Live(harness, Larva));

		BossAiHarness.SetExactPercent(queen, 49);
		Strike(queen, raider);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));
		Assert.Equal(2, Live(harness, Egg).Count);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(15000));

		Assert.Empty(Live(harness, Egg));
		Assert.Equal(2, Live(harness, Larva).Count);
	}
}
