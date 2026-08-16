using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="WatchmanHokuruki"/> and <see cref="IDSweepStageAddAI"/>, translated from retail
/// patterns <c>IDSweep_Monster_Nmd03</c>, <c>IDSweep_Monster_02</c>, <c>IDSweep_S1_Monster</c> and
/// <c>IDSweep_S1_Shulack_Gu_01</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The Shugo Emperor's Vault stage-one boss. aionemu had him calling two templates no retail pattern
/// spawns, at nine hand-placed positions; retail has him scattering mosbears around himself and then
/// clearing the whole room when he dies.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class WatchmanHokurukiAiTests
{
	private const int Vault = 301400000;

	private const int Hokuruki = 235634;
	private const int TamedMosbear = 235632;

	// Stage-one room population. aionemu had the boss summoning the last two.
	private const int IntruderSniper = 235649;
	private const int IntruderMarksman = 236083;
	private const int BrainwashedPeon = 235631;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Vault).WithWorldSize(2048)
			.WithAi(typeof(WatchmanHokuruki), typeof(IDSweepStageAddAI), typeof(AggressiveNpcAI))
			.Build();

	/// <summary>
	/// The player stands back: the mosbears are aggressive NPCs with a cast loop of their own, and a
	/// cast into the harness's stand-in player takes the effect engine down. Out of their reach the
	/// summoning is observable for as long as a pin needs.
	/// </summary>
	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Hokuruki, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(360f, 300f, 200f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	/// <summary>
	/// One more swing, which is what advances a first-match-wins ladder by one rung.
	/// </summary>
	/// <remarks>
	/// Deliberately no <c>Rehate</c>: adding hate raises an Attack event of its own, so topping the
	/// hate up here would deliver <em>two</em> swings per call and a rung-counting pin would read half
	/// the ladder it thinks it does. The hate <see cref="BossAiHarness.Engage"/> puts on is enough to
	/// hold a fight open for a test's worth of swings.
	/// </remarks>
	private static void Hit(Npc boss, Player player)
		=> boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// Retail's chain, in retail's order. The three zero-bear rungs are the stage counter we cannot
	/// express, and they sit <b>above</b> both summoning rungs — which is the whole reason they are in
	/// the table rather than dropped.
	/// </summary>
	[Fact]
	public void TheLadderIsRetailsOrder()
	{
		Assert.Equal(
			[(30, 0), (60, 0), (80, 0), (50, 2), (25, 3)],
			WatchmanHokuruki.Rungs());
	}

	/// <summary>Entering the fight scatters four mosbears around him.</summary>
	[Fact]
	public void TheFightOpensWithFourMosbears()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		Assert.Equal(4, Count(harness, TamedMosbear));
	}

	/// <summary>
	/// <b>He never calls a gunner.</b> No retail pattern spawns either template — they are stage one's
	/// room population — so this asserts the absence across a whole fight, every rung included.
	/// </summary>
	[Fact]
	public void HeNeverCallsTheGunners()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		foreach (int percent in new[] { 79, 59, 49, 29, 24, 10 })
		{
			BossAiHarness.SetExactPercent(boss, percent);
			for (int i = 0; i < 4; i++)
				Hit(boss, player);
		}

		Assert.Equal(0, Count(harness, IntruderSniper));
		Assert.Equal(0, Count(harness, IntruderMarksman));
	}

	/// <summary>
	/// <b>The stage-counter rungs cost a hit each.</b> Below fifty with nothing spent, retail spends one
	/// swing on the sixty rung and one on the eighty rung before the bears come on the third — the
	/// consequence of keeping rungs whose action we cannot perform, and the thing that would break if
	/// they were dropped.
	/// </summary>
	[Fact]
	public void TheStageCounterRungsEachCostASwing()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		int opening = Count(harness, TamedMosbear);
		BossAiHarness.SetExactPercent(boss, 49);

		Hit(boss, player);                                        // rung 60
		Assert.Equal(opening, Count(harness, TamedMosbear));

		Hit(boss, player);                                        // rung 80
		Assert.Equal(opening, Count(harness, TamedMosbear));

		Hit(boss, player);                                        // rung 50 -- two bears
		Assert.Equal(opening + 2, Count(harness, TamedMosbear));
	}

	/// <summary>Below twenty-five he calls three, and the fifty rung is still owed its two.</summary>
	[Fact]
	public void BelowTwentyFiveHeCallsThreeMore()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		int opening = Count(harness, TamedMosbear);
		BossAiHarness.SetExactPercent(boss, 24);

		// Rungs 30, 60 and 80 first: all three match at 24 and all three are above the summoning pair.
		for (int i = 0; i < 3; i++)
			Hit(boss, player);
		Assert.Equal(opening, Count(harness, TamedMosbear));

		Hit(boss, player);
		Assert.Equal(opening + 2, Count(harness, TamedMosbear));

		Hit(boss, player);
		Assert.Equal(opening + 5, Count(harness, TamedMosbear));
	}

	/// <summary>Every rung carries a flag var, so the waves stop once both have been paid.</summary>
	[Fact]
	public void EachRungFiresOnlyOnce()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 10);
		for (int i = 0; i < 20; i++)
			Hit(boss, player);

		Assert.Equal(4 + 2 + 3, Count(harness, TamedMosbear));
	}

	/// <summary>
	/// <b>His death clears stage one</b> — the bears he called and the room population he did not,
	/// through one broadcast that eleven templates answer.
	/// </summary>
	[Fact]
	public void HisDeathClearsStageOne()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		Npc sniper = harness.Spawn(IntruderSniper, 320f, 300f, 200f);
		Npc marksman = harness.Spawn(IntruderMarksman, 310f, 305f, 200f);
		Npc peon = harness.Spawn(BrainwashedPeon, 305f, 295f, 200f);
		foreach (Npc add in new[] { sniper, marksman, peon })
			BossAiHarness.MakeMutuallyKnown(boss, add);

		Assert.Equal(4, Count(harness, TamedMosbear));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, TamedMosbear));
		Assert.Equal(0, Count(harness, IntruderSniper));
		Assert.Equal(0, Count(harness, IntruderMarksman));
		Assert.Equal(0, Count(harness, BrainwashedPeon));
	}

	/// <summary>
	/// A hundred metres, which is retail's <c>range_as_meter</c>. Stated as its own pin because the
	/// clear-up is the kind of thing that reads as working when it is really reaching everything.
	/// </summary>
	[Fact]
	public void TheClearReachesAHundredMetresAndNoFurther()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		Npc near = harness.Spawn(BrainwashedPeon, 380f, 300f, 200f);   // 80m
		Npc far = harness.Spawn(BrainwashedPeon, 460f, 300f, 200f);    // 160m
		BossAiHarness.MakeMutuallyKnown(boss, near);
		BossAiHarness.MakeMutuallyKnown(boss, far);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.False(near.IsSpawned(), "the add 80m away should have gone");
		Assert.True(far.IsSpawned(), "the add 160m away should have stayed");
	}

	/// <summary>
	/// Message numbers are chosen per encounter and have no registry, so a listener that answered any
	/// message would clear the room on somebody else's broadcast.
	/// </summary>
	[Fact]
	public void AStageAddIgnoresOtherMessages()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Hokuruki, 300f, 300f, 200f);
		Npc peon = harness.Spawn(BrainwashedPeon, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, peon);

		Aion.GameServer.Ai.NpcMessageBus.Broadcast(boss, WatchmanHokuruki.StageIsOver + 1, null, 100f);
		Assert.True(peon.IsSpawned());

		Aion.GameServer.Ai.NpcMessageBus.Broadcast(boss, WatchmanHokuruki.StageIsOver, null, 100f);
		Assert.False(peon.IsSpawned());
	}

	/// <summary>
	/// A reset replays the fight rather than resuming it: the opening wave comes again and the ladder
	/// starts from the top, which is the convention the whole pattern runtime holds to.
	/// </summary>
	[Fact]
	public void AResetReplaysTheFight()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 49);
		for (int i = 0; i < 3; i++)
			Hit(boss, player);
		Assert.Equal(6, Count(harness, TamedMosbear));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);
		BossAiHarness.SetExactPercent(boss, 100);
		harness.Engage(boss, player);

		Assert.Equal(10, Count(harness, TamedMosbear));
	}
}
