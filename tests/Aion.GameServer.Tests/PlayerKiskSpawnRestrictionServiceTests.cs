using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskSpawnRestrictionServiceTests
{
	[Fact]
	public void ValidateSpawnMatchesJavaToyPetSpawnGuardOrderAndRuntimeBindOption()
	{
		var worldMaps = new[]
		{
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 5, Flags: WorldZoneAttributes.Bind),
			new WorldMapSummary(210020000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Glide),
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 0, Flags: WorldZoneAttributes.Bind),
		};
		var runtimeMaps = new WorldMapRuntimeStateTable(worldMaps);
		Assert.True(runtimeMaps.RemoveWorldOption(210010000, WorldZoneAttributes.Bind));
		Assert.True(runtimeMaps.SetWorldOption(210020000, WorldZoneAttributes.Bind));

		var flyingPlayer = CreatePlayer(300030000);
		flyingPlayer.SetFlyState(PlayerFlyState.Flying);
		var flying = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			flyingPlayer,
			enableKiskRestriction: true,
			hasKisk: true,
			worldMaps,
			runtimeMaps);

		Assert.Equal(PlayerKiskSpawnRestrictionStatus.Flying, flying.Status);
		Assert.False(flying.FoundWorldMap);
		Assert.False(flying.UsedRuntimeWorldMap);

		var instance = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			CreatePlayer(300030000),
			enableKiskRestriction: true,
			hasKisk: true,
			worldMaps,
			runtimeMaps);

		Assert.Equal(PlayerKiskSpawnRestrictionStatus.Instance, instance.Status);
		Assert.True(instance.FoundWorldMap);
		Assert.True(instance.UsedRuntimeWorldMap);

		var alreadyInstalled = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			CreatePlayer(210010000),
			enableKiskRestriction: true,
			hasKisk: true,
			worldMaps,
			runtimeMaps);

		Assert.Equal(PlayerKiskSpawnRestrictionStatus.AlreadyInstalled, alreadyInstalled.Status);
		Assert.True(alreadyInstalled.FoundWorldMap);
		Assert.True(alreadyInstalled.UsedRuntimeWorldMap);

		var disabledRestrictionStillChecksLocation = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			CreatePlayer(210010000),
			enableKiskRestriction: false,
			hasKisk: true,
			worldMaps,
			runtimeMaps);

		Assert.Equal(PlayerKiskSpawnRestrictionStatus.InvalidLocation, disabledRestrictionStillChecksLocation.Status);
		Assert.True(disabledRestrictionStillChecksLocation.FoundWorldMap);
		Assert.True(disabledRestrictionStillChecksLocation.UsedRuntimeWorldMap);

		var runtimeAddedBind = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			CreatePlayer(210020000),
			enableKiskRestriction: true,
			hasKisk: false,
			worldMaps,
			runtimeMaps);

		Assert.True(runtimeAddedBind.CanSpawn);
		Assert.True(runtimeAddedBind.FoundWorldMap);
		Assert.True(runtimeAddedBind.UsedRuntimeWorldMap);
	}

	[Fact]
	public void ValidateSpawnFallsBackToStaticWorldMapAndAllowsUnknownMaps()
	{
		var worldMaps = new[]
		{
			new WorldMapSummary(210010000, IsInstance: false, TwinCount: 5, Flags: WorldZoneAttributes.Bind),
			new WorldMapSummary(210020000, IsInstance: false, TwinCount: 1, Flags: WorldZoneAttributes.Glide),
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 0, Flags: WorldZoneAttributes.Bind),
		};

		var staticBind = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			CreatePlayer(210010000),
			enableKiskRestriction: true,
			hasKisk: false,
			worldMaps);
		var staticNoBind = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			CreatePlayer(210020000),
			enableKiskRestriction: true,
			hasKisk: false,
			worldMaps);
		var staticInstance = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			CreatePlayer(300030000),
			enableKiskRestriction: true,
			hasKisk: false,
			worldMaps);
		var unknownMap = PlayerKiskSpawnRestrictionService.ValidateSpawn(
			CreatePlayer(999999999),
			enableKiskRestriction: true,
			hasKisk: false,
			worldMaps);

		Assert.True(staticBind.CanSpawn);
		Assert.True(staticBind.FoundWorldMap);
		Assert.False(staticBind.UsedRuntimeWorldMap);
		Assert.Equal(PlayerKiskSpawnRestrictionStatus.InvalidLocation, staticNoBind.Status);
		Assert.Equal(PlayerKiskSpawnRestrictionStatus.Instance, staticInstance.Status);
		Assert.True(unknownMap.CanSpawn);
		Assert.False(unknownMap.FoundWorldMap);
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
