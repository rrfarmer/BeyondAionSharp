using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Petrification Incarnate, who summoned nothing at all until now.
/// </summary>
/// <remarks>
/// He ran <c>aggressive</c>, so the four holy stones his fight is built around never appeared. Found by
/// triaging the nineteen AI classes that no template, no spawn spot and no code reaches: most were
/// superseded by newer classes, and this one was simply dropped.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PetrificationIncarnateAiTests
{
	private const int TiamatStronghold = 400010000;

	private const int PetrificationIncarnate = 259614;

	/// <summary>The same crystal Tiamat's incarnations drop — already in our data, with its own AI.</summary>
	private const int PetrificationCrystal = 282731;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			// The crystal carries tiamats_incarnation_spawn, and the harness validates every AI name it
			// is asked to place -- so leaving it out makes the spawn fail silently and reads as a boss
			// that summons nothing.
			.WithAi(typeof(PetrificationIncarnateAI), typeof(TiamatsIncarnationSpawnsAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(PetrificationIncarnate, 600f, 600f, 300f);
		Player player = harness.SpawnPlayer(603f, 603f, 300f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static int Count(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == PetrificationCrystal);

	/// <summary><b>Four crystals at eighteen seconds</b>, and none before.</summary>
	[Fact]
	public void FourCrystalsArriveAtEighteenSeconds()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(17));
		Assert.Equal(0, Count(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(4, Count(harness));
	}

	/// <summary><b>They leave at thirty-five seconds</b>, which is retail's <c>live_time</c>.</summary>
	[Fact]
	public void TheCrystalsLeaveAtThirtyFiveSeconds()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(19));
		var first = harness.LiveNpcs().Where(n => n.GetNpcId() == PetrificationCrystal).ToHashSet();
		Assert.Equal(4, first.Count);

		harness.Clock.Advance(TimeSpan.FromSeconds(33));
		Assert.All(first, c => Assert.True(c.IsSpawned(), "gone before thirty-five seconds"));

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.All(first, c => Assert.False(c.IsSpawned(), "still standing past thirty-five"));
	}

	/// <summary>
	/// <b>And the clock keeps coming</b>, thirty seconds after the first wave rather than eighteen.
	/// </summary>
	/// <remarks>
	/// Retail arms this clock at eighteen and re-arms it at thirty, so the opening gap and the cadence
	/// are different numbers. Counted as arrivals: the first four are gone by the time the second wave
	/// lands, so a standing count cannot tell a second wave from the first.
	/// </remarks>
	[Fact]
	public void TheCrystalClockReArmsAtThirty()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(19));

		BossAiHarness.Watched later = harness.WatchNew(
			25, () => BossAiHarness.Rehate(boss, player), PetrificationCrystal);
		Assert.Equal(0, later.Total);

		BossAiHarness.Watched second = harness.WatchNew(
			10, () => BossAiHarness.Rehate(boss, player), PetrificationCrystal);
		Assert.Equal(4, second.Total);
	}

	/// <summary><b>And they scatter rather than stacking on him.</b> Retail's forty-metre range.</summary>
	[Fact]
	public void TheCrystalsScatterAroundHim()
	{
		var (harness, boss, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(19));

		var spots = harness.LiveNpcs()
			.Where(n => n.GetNpcId() == PetrificationCrystal)
			.Select(n => (n.GetX(), n.GetY()))
			.Distinct()
			.ToList();

		// Four distinct positions, none of them his own point.
		Assert.Equal(4, spots.Count);
		Assert.DoesNotContain((boss.GetX(), boss.GetY()), spots);
	}
}
