using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerZoneStateServiceTests
{
	[Fact]
	public void RevalidateFlightZonesFromWorldMapMatchesJavaWorldMapFlightSlice()
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

		Assert.True(PlayerZoneStateService.RevalidateFlightZonesFromWorldMap(flyMapPlayer, worldMaps));
		Assert.True(flyMapPlayer.IsInsideFlyZone);
		Assert.False(flyMapPlayer.IsInsideNoFlyZone);

		Assert.True(PlayerZoneStateService.RevalidateFlightZonesFromWorldMap(nonFlyMapPlayer, worldMaps));
		Assert.False(nonFlyMapPlayer.IsInsideFlyZone);
		Assert.False(nonFlyMapPlayer.IsInsideNoFlyZone);

		Assert.False(PlayerZoneStateService.RevalidateFlightZonesFromWorldMap(unknownMapPlayer, worldMaps));
		Assert.False(unknownMapPlayer.IsInsideFlyZone);
		Assert.False(unknownMapPlayer.IsInsideNoFlyZone);
	}
}
