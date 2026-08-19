using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Brigade General Chantra, whose area attack ran once every forty seconds.
/// </summary>
/// <remarks>
/// Retail arms his area rung four seconds into the fight and re-arms it every seven. This class opened
/// at five and repeated every forty, so a raid saw the ring about six times less often than it should.
/// Each firing places a ring and a drana at one fixed point for four seconds, and the ring itself puts
/// down the hazard three seconds later.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class BrigadeGeneralChantraAiTests
{
	private const int TiamatStronghold = 300510000;

	private const int Chantra = 219353;

	/// <summary>The two rings, the drana beside them, and the hazards they leave.</summary>
	private const int RingA = 283092;
	private const int RingB = 283094;
	private const int DranaFx = 283173;
	private const int AfterRingA = 283171;
	private const int AfterRingB = 283172;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(BrigadeGeneralChantraAI), typeof(ChantraAreaRingAI), typeof(ChantraRingsAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static int Rings(BossAiHarness harness) =>
		Count(harness, RingA) + Count(harness, RingB);

	private static Npc Engaged(BossAiHarness harness)
	{
		Npc chantra = harness.Spawn(Chantra, 1031f, 470f, 445.45f);
		Player player = harness.SpawnPlayer(1035f, 470f, 445.45f);
		harness.Engage(chantra, player);
		return chantra;
	}

	/// <summary>
	/// <b>The first ring is four seconds in, not five.</b>
	/// </summary>
	[Fact]
	public void TheFirstRingArrivesAtFourSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Equal(0, Rings(harness));

		// Inside the second that separates retail's four from the five this class had: a ring placed at
		// five is not here yet. Advancing the full two seconds passes either way, which is how the old
		// number survived the first mutation sweep.
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1500));
		Assert.Equal(1, Rings(harness));
	}

	/// <summary>
	/// <b>Each ring leaves its own hazard.</b> A leaves 283171 and B leaves 283172.
	/// </summary>
	/// <remarks>
	/// Driven by placing each ring directly rather than by waiting for Chantra to roll one. His roll is
	/// 36/64, so a pin that waits sees whichever came up — rebinding the A ring away from its handler
	/// survived the first mutation sweep because the B ring answered on that run.
	/// </remarks>
	[Theory]
	[InlineData(RingA, AfterRingA, AfterRingB)]
	[InlineData(RingB, AfterRingB, AfterRingA)]
	public void EachRingLeavesItsOwnHazard(int ring, int hazard, int theOther)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(ring, 1031.1f, 466.38f, 445.45f);

		harness.Clock.Advance(TimeSpan.FromSeconds(4));

		Assert.Equal(1, Count(harness, hazard));
		Assert.Equal(0, Count(harness, theOther));
	}

	/// <summary>
	/// <b>And a drana stands beside it</b>, which nothing in this port had ever placed.
	/// </summary>
	[Fact]
	public void ADranaStandsBesideTheRing()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Equal(1, Count(harness, DranaFx));
	}

	/// <summary>
	/// <b>Both leave after retail's four seconds.</b>
	/// </summary>
	/// <remarks>
	/// The ring used to be deleted by hand five seconds in, and the drana did not exist.
	/// </remarks>
	[Fact]
	public void TheRingAndTheDranaLastFourSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.Equal(1, Rings(harness));
		Assert.Equal(1, Count(harness, DranaFx));

		// Placed at four, so gone by nine.
		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.Equal(0, Rings(harness));
		Assert.Equal(0, Count(harness, DranaFx));
	}

	/// <summary>
	/// <b>The ring leaves its hazard three seconds in, and the hazard lasts four.</b>
	/// </summary>
	/// <remarks>
	/// Retail hangs this off the ring's own idle timer rather than off Chantra, so it lands even if he
	/// dies in between. This class placed it five seconds in and gave it no lifetime at all.
	/// </remarks>
	[Fact]
	public void TheRingLeavesItsHazardThreeSecondsIn()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		// Ring at four seconds; nothing yet at six.
		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(0, Count(harness, AfterRingA) + Count(harness, AfterRingB));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(1, Count(harness, AfterRingA) + Count(harness, AfterRingB));

		// Placed at seven with four seconds of life.
		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.Equal(0, Count(harness, AfterRingA) + Count(harness, AfterRingB));
	}

	/// <summary>
	/// <b>And it comes back every seven seconds.</b>
	/// </summary>
	/// <remarks>
	/// This is the correction that changes the fight: at forty seconds the ring was a curiosity, at seven
	/// it is the mechanic. Counted by arrivals, because each ring clears itself after four seconds.
	/// </remarks>
	[Fact]
	public void TheRingReturnsEverySevenSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		// Four seconds to the first, then one every seven: four inside the first half minute, at 4, 11,
		// 18 and 25. The fifth would be at 32.
		BossAiHarness.Watched seen = harness.WatchNew(30, null, RingA, RingB);

		Assert.Equal(4, seen.Total);
	}

	/// <summary>
	/// <b>Below fifteen per cent the rings stop.</b>
	/// </summary>
	/// <remarks>
	/// Retail guards both area rungs with <c>is_hp_in_boundary larger_than=15</c>. This class had no
	/// health guard on them at all.
	/// </remarks>
	[Fact]
	public void BelowFifteenPerCentTheRingsStop()
	{
		using BossAiHarness harness = NewHarness();
		Npc chantra = Engaged(harness);

		BossAiHarness.SetHpPercent(chantra, 12);
		BossAiHarness.Watched seen = harness.WatchNew(30, null, RingA, RingB);

		Assert.Equal(0, seen.Total);
	}

	/// <summary>
	/// <b>But at thirty per cent they keep coming.</b> The floor is fifteen, not "wounded".
	/// </summary>
	[Fact]
	public void AtThirtyPerCentTheRingsKeepComing()
	{
		using BossAiHarness harness = NewHarness();
		Npc chantra = Engaged(harness);

		BossAiHarness.SetHpPercent(chantra, 30);
		BossAiHarness.Watched seen = harness.WatchNew(30, null, RingA, RingB);

		Assert.Equal(4, seen.Total);
	}

	/// <summary>
	/// <b>He enrages at fourteen per cent, not twenty-five.</b>
	/// </summary>
	/// <remarks>
	/// The same rung, and the same correction, as his neighbour Terath.
	/// </remarks>
	[Fact]
	public void TheRageWaitsForFourteenPerCent()
	{
		using BossAiHarness harness = NewHarness();
		Npc chantra = Engaged(harness);

		BossAiHarness.SetHpPercent(chantra, 20);
		chantra.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, chantra);
		Assert.False(chantra.GetEffectController().HasAbnormalEffect(20942),
			"Chantra enraged at twenty per cent, where retail waits for fourteen");

		BossAiHarness.SetHpPercent(chantra, 13);
		chantra.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, chantra);
		Assert.True(chantra.GetEffectController().HasAbnormalEffect(20942),
			"Chantra did not enrage at thirteen per cent");
	}
}
