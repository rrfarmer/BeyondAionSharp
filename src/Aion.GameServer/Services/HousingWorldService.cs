using System.Collections.Concurrent;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class HousingWorldService : GameEngine
{
	private readonly IHousingRepository _housingRepository;
	private readonly IDFactory _idFactory;
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly GameWorld _world;
	private readonly IHouseDoorStateService? _houseDoorStateService;
	private readonly ILogger<HousingWorldService> _logger;
	private readonly ConcurrentDictionary<int, WorldHouse> _studiosByOwnerObjectId = new();
	private int _loadedCount;
	private int _loadedStudioCount;

	public HousingWorldService(
		IHousingRepository housingRepository,
		IDFactory idFactory,
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		ILogger<HousingWorldService> logger,
		IHouseDoorStateService? houseDoorStateService = null)
	{
		_housingRepository = housingRepository;
		_idFactory = idFactory;
		_runtimeContext = runtimeContext;
		_world = world;
		_houseDoorStateService = houseDoorStateService;
		_logger = logger;
	}

	public string Name => "HousingWorldService";

	public int LoadedCount => Volatile.Read(ref _loadedCount);

	public int LoadedStudioCount => Volatile.Read(ref _loadedStudioCount);

	public async ValueTask InitAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingService constructor loads persistent houses before maps spawn them.
		var housingTemplates = _runtimeContext.DataManager?.StaticData.HousingTemplates;
		var housingObjectTemplates = _runtimeContext.DataManager?.StaticData.HousingObjectTemplates;
		if (housingTemplates == null || housingObjectTemplates == null)
		{
			_logger.LogWarning("Housing templates are not loaded; skipping world house load");
			return;
		}

		await LoadWorldHousesAsync(housingTemplates, housingObjectTemplates, cancellationToken);
		await LoadWorldStudiosAsync(housingTemplates, housingObjectTemplates, cancellationToken);
	}

	public async Task<int> LoadWorldHousesAsync(
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable? housingObjectTemplates = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingService.spawnHouses stores spawned custom houses in World.
		var persistentHouses = await _housingRepository.LoadWorldHousesAsync(housingTemplates, cancellationToken);
		var validatedHouses = ValidatePersistentWorldHouses(persistentHouses, housingTemplates);
		var houses = AddMissingUnownedCustomHouses(validatedHouses, housingTemplates);
		if (housingObjectTemplates != null)
			houses = await AttachRegistriesAsync(houses, housingTemplates, housingObjectTemplates, cancellationToken);
		foreach (var house in houses)
		{
			_world.AddOrUpdateHouse(house);
			_houseDoorStateService?.SetHouseDoorState(house.Position.WorldId, house.AddressId, house.DoorState);
		}
		Volatile.Write(ref _loadedCount, houses.Count);
		_logger.LogInformation(
			"Loaded {PersistentCount} persistent and {SyntheticCount} unowned houses into world visibility",
			persistentHouses.Count,
			houses.Count - persistentHouses.Count);
		return houses.Count;
	}

	public async Task<int> LoadWorldStudiosAsync(
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable? housingObjectTemplates = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingService constructor keeps PERSONAL_INS studios in a separate owner-keyed map.
		var persistentStudios = await _housingRepository.LoadWorldStudiosAsync(housingTemplates, cancellationToken);
		var studios = ValidatePersistentWorldStudios(persistentStudios, housingTemplates);
		if (housingObjectTemplates != null)
			studios = await AttachRegistriesAsync(studios, housingTemplates, housingObjectTemplates, cancellationToken);

		_studiosByOwnerObjectId.Clear();
		foreach (var studio in studios)
			_studiosByOwnerObjectId[studio.OwnerObjectId] = studio;

		Volatile.Write(ref _loadedStudioCount, _studiosByOwnerObjectId.Count);
		_logger.LogInformation("Loaded {StudioCount} studios into owner-keyed housing cache", _studiosByOwnerObjectId.Count);
		return _studiosByOwnerObjectId.Count;
	}

	public bool TryGetPlayerStudio(int playerObjectId, out WorldHouse? studio)
	{
		// Java parity: services/HousingService.getPlayerStudio.
		return _studiosByOwnerObjectId.TryGetValue(playerObjectId, out studio);
	}

	public bool TrySpawnStudio(int playerObjectId, int worldId, out WorldHouse? studio)
	{
		// Java parity: services/HousingService.spawnStudio spawns the owner studio only for the matching studio world.
		if (!TryGetPlayerStudio(playerObjectId, out studio) || studio == null || studio.Position.WorldId != worldId)
		{
			studio = null;
			return false;
		}

		_world.AddOrUpdateHouse(studio);
		_houseDoorStateService?.SetHouseDoorState(studio.Position.WorldId, studio.AddressId, studio.DoorState);
		return true;
	}

	private async Task<IReadOnlyList<WorldHouse>> AttachRegistriesAsync(
		IEnumerable<WorldHouse> houses,
		HousingTemplateTable housingTemplates,
		HousingObjectTemplateTable housingObjectTemplates,
		CancellationToken cancellationToken)
	{
		// Java parity: controllers/HouseController.onAfterSpawn loads HouseRegistry before spawned house objects are exposed.
		var loaded = new List<WorldHouse>();
		foreach (var house in houses)
		{
			if (house.OwnerObjectId <= 0)
			{
				loaded.Add(house);
				continue;
			}

			var registry = await _housingRepository.LoadHouseRegistryAsync(
				house.OwnerObjectId,
				house.BuildingId,
				housingTemplates,
				housingObjectTemplates,
				cancellationToken);
			loaded.Add(house with { Registry = registry });
		}

		return loaded;
	}

	private IReadOnlyList<WorldHouse> ValidatePersistentWorldHouses(
		IReadOnlyList<WorldHouse> persistentHouses,
		HousingTemplateTable housingTemplates)
	{
		// Java parity: dao/HousesDAO.loadHouses skips missing building templates and duplicate custom-house addresses.
		var houses = new List<WorldHouse>();
		var knownAddresses = new HashSet<int>();
		foreach (var house in persistentHouses)
		{
			if (housingTemplates.GetAddress(house.AddressId) == null)
			{
				_logger.LogWarning("Skipping DB house {HouseObjectId} with unknown address {AddressId}", house.ObjectId, house.AddressId);
				continue;
			}

			if (housingTemplates.GetBuilding(house.BuildingId) == null)
			{
				_logger.LogWarning("Skipping DB house {HouseObjectId} with unknown building {BuildingId}", house.ObjectId, house.BuildingId);
				continue;
			}

			if (!knownAddresses.Add(house.AddressId))
			{
				_logger.LogWarning("Skipping duplicate DB house address {AddressId} for house {HouseObjectId}", house.AddressId, house.ObjectId);
				continue;
			}

			houses.Add(house);
		}

		return houses;
	}

	private IReadOnlyList<WorldHouse> ValidatePersistentWorldStudios(
		IReadOnlyList<WorldHouse> persistentStudios,
		HousingTemplateTable housingTemplates)
	{
		// Java parity: dao/HousesDAO.loadHouses(..., true) keys studios by owner, not by shared studio address.
		var studios = new List<WorldHouse>();
		var knownOwners = new HashSet<int>();
		foreach (var studio in persistentStudios)
		{
			if (studio.OwnerObjectId <= 0)
			{
				_logger.LogWarning("Skipping studio {HouseObjectId} without an owner", studio.ObjectId);
				continue;
			}

			var address = housingTemplates.GetAddress(studio.AddressId);
			if (address == null)
			{
				_logger.LogWarning("Skipping DB studio {HouseObjectId} with unknown address {AddressId}", studio.ObjectId, studio.AddressId);
				continue;
			}

			if (!string.Equals(address.DefaultBuildingType, "PERSONAL_INS", StringComparison.OrdinalIgnoreCase))
			{
				_logger.LogWarning("Skipping DB studio {HouseObjectId} with non-studio address {AddressId}", studio.ObjectId, studio.AddressId);
				continue;
			}

			if (housingTemplates.GetBuilding(studio.BuildingId) == null)
			{
				_logger.LogWarning("Skipping DB studio {HouseObjectId} with unknown building {BuildingId}", studio.ObjectId, studio.BuildingId);
				continue;
			}

			if (!knownOwners.Add(studio.OwnerObjectId))
			{
				_logger.LogWarning("Skipping duplicate DB studio owner {OwnerObjectId} for house {HouseObjectId}", studio.OwnerObjectId, studio.ObjectId);
				continue;
			}

			studios.Add(studio);
		}

		return studios;
	}

	private IReadOnlyList<WorldHouse> AddMissingUnownedCustomHouses(
		IReadOnlyList<WorldHouse> persistentHouses,
		HousingTemplateTable housingTemplates)
	{
		// Java parity: services/HousingService.spawnHouses creates ownerless custom houses for static addresses absent from DB.
		var houses = new List<WorldHouse>(persistentHouses);
		var knownAddresses = persistentHouses.Select(house => house.AddressId).ToHashSet();
		foreach (var address in housingTemplates.GetCustomFieldAddresses())
		{
			if (knownAddresses.Contains(address.AddressId))
				continue;

			if (WorldHouse.TryCreateUnowned(address, _idFactory.NextId, out var worldHouse) && worldHouse != null)
			{
				houses.Add(worldHouse);
				knownAddresses.Add(address.AddressId);
			}
		}

		return houses;
	}

	public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: House persistence is still handled by the existing settings/rent writes until periodic House.save is ported.
		Volatile.Write(ref _loadedCount, 0);
		Volatile.Write(ref _loadedStudioCount, 0);
		_studiosByOwnerObjectId.Clear();
		return ValueTask.CompletedTask;
	}
}
