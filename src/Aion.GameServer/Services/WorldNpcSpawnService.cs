using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcSpawnService : GameEngine
{
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly GameWorld _world;
	private readonly IDFactory _idFactory;
	private readonly ILogger<WorldNpcSpawnService> _logger;
	private int _loadedCount;
	private int _skippedCount;

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		ILogger<WorldNpcSpawnService> logger)
	{
		_runtimeContext = runtimeContext;
		_world = world;
		_idFactory = idFactory;
		_logger = logger;
	}

	public string Name => "WorldNpcSpawnService";

	public int LoadedCount => Volatile.Read(ref _loadedCount);

	public int SkippedCount => Volatile.Read(ref _skippedCount);

	public ValueTask InitAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: GameServer.main calls SpawnEngine.spawnAll after DataManager and HousingService startup.
		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
		{
			_logger.LogWarning("Static data is not loaded; skipping NPC world spawn load");
			return ValueTask.CompletedTask;
		}

		var result = SpawnWorldNpcs(
			staticData.NpcSpawns,
			staticData.NpcTemplates,
			staticData.WorldMaps.Where(map => !map.IsInstance).Select(map => map.MapId),
			cancellationToken);
		Volatile.Write(ref _loadedCount, result.SpawnedCount);
		Volatile.Write(ref _skippedCount, result.SkippedCount);
		_logger.LogInformation(
			"Loaded {SpawnedCount} regular NPC spawns into world visibility; skipped {SkippedCount} unsupported spawns",
			result.SpawnedCount,
			result.SkippedCount);
		return ValueTask.CompletedTask;
	}

	public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
	{
		Volatile.Write(ref _loadedCount, 0);
		Volatile.Write(ref _skippedCount, 0);
		return ValueTask.CompletedTask;
	}

	public WorldNpcSpawnResult SpawnWorldNpcs(
		NpcSpawnTable spawns,
		NpcTemplateTable npcTemplates,
		IEnumerable<int>? allowedMapIds = null,
		CancellationToken cancellationToken = default)
	{
		var allowedMaps = allowedMapIds?.ToHashSet();
		var spawned = 0;
		var skipped = 0;
		foreach (var group in spawns.Spawns.GroupBy(CreateSpawnGroupKey))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var groupSpawns = group.ToArray();
			var groupKey = group.Key;
			if (allowedMaps != null && !allowedMaps.Contains(groupKey.MapId))
			{
				skipped += groupSpawns.Length;
				continue;
			}

			if (!CanMaterializeAsWorldNpc(groupKey))
			{
				skipped += groupSpawns.Length;
				continue;
			}

			var template = npcTemplates.GetNpcTemplate(groupKey.NpcId);
			if (template == null)
			{
				skipped += groupSpawns.Length;
				continue;
			}

			foreach (var spawn in SelectActivePoolSpots(groupSpawns))
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (SpawnNpc(spawn, template))
					spawned++;
				else
					skipped++;
			}
		}

		return new WorldNpcSpawnResult(spawned, skipped);
	}

	public WorldNpcSpawnResult SpawnWorldNpcsForMap(
		int mapId,
		NpcSpawnTable spawns,
		NpcTemplateTable npcTemplates,
		CancellationToken cancellationToken = default)
	{
		return SpawnWorldNpcs(
			new NpcSpawnTable(spawns.GetSpawnsForMap(mapId)),
			npcTemplates,
			[mapId],
			cancellationToken);
	}

	private bool SpawnNpc(NpcSpawnSummary spawn, NpcTemplateSummary template)
	{
		var objectId = _idFactory.NextId();
		var worldNpc = new WorldNpc(
			objectId,
			template.TemplateId,
			template,
			new global::Aion.GameServer.World.WorldPosition(spawn.MapId, spawn.X, spawn.Y, spawn.Z, spawn.Heading));
		if (!_world.TryAddObject(objectId, worldNpc))
		{
			_idFactory.ReleaseId(objectId);
			return false;
		}

		return true;
	}

	private static IReadOnlyList<NpcSpawnSummary> SelectActivePoolSpots(IReadOnlyList<NpcSpawnSummary> groupSpawns)
	{
		var poolSize = groupSpawns[0].PoolSize;
		if (poolSize <= 0 || poolSize >= groupSpawns.Count)
			return groupSpawns;

		// Java parity: SpawnGroup.reserveRandomFreePoolSpot chooses unique random active spots per instance.
		return groupSpawns
			.OrderBy(_ => Random.Shared.Next())
			.Take(poolSize)
			.ToArray();
	}

	private static bool CanMaterializeAsWorldNpc(NpcSpawnGroupKey spawn)
	{
		// Java parity: this first C# pass mirrors SpawnEngine's ordinary spawnObject branch only.
		return string.IsNullOrEmpty(spawn.Handler)
			&& spawn.NpcId is <= 400000 or >= 499999;
	}

	private static NpcSpawnGroupKey CreateSpawnGroupKey(NpcSpawnSummary spawn)
	{
		// Java parity: SpawnsData unique direct spawn groups are keyed by map, npc_id, and custom flag.
		return new NpcSpawnGroupKey(
			spawn.MapId,
			spawn.NpcId,
			spawn.RespawnSeconds,
			spawn.PoolSize,
			spawn.Handler,
			spawn.Custom);
	}

	private readonly record struct NpcSpawnGroupKey(
		int MapId,
		int NpcId,
		int RespawnSeconds,
		int PoolSize,
		string Handler,
		bool Custom);
}

public readonly record struct WorldNpcSpawnResult(int SpawnedCount, int SkippedCount);
