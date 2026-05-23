using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class CreaturePvpZoneRevalidationServiceTests
{
	[Fact]
	public void RevalidateUsesStableZoneNameMembershipForEnterAndLeave()
	{
		var zones = new CreaturePvpZoneTable(
		[
			CreateZone("PVP_A_210010000", CreaturePvpZoneType.Pvp, 0, 0, 10, 10, 100),
		]);
		var counterService = new CreaturePvpZoneCounterService();

		var entered = CreaturePvpZoneRevalidationService.Revalidate(
			1001,
			new WorldPosition(210010000, 5, 5, 50, 0),
			zones,
			counterService);
		var duplicateEnter = CreaturePvpZoneRevalidationService.Revalidate(
			1001,
			new WorldPosition(210010000, 5, 5, 50, 0),
			zones,
			counterService);
		var left = CreaturePvpZoneRevalidationService.Revalidate(
			1001,
			new WorldPosition(210010000, 20, 20, 50, 0),
			zones,
			counterService);

		var enterTransition = Assert.Single(entered.Transitions);
		Assert.Equal("PVP_A_210010000", enterTransition.ZoneId);
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.Entered, enterTransition.Status);
		Assert.Equal(1, enterTransition.Counters.PvpZoneCount);
		Assert.Empty(duplicateEnter.Transitions);
		var leaveTransition = Assert.Single(left.Transitions);
		Assert.Equal("PVP_A_210010000", leaveTransition.ZoneId);
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.Left, leaveTransition.Status);
		Assert.Equal(CreaturePvpZoneCounters.Empty, counterService.GetCounters(1001));
	}

	[Fact]
	public void RevalidateTracksNestedPvpAndFortressSiegeZones()
	{
		var zones = new CreaturePvpZoneTable(
		[
			CreateZone("PVP_A_210010000", CreaturePvpZoneType.Pvp, 0, 0, 10, 10, 100),
			CreateZone("PVP_B_210010000", CreaturePvpZoneType.Pvp, 0, 0, 10, 10, 100),
			CreateZone("ABYSS_CASTLE_AREA_2011_210010000", CreaturePvpZoneType.Siege, 0, 0, 10, 10, 100),
		]);
		var counterService = new CreaturePvpZoneCounterService();

		var result = CreaturePvpZoneRevalidationService.Revalidate(
			1001,
			new WorldPosition(210010000, 5, 5, 50, 0),
			zones,
			counterService);
		var counters = counterService.GetCounters(1001);

		Assert.Equal(3, result.Transitions.Count);
		Assert.Equal(2, counters.PvpZoneCount);
		Assert.Equal(1, counters.SiegeZoneCount);
		Assert.True(counters.IsInsidePvpZone);
	}

	[Fact]
	public void RevalidateIgnoresUnavailableInputs()
	{
		var zones = new CreaturePvpZoneTable(
		[
			CreateZone("PVP_A_210010000", CreaturePvpZoneType.Pvp, 0, 0, 10, 10, 100),
		]);
		var counterService = new CreaturePvpZoneCounterService();

		Assert.Empty(CreaturePvpZoneRevalidationService.Revalidate(0, new WorldPosition(210010000, 5, 5, 50, 0), zones, counterService).Transitions);
		Assert.Empty(CreaturePvpZoneRevalidationService.Revalidate(1001, new WorldPosition(210010000, 5, 5, 50, 0), null, counterService).Transitions);
		Assert.Empty(CreaturePvpZoneRevalidationService.Revalidate(1001, new WorldPosition(210010000, 5, 5, 50, 0), zones, null).Transitions);
	}

	private static CreaturePvpZoneSummary CreateZone(
		string name,
		CreaturePvpZoneType zoneType,
		float left,
		float bottom,
		float right,
		float top,
		float verticalTop)
	{
		return new CreaturePvpZoneSummary(
			210010000,
			name,
			zoneType,
			Flags: 0,
			Bottom: 0,
			Top: verticalTop,
			Points:
			[
				new ZonePoint2D(left, bottom),
				new ZonePoint2D(right, bottom),
				new ZonePoint2D(right, top),
				new ZonePoint2D(left, top),
			]);
	}
}
