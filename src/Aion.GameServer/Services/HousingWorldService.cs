using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class HousingWorldService : GameEngine
{
	private readonly IHousingRepository _housingRepository;
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly GameWorld _world;
	private readonly ILogger<HousingWorldService> _logger;
	private int _loadedCount;

	public HousingWorldService(
		IHousingRepository housingRepository,
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		ILogger<HousingWorldService> logger)
	{
		_housingRepository = housingRepository;
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
		if (housingTemplates == null)
		{
			_logger.LogWarning("Housing templates are not loaded; skipping world house load");
			return;
		}

		await LoadWorldHousesAsync(housingTemplates, cancellationToken);
	}

	public async Task<int> LoadWorldHousesAsync(HousingTemplateTable housingTemplates, CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingService.spawnHouses stores spawned custom houses in World.
		var houses = await _housingRepository.LoadWorldHousesAsync(housingTemplates, cancellationToken);
		foreach (var house in houses)
			_world.AddOrUpdateHouse(house);
		Volatile.Write(ref _loadedCount, houses.Count);
		_logger.LogInformation("Loaded {Count} persistent houses into world visibility", houses.Count);
		return houses.Count;
	}

	public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: House persistence is still handled by the existing settings/rent writes until periodic House.save is ported.
		Volatile.Write(ref _loadedCount, 0);
		return ValueTask.CompletedTask;
	}
}
