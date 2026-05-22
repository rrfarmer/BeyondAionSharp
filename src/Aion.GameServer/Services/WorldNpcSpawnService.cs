using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcSpawnService : GameEngine
{
	private static readonly TimeSpan ImmediateDecayDelay = TimeSpan.FromSeconds(2);
	private static readonly TimeSpan WithDropDecayDelay = TimeSpan.FromMinutes(5);
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly GameWorld _world;
	private readonly IDFactory _idFactory;
	private readonly GameTimeService? _gameTimeService;
	private readonly ThreadPoolManager? _threadPoolManager;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly IStaticPlaceableStateService? _staticPlaceables;
	private readonly IWorldNpcWalkerSpawnPlanCacheService? _walkerSpawnPlans;
	private readonly WorldNpcWalkerPlacementApplicationService? _walkerPlacementApplication;
	private readonly ILogger<WorldNpcSpawnService> _logger;
	private readonly ConcurrentDictionary<NpcSpawnSummary, int> _temporarySpawnObjectIds = new();
	private readonly ConcurrentDictionary<int, SpawnedWorldNpcRegistration> _spawnedWorldNpcs = new();
	private readonly ConcurrentDictionary<int, PendingWorldNpcRespawn> _pendingRespawns = new();
	private int _loadedCount;
	private int _skippedCount;

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		GameTimeService? gameTimeService,
		ThreadPoolManager? threadPoolManager,
		IGameClientConnectionRegistry? connectionRegistry,
		IStaticPlaceableStateService? staticPlaceables,
		IWorldNpcWalkerSpawnPlanCacheService? walkerSpawnPlans,
		WorldNpcWalkerPlacementApplicationService? walkerPlacementApplication,
		ILogger<WorldNpcSpawnService> logger)
	{
		_runtimeContext = runtimeContext;
		_world = world;
		_idFactory = idFactory;
		_gameTimeService = gameTimeService;
		_threadPoolManager = threadPoolManager;
		_connectionRegistry = connectionRegistry;
		_staticPlaceables = staticPlaceables;
		_walkerSpawnPlans = walkerSpawnPlans;
		_walkerPlacementApplication = walkerPlacementApplication;
		_logger = logger;
	}

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		GameTimeService? gameTimeService,
		ThreadPoolManager? threadPoolManager,
		IStaticPlaceableStateService? staticPlaceables,
		ILogger<WorldNpcSpawnService> logger)
		: this(runtimeContext, world, idFactory, gameTimeService, threadPoolManager, null, staticPlaceables, null, null, logger)
	{
	}

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		GameTimeService? gameTimeService,
		ILogger<WorldNpcSpawnService> logger)
		: this(runtimeContext, world, idFactory, gameTimeService, null, null, null, null, null, logger)
	{
	}

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		ILogger<WorldNpcSpawnService> logger)
		: this(runtimeContext, world, idFactory, null, null, null, null, null, null, logger)
	{
	}

	public string Name => "WorldNpcSpawnService";

	public int LoadedCount => Volatile.Read(ref _loadedCount);

	public int SkippedCount => Volatile.Read(ref _skippedCount);

	public int PendingRespawnCount => _pendingRespawns.Count;

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
		CancelPendingRespawns();
		_temporarySpawnObjectIds.Clear();
		_spawnedWorldNpcs.Clear();
		_walkerSpawnPlans?.Clear();
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

		RefreshWalkerSpawnPlansFromStaticData(allowedMaps);
		return new WorldNpcSpawnResult(spawned, skipped);
	}

	public IReadOnlyList<WorldNpcWalkerWorldSpawnPlan> RefreshWalkerSpawnPlans(
		WalkerTemplateTable walkerTemplates,
		WalkerVersionTable walkerVersions,
		IEnumerable<int>? worldIds = null)
	{
		if (_walkerSpawnPlans == null)
			return Array.Empty<WorldNpcWalkerWorldSpawnPlan>();

		var plans = _walkerSpawnPlans.RefreshWorldPlans(
			_world.GetNpcs()
				.OfType<WorldNpc>()
				.ToArray(),
			walkerTemplates,
			walkerVersions,
			worldIds);
		if (_walkerPlacementApplication != null)
		{
			foreach (var plan in plans)
				_walkerPlacementApplication.ApplyActivePlacements(_world, plan.PlacementPlan);
		}

		return plans;
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
		var position = new global::Aion.GameServer.World.WorldPosition(spawn.MapId, spawn.X, spawn.Y, spawn.Z, spawn.Heading);
		var worldNpc = new WorldNpc(
			objectId,
			template.TemplateId,
			template,
			position,
			WorldNpcState.FromTemplateAndSpawn(template, spawn.State),
			WorldNpcAiName.FromTemplateAndSpawn(template, spawn.AiName),
			spawn.RespawnSeconds,
			spawn.StaticId,
			spawn.RandomWalkRange,
			spawn.WalkerId,
			spawn.WalkerIndex,
			spawn.Anchor,
			position);
		if (!_world.TryAddObject(objectId, worldNpc))
		{
			_idFactory.ReleaseId(objectId);
			return null;
		}

		_spawnedWorldNpcs[objectId] = new SpawnedWorldNpcRegistration(spawn, template);
		_staticPlaceables?.SpawnPlaceableObject(worldNpc.Position.WorldId, worldNpc.StaticId);
		return objectId;
	}

	public bool TryDespawnWorldNpc(int objectId)
	{
		// Java parity: controllers/VisibleObjectController.delete removes spawned objects and runs onDespawn cleanup first.
		return TryDespawnWorldNpc(objectId, releaseObjectId: true);
	}

	public bool TryDeleteAndScheduleRespawn(int objectId)
	{
		// Java parity: controllers/VisibleObjectController.deleteAndScheduleRespawn.
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc worldNpc)
			return false;
		if (!_spawnedWorldNpcs.TryGetValue(objectId, out var registration))
			return TryDespawnWorldNpc(objectId);

		var shouldScheduleRespawn = worldNpc.RespawnSeconds > 0 && _threadPoolManager != null && !HasRespawnTask(objectId);
		if (!TryDespawnWorldNpc(objectId, releaseObjectId: !shouldScheduleRespawn))
			return false;

		if (shouldScheduleRespawn)
			ScheduleRespawn(objectId, registration, releaseOldObjectIdBeforeSpawn: true);

		return true;
	}

	public bool TryScheduleRespawn(int objectId)
	{
		// Java parity: services/RespawnService.scheduleRespawn called while an NPC corpse can still be spawned.
		if (!_world.TryGetObject(objectId, out var gameObject)
			|| gameObject is not WorldNpc worldNpc
			|| worldNpc.RespawnSeconds <= 0
			|| _threadPoolManager == null
			|| HasRespawnTask(objectId)
			|| !_spawnedWorldNpcs.TryGetValue(objectId, out var registration))
		{
			return false;
		}

		ScheduleRespawn(objectId, registration, releaseOldObjectIdBeforeSpawn: false);
		return true;
	}

	public bool TryScheduleWorldNpcDeath(int objectId, bool hasRegisteredDrops, TimeSpan? decayDelay = null)
	{
		// Java parity: controllers/NpcController.onDie schedules respawn before delayed decay deletion.
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc worldNpc)
			return false;

		TryScheduleRespawn(objectId);
		_staticPlaceables?.DespawnPlaceableObject(worldNpc.Position.WorldId, worldNpc.StaticId);
		return TryScheduleWorldNpcDecayTask(objectId, hasRegisteredDrops, decayDelay);
	}

	public bool TryScheduleWorldNpcDecayTask(int objectId, bool hasRegisteredDrops, TimeSpan? delay = null)
	{
		// Java parity: services/RespawnService.scheduleDecayTask chooses 2s without drops and 5m with drops.
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc)
			return false;

		var decayDelay = delay ?? (hasRegisteredDrops ? WithDropDecayDelay : ImmediateDecayDelay);
		if (_threadPoolManager == null)
			return TryDespawnWorldNpc(objectId);

		_threadPoolManager.Schedule(
			_ =>
			{
				TryDespawnWorldNpc(objectId);
				return ValueTask.CompletedTask;
			},
			decayDelay <= TimeSpan.Zero ? ImmediateDecayDelay : decayDelay);
		return true;
	}

	public bool HasRespawnTask(int objectId)
	{
		// Java parity: services/RespawnService.hasRespawnTask.
		return _pendingRespawns.ContainsKey(objectId);
	}

	public bool CancelRespawn(int objectId)
	{
		// Java parity: services/RespawnService.cancelRespawn unregisters a pending respawn task.
		if (!_pendingRespawns.TryRemove(objectId, out var pendingRespawn))
			return false;

		pendingRespawn.ScheduledTask?.Cancel();
		if (pendingRespawn.ReleaseOldObjectIdBeforeSpawn)
			_idFactory.ReleaseId(objectId);
		return true;
	}

	private bool TryDespawnWorldNpc(int objectId, bool releaseObjectId)
	{
		if (!_world.TryRemoveObject(objectId, out var gameObject) || gameObject is not WorldNpc worldNpc)
			return false;

		_spawnedWorldNpcs.TryRemove(objectId, out _);
		_staticPlaceables?.DespawnPlaceableObject(worldNpc.Position.WorldId, worldNpc.StaticId);
		if (releaseObjectId)
			_idFactory.ReleaseId(objectId);
		RefreshWalkerSpawnPlansFromStaticData([worldNpc.Position.WorldId]);
		return true;
	}

	private void ScheduleRespawn(int oldObjectId, SpawnedWorldNpcRegistration registration, bool releaseOldObjectIdBeforeSpawn)
	{
		if (_threadPoolManager == null)
			return;

		var pendingRespawn = new PendingWorldNpcRespawn(registration, releaseOldObjectIdBeforeSpawn);
		pendingRespawn.ScheduledTask = _threadPoolManager.Schedule(
			cancellationToken => RunRespawnAsync(oldObjectId, pendingRespawn, cancellationToken),
			TimeSpan.FromSeconds(registration.Spawn.RespawnSeconds));
		if (!_pendingRespawns.TryAdd(oldObjectId, pendingRespawn))
		{
			pendingRespawn.ScheduledTask.Cancel();
			_idFactory.ReleaseId(oldObjectId);
		}
	}

	private ValueTask RunRespawnAsync(int oldObjectId, PendingWorldNpcRespawn pendingRespawn, CancellationToken cancellationToken)
	{
		// Java parity: services/RespawnService.RespawnTask.run unregisters, then SpawnEngine.spawnObject.
		if (!_pendingRespawns.TryGetValue(oldObjectId, out var currentRespawn)
			|| !ReferenceEquals(currentRespawn, pendingRespawn)
			|| !_pendingRespawns.TryRemove(oldObjectId, out _))
		{
			return ValueTask.CompletedTask;
		}

		if (pendingRespawn.ReleaseOldObjectIdBeforeSpawn)
			_idFactory.ReleaseId(oldObjectId);
		if (cancellationToken.IsCancellationRequested)
			return ValueTask.CompletedTask;

		var newObjectId = SpawnNpc(pendingRespawn.Registration.Spawn, pendingRespawn.Registration.Template);
		if (newObjectId.HasValue && pendingRespawn.Registration.Spawn.GroupTemporarySchedule != null)
			_temporarySpawnObjectIds[pendingRespawn.Registration.Spawn] = newObjectId.Value;
		if (newObjectId.HasValue)
			RefreshWalkerSpawnPlansFromStaticData([pendingRespawn.Registration.Spawn.MapId]);
		return ValueTask.CompletedTask;
	}

	private void CancelPendingRespawns()
	{
		foreach (var pair in _pendingRespawns.ToArray())
		{
			if (!_pendingRespawns.TryRemove(pair.Key, out var pendingRespawn))
				continue;

			pendingRespawn.ScheduledTask?.Cancel();
			if (pendingRespawn.ReleaseOldObjectIdBeforeSpawn)
				_idFactory.ReleaseId(pair.Key);
		}
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
				&& TryDespawnWorldNpc(objectId))
			{
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

	private void RefreshWalkerSpawnPlansFromStaticData(IEnumerable<int>? worldIds)
	{
		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null || _walkerSpawnPlans == null)
			return;

		RefreshWalkerSpawnPlans(staticData.WalkerTemplates, staticData.WalkerVersions, worldIds);
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

	private readonly record struct SpawnedWorldNpcRegistration(NpcSpawnSummary Spawn, NpcTemplateSummary Template);

	private sealed class PendingWorldNpcRespawn
	{
		public PendingWorldNpcRespawn(SpawnedWorldNpcRegistration registration, bool releaseOldObjectIdBeforeSpawn)
		{
			Registration = registration;
			ReleaseOldObjectIdBeforeSpawn = releaseOldObjectIdBeforeSpawn;
		}

		public SpawnedWorldNpcRegistration Registration { get; }

		public bool ReleaseOldObjectIdBeforeSpawn { get; }

		public ScheduledTask? ScheduledTask { get; set; }
	}
}

public readonly record struct WorldNpcSpawnResult(int SpawnedCount, int SkippedCount);

public readonly record struct TemporarySpawnHourChangeResult(int SpawnedCount, int DespawnedCount, int SkippedCount);
