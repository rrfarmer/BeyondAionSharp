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
	private readonly ILogger<HousingWorldService> _logger;
	private int _loadedCount;

	public HousingWorldService(
		IHousingRepository housingRepository,
		IDFactory idFactory,
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		ILogger<HousingWorldService> logger)
	{
		_housingRepository = housingRepository;
		_idFactory = idFactory;
		_runtimeContext = runtimeContext;
		_world = world;
		_logger = logger;
	}

	public string Name => "HousingWorldService";

	public int LoadedCount => Volatile.Read(ref _loadedCount);

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
			_world.AddOrUpdateHouse(house);
		Volatile.Write(ref _loadedCount, houses.Count);
		_logger.LogInformation(
			"Loaded {PersistentCount} persistent and {SyntheticCount} unowned houses into world visibility",
			persistentHouses.Count,
			houses.Count - persistentHouses.Count);
		return houses.Count;
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
		return ValueTask.CompletedTask;
	}
}
