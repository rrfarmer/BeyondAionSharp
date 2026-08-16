using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the corrections made to <see cref="TahabataPyrelordAI"/> against retail pattern
/// <c>Dragon_G1</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The first four pins were written when this class was still aionemu's, with an enrage timer bolted
/// on: they cover when the enrage starts, how long it runs, and the primal dragon he leaves. They pass
/// unchanged against the rebuilt table, which is the point of keeping them.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TahabataPyrelordAiTests
{
	private const int DarkPoeta = 300040000;
	private const int Tahabata = 215280;
	private const int PrimalDragon = 281265;
	private const int FlameCenter = 281261;
	private const int CyclopsSpot = 281262;
	private const int DrakanSpot = 281263;
	private const int FaithfulSubordinate = 281258;
	private const int Drakan = 281259;

	/// <summary>The enrage he casts when the ten minutes run out.</summary>
	private const int YouAreUnworthy = 19679;

	private static (BossAiHarness, Npc, Player) Spawned()
	{
		BossAiHarness harness = BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(TahabataPyrelordAI), typeof(TahabataSummonSpotAI), typeof(TahabataDrakanSpotAI),
				typeof(TahabataGargoyleAI), typeof(NTrapAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Tahabata, 1180f, 1235f, 143f);

		// Well out of his aggro range: he is an aggressive NPC and will pull anyone standing next to
		// him, which is exactly what the idle test needs not to happen.
		Player player = harness.SpawnPlayer(1600f, 1600f, 143f);
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

	/// <summary>
	/// Retail arms the enrage in <c>on_enter_attack_state</c>. It used to start on spawn, so a group
	/// that spent four minutes reaching him arrived with one minute left.
	/// </summary>
	[Fact]
	public void TheEnrageDoesNotStartUntilHeIsEngaged()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;

		// Eleven minutes standing idle: nothing should be counting.
		harness.Clock.Advance(TimeSpan.FromSeconds(660));
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == YouAreUnworthy);
		Assert.True(boss.IsSpawned(), "he should not have wiped the room while unengaged");
	}

	/// <summary>Ten minutes from the pull, not five.</summary>
	[Fact]
	public void TheEnrageComesAtTenMinutesFromThePull()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		Advance(harness, boss, player, 560);
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == YouAreUnworthy);

		Advance(harness, boss, player, 60);
		Assert.Contains(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == YouAreUnworthy);
	}

	/// <summary>
	/// The fuse is lit once, on the first swing. Every later hit arrives through the same handler, and
	/// scheduling is not cancelling — so without a latch each hit would book <i>another</i> enrage,
	/// and the room would be wiped once per swing from the ten-minute mark onwards rather than once.
	/// </summary>
	[Fact]
	public void BeingHitDoesNotPostponeTheEnrage()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		// Hit him steadily for the whole ten minutes.
		for (int i = 0; i < 620; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Equal(1, BossAiHarness.DrainQueuedSkills(boss).Count(c => c.SkillId == YouAreUnworthy));
	}

	/// <summary>
	/// The primal dragon he leaves where he falls is a <c>NTrap_A</c> marker: it appears, lands Final
	/// Blow on whatever is standing round the corpse, and is gone. Nothing is left standing.
	/// </summary>
	/// <remarks>
	/// Both halves matter and both are visible only because the despawn waits for the cast. Had the
	/// trap removed itself in the same breath as casting, a despawned NPC being dropped from the world
	/// map outright would leave no way to tell "he placed a marker that fired and left" from "he placed
	/// nothing at all".
	/// </remarks>
	[Fact]
	public void ThePrimalDragonHeLeavesGoesOffWhereHeFellAndIsGone()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		Assert.Equal(0, Count(harness, PrimalDragon));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Npc dragon = harness.LiveNpcs().First(n => n.GetNpcId() == PrimalDragon);
		Assert.Equal(boss.GetX(), dragon.GetX());
		Assert.Equal(boss.GetY(), dragon.GetY());

		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Equal(0, Count(harness, PrimalDragon));
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Engaged at a chosen health, with the quarry kept out of his aggro range.</summary>
	private static (BossAiHarness, Npc, Player) EngagedAt(int hpPercent)
	{
		var (harness, boss, player) = Spawned();
		BossAiHarness.MakeMutuallyKnown(boss, player);
		BossAiHarness.SetHpPercent(boss, hpPercent);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	/// <summary>
	/// The healthiest band is four chained casts and nothing else — no marker of any kind goes out
	/// above 80%. Retail only starts placing things when T0 hands over to the 61-80 loop.
	/// </summary>
	/// <remarks>
	/// Watched every second rather than counted at the end. Every marker in this fight lives ten
	/// seconds, so a band that put one out on a fifteen-second step would be standing empty again by
	/// the time anything looked — the first version of this pin counted at ninety seconds and could
	/// not tell a band that places nothing from one that places a ring every step.
	/// </remarks>
	[Fact]
	public void AboveEightyHePlacesNothing()
	{
		var (harness, boss, player) = EngagedAt(90);
		using BossAiHarness _h = harness;

		int seen = 0;
		for (int i = 0; i < 90; i++)
		{
			Advance(harness, boss, player, 1);
			seen += Count(harness, FlameCenter) + Count(harness, CyclopsSpot) + Count(harness, DrakanSpot);
		}

		Assert.Equal(0, seen);
	}

	/// <summary>
	/// Below 80 the T0 heartbeat hands over to the second loop, and the branch that does it rings him
	/// with four flame centers. Nothing in the server spawned this NPC before.
	/// </summary>
	/// <remarks>
	/// Watched second by second and measured at its peak. A flame center is a trap: it goes off as soon
	/// as it appears and leaves when the cast lands, so counting at any chosen moment finds an empty
	/// arena unless that moment is the one the ring landed on.
	/// </remarks>
	[Fact]
	public void TheSecondBandRingsHimWithFlames()
	{
		var (harness, boss, player) = EngagedAt(70);
		using BossAiHarness _h = harness;

		int peak = 0;
		var marks = new HashSet<(float, float)>();
		for (int i = 0; i < 15; i++)
		{
			Advance(harness, boss, player, 1);
			Npc[] flames = harness.LiveNpcs().Where(n => n.GetNpcId() == FlameCenter).ToArray();
			peak = Math.Max(peak, flames.Length);
			foreach (Npc flame in flames)
				marks.Add((flame.GetX(), flame.GetY()));
		}

		Assert.Equal(4, peak);
		Assert.Equal(4, marks.Count);
	}

	/// <summary>
	/// The 31-60 band puts four summon spots out instead, and each of those is what calls up a
	/// faithful subordinate. Neither the spot nor this route to the subordinate existed before: the
	/// old class spawned subordinates directly off a cast, at coordinates of aionemu's own choosing.
	/// </summary>
	[Fact]
	public void TheThirdBandCallsUpSubordinatesThroughSummonSpots()
	{
		var (harness, boss, player) = EngagedAt(50);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 15);

		Assert.Equal(4, Count(harness, CyclopsSpot));
		Assert.Equal(4, Count(harness, FaithfulSubordinate));

		// Each subordinate stands on its own spot's mark, which is what makes the wave land on four
		// fixed marks rather than wherever the boss happens to be.
		var spots = harness.LiveNpcs().Where(n => n.GetNpcId() == CyclopsSpot)
			.Select(n => (n.GetX(), n.GetY())).OrderBy(p => p.Item1).ToArray();
		var slaves = harness.LiveNpcs().Where(n => n.GetNpcId() == FaithfulSubordinate)
			.Select(n => (n.GetX(), n.GetY())).OrderBy(p => p.Item1).ToArray();
		Assert.Equal(spots, slaves);
	}

	/// <summary>
	/// Below 30 the marks are the same four and what steps off them is a drakan. It takes longer to
	/// arrive: entry hands over to T5, and the drakan branch is three links along that chain.
	/// </summary>
	[Fact]
	public void BelowThirtyTheSameMarksCallUpDrakan()
	{
		var (harness, boss, player) = EngagedAt(25);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 35);
		Assert.Equal(0, Count(harness, DrakanSpot));

		Advance(harness, boss, player, 15);

		Assert.Equal(4, Count(harness, DrakanSpot));
		Assert.Equal(4, Count(harness, Drakan));

		// The cyclops band is behind him and does not come back.
		Assert.Equal(0, Count(harness, CyclopsSpot));
	}

	/// <summary>
	/// Every fresh ring of spots is preceded by a call that clears whatever the last ring left, which
	/// is what holds the wave at four however long he sits in the band.
	/// </summary>
	/// <remarks>
	/// The subordinate is placed and introduced by hand rather than taken from the first ring: the
	/// harness has no known-list sweep, so a subordinate that arrived through a spawn is not in his
	/// known list and the call cannot reach it. On the live server <c>World.Spawn</c> files it in a
	/// moment after the spawn hook runs.
	/// </remarks>
	[Fact]
	public void AFreshRingClearsTheSubordinatesTheLastOneLeft()
	{
		var (harness, boss, player) = EngagedAt(50);
		using BossAiHarness _h = harness;
		Npc leftover = harness.Spawn(FaithfulSubordinate, 1183f, 1238f, 143f);
		BossAiHarness.MakeMutuallyKnown(boss, leftover);

		Advance(harness, boss, player, 15);

		Assert.False(leftover.IsSpawned(), "the ring call should have sent the leftover away");
	}

	/// <summary>
	/// A summon spot fires its Summon rather than leaving it queued. The queue is drained by the attack
	/// loop and only while the NPC has a target it hates, so a marker that queues its one cast never
	/// fires it — which is what these spots did on the day they shipped.
	/// </summary>
	[Fact]
	public void TheSpotsDoNotLeaveTheirCastSittingInTheQueue()
	{
		var (harness, boss, player) = EngagedAt(50);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 15);

		Npc spot = harness.LiveNpcs().First(n => n.GetNpcId() == CyclopsSpot);

		Assert.Empty(BossAiHarness.DrainQueuedSkills(spot));
	}

	/// <summary>Dying takes the markers with him — retail despawns all three spawn ids.</summary>
	[Fact]
	public void DyingClearsTheMarkers()
	{
		var (harness, boss, player) = EngagedAt(50);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 15);
		Assert.Equal(4, Count(harness, CyclopsSpot));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, CyclopsSpot));
		Assert.Equal(0, Count(harness, FlameCenter));
	}
}
