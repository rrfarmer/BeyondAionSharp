using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerZoneStateServiceTests
{
	[Fact]
	public void RevalidateFlightZonesMatchesJavaWorldMapFlightSlice()
	{
		var worldMaps = new[]
		{
			new WorldMapSummary(400010000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide),
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 5, Flags: WorldZoneAttributes.Glide),
		};
		var flyMapPlayer = new Player
		{
			Position = new WorldPosition(400010000, 0, 0, 0, 0),
			IsInsideNoFlyZone = true,
		};
		var nonFlyMapPlayer = new Player
		{
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
			IsInsideFlyZone = true,
			IsInsideNoFlyZone = true,
		};
		var unknownMapPlayer = new Player
		{
			Position = new WorldPosition(123, 0, 0, 0, 0),
			IsInsideFlyZone = true,
			IsInsideNoFlyZone = true,
		};

		Assert.True(PlayerZoneStateService.RevalidateFlightZones(flyMapPlayer, worldMaps));
		Assert.True(flyMapPlayer.IsInsideFlyZone);
		Assert.False(flyMapPlayer.IsInsideNoFlyZone);

		Assert.True(PlayerZoneStateService.RevalidateFlightZones(nonFlyMapPlayer, worldMaps));
		Assert.False(nonFlyMapPlayer.IsInsideFlyZone);
		Assert.False(nonFlyMapPlayer.IsInsideNoFlyZone);

		Assert.False(PlayerZoneStateService.RevalidateFlightZones(unknownMapPlayer, worldMaps));
		Assert.False(unknownMapPlayer.IsInsideFlyZone);
		Assert.False(unknownMapPlayer.IsInsideNoFlyZone);
	}

	[Fact]
	public void RevalidateFlightZonesMatchesJavaPolygonFlyAndNoFlySlice()
	{
		var worldMaps = new[]
		{
			new WorldMapSummary(210020000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Glide),
			new WorldMapSummary(400020000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Fly | WorldZoneAttributes.Glide),
		};
		var flightZones = new FlightZoneTable(
		[
			new FlightZoneSummary(
				210020000,
				"FLYINGZONESHAPE1_4_210020000",
				FlightZoneType.Fly,
				Flags: -1,
				Bottom: 0,
				Top: 100,
				Points: [new ZonePoint2D(0, 0), new ZonePoint2D(10, 0), new ZonePoint2D(10, 10), new ZonePoint2D(0, 10)]),
			new FlightZoneSummary(
				400020000,
				"GAB1_01_FLYING_ZONE01_400020000",
				FlightZoneType.NoFly,
				Flags: 48,
				Bottom: 0,
				Top: 100,
				Points: [new ZonePoint2D(20, 20), new ZonePoint2D(30, 20), new ZonePoint2D(30, 30), new ZonePoint2D(20, 30)]),
		]);
		var localFlyPlayer = new Player { Position = new WorldPosition(210020000, 5, 5, 50, 0) };
		var abyssNoFlyPlayer = new Player { Position = new WorldPosition(400020000, 25, 25, 50, 0) };
		var aboveZonePlayer = new Player { Position = new WorldPosition(210020000, 5, 5, 150, 0) };

		Assert.True(PlayerZoneStateService.RevalidateFlightZones(localFlyPlayer, worldMaps, flightZones));
		Assert.True(localFlyPlayer.IsInsideFlyZone);
		Assert.False(localFlyPlayer.IsInsideNoFlyZone);

		Assert.True(PlayerZoneStateService.RevalidateFlightZones(abyssNoFlyPlayer, worldMaps, flightZones));
		Assert.True(abyssNoFlyPlayer.IsInsideFlyZone);
		Assert.True(abyssNoFlyPlayer.IsInsideNoFlyZone);

		Assert.True(PlayerZoneStateService.RevalidateFlightZones(aboveZonePlayer, worldMaps, flightZones));
		Assert.False(aboveZonePlayer.IsInsideFlyZone);
		Assert.False(aboveZonePlayer.IsInsideNoFlyZone);
	}
}
