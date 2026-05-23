using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerRideRestrictionServiceTests
{
	[Fact]
	public void ValidateStartRideReadsJavaWorldMapRuntimeRideOption()
	{
		var worldMaps = new[]
		{
			new WorldMapSummary(400010000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Ride),
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 5, Flags: WorldZoneAttributes.Glide),
		};
		var runtimeMaps = new WorldMapRuntimeStateTable(worldMaps);
		Assert.True(runtimeMaps.RemoveWorldOption(400010000, WorldZoneAttributes.Ride));
		Assert.True(runtimeMaps.SetWorldOption(210010000, WorldZoneAttributes.Ride));

		var removedRidePlayer = CreatePlayer(400010000);
		var removedRide = PlayerRideRestrictionService.ValidateStartRide(
			removedRidePlayer,
			enableRideRestriction: true,
			worldMaps,
			runtimeMaps);

		Assert.Equal(PlayerRideRestrictionStatus.InvalidLocation, removedRide.Status);
		Assert.True(removedRide.FoundWorldMap);
		Assert.True(removedRide.UsedRuntimeWorldMap);

		var addedRidePlayer = CreatePlayer(210010000);
		var addedRide = PlayerRideRestrictionService.ValidateStartRide(
			addedRidePlayer,
			enableRideRestriction: true,
			worldMaps,
			runtimeMaps);

		Assert.True(addedRide.CanRide);
		Assert.True(addedRide.FoundWorldMap);
		Assert.True(addedRide.UsedRuntimeWorldMap);
	}

	[Fact]
	public void ValidateStartRideFallsBackToStaticWorldMapAndHonorsDisabledRestriction()
	{
		var worldMaps = new[]
		{
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 5, Flags: WorldZoneAttributes.Glide),
			new WorldMapSummary(400010000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Ride),
		};

		var noRide = PlayerRideRestrictionService.ValidateStartRide(
			CreatePlayer(210010000),
			enableRideRestriction: true,
			worldMaps);
		var ride = PlayerRideRestrictionService.ValidateStartRide(
			CreatePlayer(400010000),
			enableRideRestriction: true,
			worldMaps);
		var disabled = PlayerRideRestrictionService.ValidateStartRide(
			CreatePlayer(210010000),
			enableRideRestriction: false,
			worldMaps);

		Assert.Equal(PlayerRideRestrictionStatus.InvalidLocation, noRide.Status);
		Assert.True(noRide.FoundWorldMap);
		Assert.False(noRide.UsedRuntimeWorldMap);
		Assert.True(ride.CanRide);
		Assert.True(ride.FoundWorldMap);
		Assert.True(disabled.CanRide);
		Assert.False(disabled.FoundWorldMap);
	}

	private static Player CreatePlayer(int worldId)
	{
		return new Player
		{
			ObjectId = worldId / 10,
			Position = new WorldPosition(worldId, 1, 2, 3, 0),
		};
	}
}
