using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class HousingWorldServiceTests
{
	[Fact]
	public async Task LoadWorldHousesAsync_StoresPersistentHouseSnapshotsInWorld()
	{
		var templates = new HousingTemplateTable(
			[new HousingAddressSummary(700100, 1, 798000, MapId: 210010000, X: 10, Y: 20, Z: 30)],
			[new HousingBuildingSummary(730001, "HOUSE", 1)]);
		var worldHouse = new WorldHouse(
			5001,
			700100,
			730001,
			1001,
			"Owner",
			0,
			string.Empty,
			0,
			0,
			0,
			0,
			0,
			0,
			false,
			PlayerHouse.DoorOpen,
			true,
			"notice",
			new WorldPosition(210010000, 10, 20, 30, 0));
		var repository = new CapturingHousingRepository([worldHouse]);
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var doorStates = new HouseDoorStateService();
		var service = new HousingWorldService(
			repository,
			new IDFactory([worldHouse.ObjectId]),
			new GameServerRuntimeContext(),
			world,
			NullLogger<HousingWorldService>.Instance,
			doorStates);

		var loaded = await service.LoadWorldHousesAsync(templates);

		Assert.Equal(1, loaded);
		Assert.Equal(1, service.LoadedCount);
		Assert.Same(templates, repository.Templates);
		Assert.Same(worldHouse, Assert.Single(world.GetHouses()));
		Assert.True(world.TryGetObject(worldHouse.ObjectId, out var stored));
		Assert.Same(worldHouse, stored);
		Assert.Equal(PlayerHouse.DoorOpen, doorStates.GetHouseDoorState(210010000, 700100));
	}

	[Fact]
	public async Task LoadWorldHousesAsync_AttachesPersistentHouseRegistries()
	{
		var templates = new HousingTemplateTable(
			[new HousingAddressSummary(700100, 1, 798000, MapId: 210010000, X: 10, Y: 20, Z: 30)],
			[new HousingBuildingSummary(730001, "HOUSE", 1)]);
		var objectTemplates = new HousingObjectTemplateTable(
			[new HousingObjectTemplateSummary(3001000, 7, "npc", "EXTERIOR", "FLOOR", "NONE", "NPC", 0, false, NpcId: 810013)]);
		var registry = HouseRegistrySummary.FromRows(
			730001,
			templates,
			objectTemplates,
			[new HouseRegisteredItemRow(9001, 3001000, null, null, 0, 0, 0, 1, 2, 3, 10, "EXTERIOR", 0)]);
		var worldHouse = new WorldHouse(
			5001,
			700100,
			730001,
			1001,
			"Owner",
			0,
			string.Empty,
			0,
			0,
			0,
			0,
			0,
			0,
			false,
			PlayerHouse.DoorOpen,
			true,
			"notice",
			new WorldPosition(210010000, 10, 20, 30, 0));
		var repository = new CapturingHousingRepository([worldHouse]) { Registry = registry };
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = new HousingWorldService(
			repository,
			new IDFactory([worldHouse.ObjectId]),
			new GameServerRuntimeContext(),
			world,
			NullLogger<HousingWorldService>.Instance);

		await service.LoadWorldHousesAsync(templates, objectTemplates);

		var storedHouse = Assert.Single(world.GetHouses());
		Assert.Same(registry, storedHouse.Registry);
		Assert.Equal(1001, repository.RegistryPlayerObjectId);
		Assert.Equal(730001, repository.RegistryBuildingId);
	}

	[Fact]
	public async Task LoadWorldHousesAsync_SkipsInvalidAndDuplicatePersistentRows()
	{
		var templates = new HousingTemplateTable(
			[
				new HousingAddressSummary(
					700100,
					1,
					798000,
					MapId: 210010000,
					X: 10,
					Y: 20,
					Z: 30,
					DefaultBuildingId: 730001,
					DefaultBuildingType: "PERSONAL_FIELD"),
				new HousingAddressSummary(
					700101,
					1,
					798000,
					MapId: 210010000,
					X: 40,
					Y: 50,
					Z: 60,
					DefaultBuildingId: 730001,
					DefaultBuildingType: "PERSONAL_FIELD"),
			],
			[new HousingBuildingSummary(730001, "HOUSE", 1, "PERSONAL_FIELD")]);
		var validHouse = new WorldHouse(
			5001,
			700100,
			730001,
			1001,
			"Owner",
			0,
			string.Empty,
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
			new WorldPosition(210010000, 10, 20, 30, 0));
		var duplicateAddressHouse = validHouse with { ObjectId = 5002, OwnerObjectId = 1002, OwnerName = "Duplicate" };
		var missingBuildingHouse = validHouse with { ObjectId = 5003, AddressId = 700101, BuildingId = 739999, OwnerObjectId = 1003 };
		var missingAddressHouse = validHouse with { ObjectId = 5004, AddressId = 799999, OwnerObjectId = 1004 };
		var repository = new CapturingHousingRepository([validHouse, duplicateAddressHouse, missingBuildingHouse, missingAddressHouse]);
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = new HousingWorldService(
			repository,
			new IDFactory([5001, 5002, 5003, 5004]),
			new GameServerRuntimeContext(),
			world,
			NullLogger<HousingWorldService>.Instance);

		var loaded = await service.LoadWorldHousesAsync(templates);

		Assert.Equal(2, loaded);
		Assert.Equal(2, service.LoadedCount);
		var houses = world.GetHouses().OrderBy(house => house.AddressId).ToArray();
		Assert.Equal([700100, 700101], houses.Select(house => house.AddressId).ToArray());
		Assert.Equal(5001, houses[0].ObjectId);
		Assert.Equal(0, houses[1].OwnerObjectId);
		Assert.DoesNotContain(world.GetHouses(), house => house.ObjectId is 5002 or 5003 or 5004);
	}

	[Fact]
	public async Task LoadWorldHousesAsync_SynthesizesOwnerlessCustomHousesMissingFromDb()
	{
		var templates = new HousingTemplateTable(
			[
				new HousingAddressSummary(
					700100,
					1,
					798000,
					MapId: 210010000,
					X: 10,
					Y: 20,
					Z: 30,
					DefaultBuildingId: 730001,
					DefaultBuildingType: "PERSONAL_FIELD"),
				new HousingAddressSummary(
					700101,
					1,
					798000,
					MapId: 210010000,
					X: 40,
					Y: 50,
					Z: 60,
					DefaultBuildingId: 730001,
					DefaultBuildingType: "PERSONAL_FIELD"),
				new HousingAddressSummary(
					2001,
					2,
					798001,
					MapId: 720010000,
					X: 70,
					Y: 80,
					Z: 90,
					DefaultBuildingId: 735001,
					DefaultBuildingType: "PERSONAL_INS"),
			],
			[
				new HousingBuildingSummary(730001, "HOUSE", 1, "PERSONAL_FIELD"),
				new HousingBuildingSummary(735001, "STUDIO", 0, "PERSONAL_INS"),
			]);
		var persistentHouse = new WorldHouse(
			5001,
			700100,
			730001,
			1001,
			"Owner",
			0,
			string.Empty,
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
			new WorldPosition(210010000, 10, 20, 30, 0));
		var repository = new CapturingHousingRepository([persistentHouse]);
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = new HousingWorldService(
			repository,
			new IDFactory([persistentHouse.ObjectId]),
			new GameServerRuntimeContext(),
			world,
			NullLogger<HousingWorldService>.Instance);

		var loaded = await service.LoadWorldHousesAsync(templates);

		Assert.Equal(2, loaded);
		var houses = world.GetHouses().OrderBy(house => house.AddressId).ToArray();
		Assert.Equal([700100, 700101], houses.Select(house => house.AddressId).ToArray());
		var ownerlessHouse = houses[1];
		Assert.Equal(730001, ownerlessHouse.BuildingId);
		Assert.Equal(0, ownerlessHouse.OwnerObjectId);
		Assert.Equal(PlayerHouse.DoorClosed, ownerlessHouse.DoorState);
		Assert.True(ownerlessHouse.ShowOwnerName);
		Assert.Equal(new WorldPosition(210010000, 40, 50, 60, 0), ownerlessHouse.Position);
		Assert.True(world.TryGetObject(ownerlessHouse.ObjectId, out var stored));
		Assert.Same(ownerlessHouse, stored);
		Assert.DoesNotContain(world.GetHouses(), house => house.AddressId == 2001);
	}

	[Fact]
	public async Task LoadWorldStudiosAsync_CachesStudiosByOwnerWithoutSpawningThem()
	{
		var templates = CreateStudioTemplates();
		var studio = CreateStudio(ownerObjectId: 1001, objectId: 6001);
		var repository = new CapturingHousingRepository([]) { Studios = [studio] };
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = new HousingWorldService(
			repository,
			new IDFactory([studio.ObjectId]),
			new GameServerRuntimeContext(),
			world,
			NullLogger<HousingWorldService>.Instance);

		var loaded = await service.LoadWorldStudiosAsync(templates);

		Assert.Equal(1, loaded);
		Assert.Equal(1, service.LoadedStudioCount);
		Assert.True(service.TryGetPlayerStudio(1001, out var cachedStudio));
		Assert.Same(studio, cachedStudio);
		Assert.Empty(world.GetHouses());
		Assert.False(world.TryGetObject(studio.ObjectId, out _));
	}

	[Fact]
	public async Task TrySpawnStudio_StoresCachedStudioOnlyForMatchingWorld()
	{
		var templates = CreateStudioTemplates();
		var studio = CreateStudio(ownerObjectId: 1001, objectId: 6001);
		var repository = new CapturingHousingRepository([]) { Studios = [studio] };
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var doorStates = new HouseDoorStateService();
		var service = new HousingWorldService(
			repository,
			new IDFactory([studio.ObjectId]),
			new GameServerRuntimeContext(),
			world,
			NullLogger<HousingWorldService>.Instance,
			doorStates);
		await service.LoadWorldStudiosAsync(templates);

		var wrongWorld = service.TrySpawnStudio(1001, 210010000, out var missingStudio);
		var spawned = service.TrySpawnStudio(1001, 720010000, out var spawnedStudio);

		Assert.False(wrongWorld);
		Assert.Null(missingStudio);
		Assert.True(spawned);
		Assert.Same(studio, spawnedStudio);
		Assert.True(world.TryGetObject(studio.ObjectId, out var stored));
		Assert.Same(studio, stored);
		Assert.Equal(PlayerHouse.DoorOpen, doorStates.GetHouseDoorState(720010000, studio.AddressId));
	}

	[Fact]
	public async Task LoadWorldStudiosAsync_AllowsSharedStudioAddressForDifferentOwners()
	{
		var templates = CreateStudioTemplates();
		var firstStudio = CreateStudio(ownerObjectId: 1001, objectId: 6001);
		var secondStudio = CreateStudio(ownerObjectId: 1002, objectId: 6002);
		var repository = new CapturingHousingRepository([]) { Studios = [firstStudio, secondStudio] };
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = new HousingWorldService(
			repository,
			new IDFactory([firstStudio.ObjectId, secondStudio.ObjectId]),
			new GameServerRuntimeContext(),
			world,
			NullLogger<HousingWorldService>.Instance);

		var loaded = await service.LoadWorldStudiosAsync(templates);

		Assert.Equal(2, loaded);
		Assert.True(service.TryGetPlayerStudio(1001, out var cachedFirst));
		Assert.True(service.TryGetPlayerStudio(1002, out var cachedSecond));
		Assert.Same(firstStudio, cachedFirst);
		Assert.Same(secondStudio, cachedSecond);
	}

	private static HousingTemplateTable CreateStudioTemplates()
	{
		return new HousingTemplateTable(
			[new HousingAddressSummary(
				2001,
				2,
				798001,
				MapId: 720010000,
				X: 70,
				Y: 80,
				Z: 90,
				DefaultBuildingId: 735001,
				DefaultBuildingType: "PERSONAL_INS")],
			[new HousingBuildingSummary(735001, "STUDIO", 0, "PERSONAL_INS")]);
	}

	private static WorldHouse CreateStudio(int ownerObjectId, int objectId)
	{
		return new WorldHouse(
			objectId,
			2001,
			735001,
			ownerObjectId,
			$"Owner{ownerObjectId}",
			0,
			string.Empty,
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
			new WorldPosition(720010000, 70, 80, 90, 0));
	}

	private sealed class CapturingHousingRepository : IHousingRepository
	{
		private readonly IReadOnlyList<WorldHouse> _houses;

		public CapturingHousingRepository(IReadOnlyList<WorldHouse> houses)
		{
			_houses = houses;
		}

		public HousingTemplateTable? Templates { get; private set; }

		public HouseRegistrySummary Registry { get; init; } = HouseRegistrySummary.Empty;

		public IReadOnlyList<WorldHouse> Studios { get; init; } = Array.Empty<WorldHouse>();

		public int RegistryPlayerObjectId { get; private set; }

		public int RegistryBuildingId { get; private set; }

		public RegisteredHouseObjectSummary? SavedHouseObject { get; private set; }

		public int DeletedHouseObjectId { get; private set; }

		public Task<IReadOnlyList<WorldHouse>> LoadWorldHousesAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
		{
			Templates = housingTemplates;
			return Task.FromResult(_houses);
		}

		public Task<IReadOnlyList<WorldHouse>> LoadWorldStudiosAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
		{
			Templates = housingTemplates;
			return Task.FromResult(Studios);
		}

		public Task<HouseRegistrySummary> LoadHouseRegistryAsync(
			int playerObjectId,
			int buildingId,
			HousingTemplateTable housingTemplates,
			HousingObjectTemplateTable housingObjectTemplates,
			CancellationToken cancellationToken = default)
		{
			RegistryPlayerObjectId = playerObjectId;
			RegistryBuildingId = buildingId;
			return Task.FromResult(Registry);
		}

		public Task<bool> SaveHouseObjectPlacementAsync(
			int playerObjectId,
			RegisteredHouseObjectSummary houseObject,
			CancellationToken cancellationToken = default)
		{
			SavedHouseObject = houseObject;
			return Task.FromResult(true);
		}

		public Task<bool> RegisterHouseObjectFromInventoryAsync(
			int playerObjectId,
			int sourceItemObjectId,
			RegisteredHouseObjectSummary houseObject,
			int? expireTimeSeconds,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> RegisterHouseDecorationFromInventoryAsync(
			int playerObjectId,
			int sourceItemObjectId,
			RegisteredHouseDecorationSummary decoration,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> SaveHouseDecorationMutationAsync(
			int playerObjectId,
			IReadOnlyList<RegisteredHouseDecorationSummary> updatedDecorations,
			IReadOnlyList<int> deletedDecorationObjectIds,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> SaveHouseRenovationAsync(
			int playerObjectId,
			int houseObjectId,
			int buildingId,
			IReadOnlyList<InventoryItem> updatedCouponItems,
			IReadOnlyList<int> deletedCouponItemObjectIds,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> SaveHouseObjectUseAsync(
			int houseOwnerObjectId,
			int usingPlayerObjectId,
			RegisteredHouseObjectSummary? updatedHouseObject,
			int? deletedHouseObjectId,
			IReadOnlyList<InventoryItem> updatedConsumedItems,
			IReadOnlyList<int> deletedConsumedObjectIds,
			IReadOnlyList<InventoryItem> updatedRewardItems,
			IReadOnlyList<InventoryItem> addedRewardItems,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> DeleteHouseRegisteredObjectAsync(
			int playerObjectId,
			int itemObjectId,
			CancellationToken cancellationToken = default)
		{
			DeletedHouseObjectId = itemObjectId;
			return Task.FromResult(true);
		}

		public Task<bool> StoreHouseScriptAsync(
			int houseObjectId,
			int scriptId,
			string scriptXml,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<bool> DeleteHouseScriptAsync(
			int houseObjectId,
			int scriptId,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}
	}
}
