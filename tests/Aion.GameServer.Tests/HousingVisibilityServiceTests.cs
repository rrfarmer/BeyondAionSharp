using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class HousingVisibilityServiceTests
{
	[Fact]
	public void WorldHouse_TryCreate_UsesStaticAddressCoordinates()
	{
		var player = CreatePlayer();
		var house = new PlayerHouse(700001, 2001, 730001, DateTime.UtcNow, null, false, SignNotice: "hello");
		var templates = new HousingTemplateTable(
			[new HousingAddressSummary(2001, 1, 798000, MapId: 710010000, X: 12.5f, Y: 20.25f, Z: 35.75f)],
			[new HousingBuildingSummary(730001, "STUDIO", 0)]);

		var created = WorldHouse.TryCreate(player, house, templates, out var worldHouse);

		Assert.True(created);
		Assert.NotNull(worldHouse);
		Assert.Equal(700001, worldHouse!.ObjectId);
		Assert.Equal(2001, worldHouse.AddressId);
		Assert.Equal(1001, worldHouse.OwnerObjectId);
		Assert.Equal("Owner", worldHouse.OwnerName);
		Assert.Equal(new WorldPosition(710010000, 12.5f, 20.25f, 35.75f, 0), worldHouse.Position);
		Assert.Equal("hello", worldHouse.SignNotice);
	}

	[Fact]
	public void UpdateKnownHouses_UsesHousingVisibilityDistanceAndReturnsDeltas()
	{
		var service = new HousingVisibilityService(visibilityDistance: 200f);
		var player = CreatePlayer(position: new WorldPosition(210010000, 0, 0, 0, 0));
		var visibleBeyondCreatureDistance = CreateHouse(100, 700100, new WorldPosition(210010000, 150, 0, 0, 0));
		var tooFar = CreateHouse(101, 700101, new WorldPosition(210010000, 201, 0, 0, 0));
		var otherMap = CreateHouse(102, 700102, new WorldPosition(220010000, 10, 0, 0, 0));

		var first = service.UpdateKnownHouses(player, [visibleBeyondCreatureDistance, tooFar, otherMap]);
		var second = service.UpdateKnownHouses(player, [visibleBeyondCreatureDistance, tooFar, otherMap]);
		player.Position = player.Position with { X = 500 };
		var third = service.UpdateKnownHouses(player, [visibleBeyondCreatureDistance, tooFar, otherMap]);

		Assert.Equal([700100], first.Appeared.Select(house => house.AddressId).ToArray());
		Assert.Empty(first.DisappearedAddressIds);
		Assert.Empty(second.Appeared);
		Assert.Empty(second.DisappearedAddressIds);
		Assert.Empty(third.Appeared);
		Assert.Equal([700100], third.DisappearedAddressIds);
	}

	[Fact]
	public void World_TracksHouseSnapshotsSeparatelyFromObjects()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var house = CreateHouse(100, 700100, new WorldPosition(210010000, 1, 2, 3, 0));

		world.AddOrUpdateHouse(house);

		Assert.Equal(1, world.HouseCount);
		Assert.Equal(1, world.ObjectCount);
		Assert.Same(house, Assert.Single(world.GetHouses()));
		Assert.True(world.TryGetObject(100, out var stored));
		Assert.Same(house, stored);

		world.TryRemoveObject(100, out _);

		Assert.Equal(0, world.HouseCount);
		Assert.Equal(0, world.ObjectCount);
	}

	private static Player CreatePlayer(WorldPosition? position = null)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Owner",
			Position = position ?? new WorldPosition(210010000, 0, 0, 0, 0),
		};
	}

	private static WorldHouse CreateHouse(int objectId, int addressId, WorldPosition position)
	{
		return new WorldHouse(
			objectId,
			addressId,
			730001,
			1001,
			"Owner",
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			false,
			PlayerHouse.DoorOpen,
			true,
			null,
			position);
	}
}
