using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionZoneIdentityServiceTests
{
	[Fact]
	public void CreateSnapshot_CarriesTownIdAndDominionZoneFlag()
	{
		var snapshot = WorldMapRegionZoneIdentityService.CreateSnapshot(new WorldMapRegionZoneIdentityContext(
			"dominion-zone",
			WorldMapRegionZoneSortClassName.Dominion,
			TownId: 123,
			CreatureObjectIds: [],
			HandlerNames: []));

		Assert.Equal("dominion-zone", snapshot.ZoneId);
		Assert.Equal(123, snapshot.TownId);
		Assert.True(snapshot.IsDominionZone);
		Assert.Contains("ZoneInstance", snapshot.JavaSource);
	}

	[Fact]
	public void CreateSnapshot_NonDominionZoneDoesNotReportDominion()
	{
		var snapshot = WorldMapRegionZoneIdentityService.CreateSnapshot(new WorldMapRegionZoneIdentityContext(
			"sub-zone",
			WorldMapRegionZoneSortClassName.Sub,
			TownId: 0,
			CreatureObjectIds: [],
			HandlerNames: []));

		Assert.False(snapshot.IsDominionZone);
	}

	[Fact]
	public void CreateSnapshot_CarriesCreatureMembershipAndMarksIterationOrderUnstable()
	{
		var snapshot = WorldMapRegionZoneIdentityService.CreateSnapshot(new WorldMapRegionZoneIdentityContext(
			"zone-with-creatures",
			WorldMapRegionZoneSortClassName.Pvp,
			TownId: 0,
			CreatureObjectIds: [3003, 1001, 2002],
			HandlerNames: []));

		Assert.Equal([3003, 1001, 2002], snapshot.CreatureObjectIds);
		Assert.False(snapshot.CreatureIterationOrderIsStable);
	}

	[Fact]
	public void CreateSnapshot_CarriesAttachedHandlerMetadataInAppendOrder()
	{
		var snapshot = WorldMapRegionZoneIdentityService.CreateSnapshot(new WorldMapRegionZoneIdentityContext(
			"zone-with-handlers",
			WorldMapRegionZoneSortClassName.Pvp,
			TownId: 0,
			CreatureObjectIds: [],
			HandlerNames:
			[
				"zone.pvpZones.PvPAreaZone",
				"com.aionemu.gameserver.world.zone.handler.MaterialZoneHandler",
			]));

		Assert.Equal(
		[
			"zone.pvpZones.PvPAreaZone",
			"com.aionemu.gameserver.world.zone.handler.MaterialZoneHandler",
		], snapshot.HandlerNames);
	}
}
