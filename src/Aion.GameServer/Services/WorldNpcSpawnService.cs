using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcSpawnService : GameEngine
{
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly GameWorld _world;
	private readonly IDFactory _idFactory;
	private readonly GameTimeService? _gameTimeService;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly ILogger<WorldNpcSpawnService> _logger;
	private readonly ConcurrentDictionary<NpcSpawnSummary, int> _temporarySpawnObjectIds = new();
	private int _loadedCount;
	private int _skippedCount;

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		GameTimeService? gameTimeService,
		IGameClientConnectionRegistry? connectionRegistry,
		ILogger<WorldNpcSpawnService> logger)
	{
		_runtimeContext = runtimeContext;
		_world = world;
		_idFactory = idFactory;
		_gameTimeService = gameTimeService;
		_connectionRegistry = connectionRegistry;
		_logger = logger;
	}

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		GameTimeService? gameTimeService,
		ILogger<WorldNpcSpawnService> logger)
		: this(runtimeContext, world, idFactory, gameTimeService, null, logger)
	{
	}

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		ILogger<WorldNpcSpawnService> logger)
		: this(runtimeContext, world, idFactory, null, null, logger)
	{
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
			_gameTimeService?.GameMinutes ?? 0,
			DateTimeOffset.Now.DayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId: 0,
			changedMapIds: null,
			cancellationToken);
		if (_gameTimeService != null)
			_gameTimeService.HourChanged += OnGameHourChangedAsync;
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
		if (_gameTimeService != null)
			_gameTimeService.HourChanged -= OnGameHourChangedAsync;
		_temporarySpawnObjectIds.Clear();
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
		return SpawnWorldNpcs(
			spawns,
			npcTemplates,
			allowedMapIds,
			_gameTimeService?.GameMinutes ?? 0,
			DateTimeOffset.Now.DayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId: 0,
			changedMapIds: null,
			cancellationToken);
	}

	public WorldNpcSpawnResult SpawnWorldNpcs(
		NpcSpawnTable spawns,
		NpcTemplateTable npcTemplates,
		IEnumerable<int>? allowedMapIds,
		int gameMinutes,
		DayOfWeek serverDayOfWeek,
		byte difficultId = 0,
		CancellationToken cancellationToken = default)
	{
		return SpawnWorldNpcs(
			spawns,
			npcTemplates,
			allowedMapIds,
			gameMinutes,
			serverDayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId,
			changedMapIds: null,
			cancellationToken);
	}

	public async ValueTask<TemporarySpawnHourChangeResult> ProcessTemporarySpawnHourChangeAsync(
		int gameMinutes,
		DayOfWeek serverDayOfWeek,
		CancellationToken cancellationToken = default)
	{
		// Java parity: spawnengine/TemporarySpawnEngine.onHourChange despawns first, then spawns newly eligible groups.
		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
			return new TemporarySpawnHourChangeResult(SpawnedCount: 0, DespawnedCount: 0, SkippedCount: 0);

		return await ProcessTemporarySpawnHourChangeAsync(
			staticData.NpcSpawns,
			staticData.NpcTemplates,
			staticData.WorldMaps.Where(map => !map.IsInstance).Select(map => map.MapId),
			gameMinutes,
			serverDayOfWeek,
			cancellationToken);
	}

	public async ValueTask<TemporarySpawnHourChangeResult> ProcessTemporarySpawnHourChangeAsync(
		NpcSpawnTable spawns,
		NpcTemplateTable npcTemplates,
		IEnumerable<int>? allowedMapIds,
		int gameMinutes,
		DayOfWeek serverDayOfWeek,
		CancellationToken cancellationToken = default)
	{
		// Java parity: spawnengine/TemporarySpawnEngine.onHourChange despawns first, then spawns newly eligible groups.
		var changedMapIds = new HashSet<int>();
		var despawned = DespawnTemporaryNpcs(gameMinutes, serverDayOfWeek, changedMapIds);
		var result = SpawnWorldNpcs(
			spawns,
			npcTemplates,
			allowedMapIds,
			gameMinutes,
			serverDayOfWeek,
			TemporarySpawnEvaluationMode.HourlySpawn,
			includeAlwaysOn: false,
			includeTemporary: true,
			difficultId: 0,
			changedMapIds,
			cancellationToken);

		await RefreshNpcVisibilityAsync(changedMapIds, cancellationToken);
		return new TemporarySpawnHourChangeResult(result.SpawnedCount, despawned, result.SkippedCount);
	}

	private WorldNpcSpawnResult SpawnWorldNpcs(
		NpcSpawnTable spawns,
		NpcTemplateTable npcTemplates,
		IEnumerable<int>? allowedMapIds,
		int gameMinutes,
		DayOfWeek serverDayOfWeek,
		TemporarySpawnEvaluationMode temporarySpawnMode,
		bool includeAlwaysOn,
		bool includeTemporary,
		byte difficultId,
		ISet<int>? changedMapIds,
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
			var isTemporaryGroup = groupKey.TemporarySchedule != null;
			var hasTemporarySpot = groupSpawns.Any(spawn => spawn.SpotTemporarySchedule != null);
			if ((!includeAlwaysOn && !isTemporaryGroup) || (!includeTemporary && (isTemporaryGroup || hasTemporarySpot)))
			{
				skipped += groupSpawns.Length;
				continue;
			}

			if (allowedMaps != null && !allowedMaps.Contains(groupKey.MapId))
			{
				skipped += groupSpawns.Length;
				continue;
			}

			if (groupKey.DifficultId != 0 && groupKey.DifficultId != difficultId)
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

			if (groupKey.TemporarySchedule != null
				&& !IsTemporaryScheduleActive(groupKey.TemporarySchedule, gameMinutes, serverDayOfWeek, temporarySpawnMode))
			{
				skipped += groupSpawns.Length;
				continue;
			}

			if (temporarySpawnMode == TemporarySpawnEvaluationMode.HourlySpawn
				&& groupKey.PoolSize > 0
				&& groupSpawns.Any(spawn => _temporarySpawnObjectIds.ContainsKey(spawn)))
			{
				skipped += groupSpawns.Length;
				continue;
			}

			// Java parity: SpawnEngine.spawnInstance checks spot-level TemporarySpawn.isInSpawnTime in the non-pool branch.
			var activeSpawns = groupKey.PoolSize > 0
				? groupSpawns
				: groupSpawns
					.Where(spawn => spawn.SpotTemporarySchedule == null
						|| IsTemporaryScheduleActive(spawn.SpotTemporarySchedule, gameMinutes, serverDayOfWeek, temporarySpawnMode))
					.ToArray();
			skipped += groupSpawns.Length - activeSpawns.Length;

			foreach (var spawn in SelectActivePoolSpots(activeSpawns))
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (spawn.GroupTemporarySchedule != null && _temporarySpawnObjectIds.ContainsKey(spawn))
				{
					skipped++;
					continue;
				}

				var objectId = SpawnNpc(spawn, template);
				if (objectId.HasValue)
				{
					spawned++;
					if (spawn.GroupTemporarySchedule != null)
						_temporarySpawnObjectIds[spawn] = objectId.Value;
					changedMapIds?.Add(spawn.MapId);
				}
				else
				{
					skipped++;
				}
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
			_gameTimeService?.GameMinutes ?? 0,
			DateTimeOffset.Now.DayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId: 0,
			changedMapIds: null,
			cancellationToken);
	}

	private int? SpawnNpc(NpcSpawnSummary spawn, NpcTemplateSummary template)
	{
		var objectId = _idFactory.NextId();
		var worldNpc = new WorldNpc(
			objectId,
			template.TemplateId,
			template,
			new global::Aion.GameServer.World.WorldPosition(spawn.MapId, spawn.X, spawn.Y, spawn.Z, spawn.Heading),
			WorldNpcState.FromTemplateAndSpawn(template, spawn.State));
		if (!_world.TryAddObject(objectId, worldNpc))
		{
			_idFactory.ReleaseId(objectId);
			return null;
		}

		return objectId;
	}

	private int DespawnTemporaryNpcs(int gameMinutes, DayOfWeek serverDayOfWeek, ISet<int> changedMapIds)
	{
		var despawned = 0;
		foreach (var pair in _temporarySpawnObjectIds.ToArray())
		{
			var spawn = pair.Key;
			var schedule = spawn.SpotTemporarySchedule ?? spawn.GroupTemporarySchedule;
			if (schedule == null || !schedule.CanDespawn(gameMinutes, serverDayOfWeek))
				continue;

			if (_temporarySpawnObjectIds.TryRemove(spawn, out var objectId)
				&& _world.TryRemoveObject(objectId, out _))
			{
				_idFactory.ReleaseId(objectId);
				changedMapIds.Add(spawn.MapId);
				despawned++;
			}
		}

		return despawned;
	}

	private async ValueTask RefreshNpcVisibilityAsync(IReadOnlySet<int> changedMapIds, CancellationToken cancellationToken)
	{
		if (_connectionRegistry == null)
			return;

		foreach (var mapId in changedMapIds)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await _connectionRegistry.RefreshNpcVisibilityAsync(_world.GetNpcs(mapId));
		}
	}

	private async ValueTask OnGameHourChangedAsync(int gameMinutes, CancellationToken cancellationToken)
	{
		await ProcessTemporarySpawnHourChangeAsync(gameMinutes, DateTimeOffset.Now.DayOfWeek, cancellationToken);
	}

	private static IReadOnlyList<NpcSpawnSummary> SelectActivePoolSpots(IReadOnlyList<NpcSpawnSummary> groupSpawns)
	{
		if (groupSpawns.Count == 0)
			return Array.Empty<NpcSpawnSummary>();

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

	private static bool IsTemporaryScheduleActive(
		TemporarySpawnSchedule schedule,
		int gameMinutes,
		DayOfWeek serverDayOfWeek,
		TemporarySpawnEvaluationMode mode)
	{
		return mode == TemporarySpawnEvaluationMode.HourlySpawn
			? schedule.CanSpawn(gameMinutes, serverDayOfWeek)
			: schedule.IsInSpawnTime(gameMinutes, serverDayOfWeek);
	}

	private static NpcSpawnGroupKey CreateSpawnGroupKey(NpcSpawnSummary spawn)
	{
		// Java parity: SpawnsData unique direct spawn groups are keyed by map, npc_id, and custom flag.
		return new NpcSpawnGroupKey(
			spawn.MapId,
			spawn.NpcId,
			spawn.RespawnSeconds,
			spawn.PoolSize,
			spawn.DifficultId,
			spawn.Handler,
			spawn.Custom,
			spawn.GroupTemporarySchedule);
	}

	private readonly record struct NpcSpawnGroupKey(
		int MapId,
		int NpcId,
		int RespawnSeconds,
		int PoolSize,
		byte DifficultId,
		string Handler,
		bool Custom,
		TemporarySpawnSchedule? TemporarySchedule);

	private enum TemporarySpawnEvaluationMode
	{
		Startup,
		HourlySpawn,
	}
}

public readonly record struct WorldNpcSpawnResult(int SpawnedCount, int SkippedCount);

public readonly record struct TemporarySpawnHourChangeResult(int SpawnedCount, int DespawnedCount, int SkippedCount);
