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
	private readonly WorldNpcRandomWalkService? _randomWalking;
	private readonly IWorldNpcDropRegistrationLookup? _dropRegistrationLookup;
	private readonly Func<int, WorldNpc, bool>? _respawnedNpcCallback;
	private readonly WorldNpcAiStateService? _npcAiStates;
	private readonly Action<WorldNpc>? _npcLifeStatsInitialize;
	private readonly Action<int>? _npcLifeStatsClear;
	private readonly CreaturePvpZoneCounterService? _creaturePvpZoneCounterService;
	private readonly ILogger<WorldNpcSpawnService> _logger;
	private readonly ConcurrentDictionary<TemporarySpawnKey, int> _temporarySpawnObjectIds = new();
	private readonly ConcurrentDictionary<int, SpawnedWorldNpcRegistration> _spawnedWorldNpcs = new();
	private readonly ConcurrentDictionary<int, PendingWorldNpcRespawn> _pendingRespawns = new();
	private readonly ConcurrentDictionary<int, PendingWorldNpcDecay> _pendingDecays = new();
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
		ILogger<WorldNpcSpawnService> logger,
		WorldNpcRandomWalkService? randomWalking = null,
		IWorldNpcDropRegistrationLookup? dropRegistrationLookup = null,
		Func<int, WorldNpc, bool>? respawnedNpcCallback = null,
		WorldNpcAiStateService? npcAiStates = null,
		Action<WorldNpc>? npcLifeStatsInitialize = null,
		Action<int>? npcLifeStatsClear = null,
		CreaturePvpZoneCounterService? creaturePvpZoneCounterService = null)
	{
		_runtimeContext = runtimeContext;
		_world = world;
		_idFactory = idFactory;
		_gameTimeService = gameTimeService;
		_threadPoolManager = threadPoolManager;
		_connectionRegistry = connectionRegistry;
		_staticPlaceables = staticPlaceables;
		_randomWalking = randomWalking;
		_dropRegistrationLookup = dropRegistrationLookup;
		_respawnedNpcCallback = respawnedNpcCallback;
		_npcAiStates = npcAiStates;
		_npcLifeStatsInitialize = npcLifeStatsInitialize;
		_npcLifeStatsClear = npcLifeStatsClear;
		_creaturePvpZoneCounterService = creaturePvpZoneCounterService;
		_logger = logger;
	}

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		GameTimeService? gameTimeService,
		ThreadPoolManager? threadPoolManager,
		IStaticPlaceableStateService? staticPlaceables,
		ILogger<WorldNpcSpawnService> logger,
		WorldNpcAiStateService? npcAiStates = null,
		Action<WorldNpc>? npcLifeStatsInitialize = null,
		Action<int>? npcLifeStatsClear = null)
		: this(
			runtimeContext,
			world,
			idFactory,
			gameTimeService,
			threadPoolManager,
			null,
			staticPlaceables,
			logger,
			npcAiStates: npcAiStates,
			npcLifeStatsInitialize: npcLifeStatsInitialize,
			npcLifeStatsClear: npcLifeStatsClear)
	{
	}

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		GameTimeService? gameTimeService,
		ILogger<WorldNpcSpawnService> logger)
		: this(runtimeContext, world, idFactory, gameTimeService, null, null, null, logger)
	{
	}

	public WorldNpcSpawnService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IDFactory idFactory,
		ILogger<WorldNpcSpawnService> logger)
		: this(runtimeContext, world, idFactory, null, null, null, null, logger)
	{
	}

	public string Name => "WorldNpcSpawnService";

	public int LoadedCount => Volatile.Read(ref _loadedCount);

	public int SkippedCount => Volatile.Read(ref _skippedCount);

	public int PendingRespawnCount => _pendingRespawns.Count;

	public int PendingDecayCount => _pendingDecays.Count;

	public async ValueTask InitAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: GameServer.main calls Aion.GameServer.SpawnEngine.SpawnEngine.spawnAll after DataManager and HousingService startup.
		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
		{
			_logger.LogWarning("Static data is not loaded; skipping NPC world spawn load");
			return;
		}

		var result = SpawnWorldNpcs(
			staticData.NpcSpawns,
			staticData.NpcTemplates,
			staticData.WorldMaps.Where(map => !map.IsInstance).Select(map => map.MapId),
			staticData.ItemTemplates,
			_gameTimeService?.GameMinutes ?? 0,
			DateTimeOffset.Now.DayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId: 0,
			instanceId: 1,
			changedMapIds: null,
			cancellationToken: cancellationToken);
		await StartNpcWalkingForWorldsAsync(
			staticData.WorldMaps.Where(map => !map.IsInstance).Select(map => map.MapId),
			cancellationToken);
		if (_gameTimeService != null)
			_gameTimeService.HourChanged += OnGameHourChangedAsync;
		Volatile.Write(ref _loadedCount, result.SpawnedCount);
		Volatile.Write(ref _skippedCount, result.SkippedCount);
		_logger.LogInformation(
			"Loaded {SpawnedCount} regular NPC spawns into world visibility; skipped {SkippedCount} unsupported spawns",
			result.SpawnedCount,
			result.SkippedCount);
	}

	public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
	{
		if (_gameTimeService != null)
			_gameTimeService.HourChanged -= OnGameHourChangedAsync;
		CancelPendingRespawns();
		_temporarySpawnObjectIds.Clear();
		_spawnedWorldNpcs.Clear();
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
			staticObjectTemplates: null,
			_gameTimeService?.GameMinutes ?? 0,
			DateTimeOffset.Now.DayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId: 0,
			instanceId: 1,
			changedMapIds: null,
			cancellationToken: cancellationToken);
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
			staticObjectTemplates: null,
			gameMinutes,
			serverDayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId: difficultId,
			instanceId: 1,
			changedMapIds: null,
			cancellationToken: cancellationToken);
	}

	public WorldNpcSpawnResult SpawnWorldNpcsForInstance(
		global::Aion.GameServer.World.WorldMapInstanceRuntimeState instance,
		int mapId,
		NpcSpawnTable spawns,
		NpcTemplateTable npcTemplates,
		StaticDoorTable? staticDoors = null,
		ItemTemplateTable? itemTemplates = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: Aion.GameServer.SpawnEngine.SpawnEngine.spawnInstance(instance, difficultId, ownerId) filters by difficulty and spawns into instance.getInstanceId().
		ArgumentNullException.ThrowIfNull(instance);

		SpawnStaticDoorsForInstance(instance, mapId, staticDoors);
		return SpawnWorldNpcs(
			new NpcSpawnTable(spawns.GetSpawnsForMap(mapId)),
			npcTemplates,
			[mapId],
			itemTemplates,
			_gameTimeService?.GameMinutes ?? 0,
			DateTimeOffset.Now.DayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId: instance.DifficultyId,
			instanceId: instance.InstanceId,
			changedMapIds: null,
			cancellationToken: cancellationToken);
	}

	public int UnregisterTemporarySpawnsForInstance(int mapId, int instanceId)
	{
		// Java parity: spawnengine/TemporarySpawnEngine.onInstanceDestroy removes spawned temporary objects
		// from tracking for the destroyed WorldMapInstance before InstanceService deletes visible objects.
		var removed = 0;
		foreach (var key in _temporarySpawnObjectIds.Keys.ToArray())
		{
			if (key.Spawn.MapId != mapId || key.InstanceId != instanceId)
				continue;

			if (_temporarySpawnObjectIds.TryRemove(key, out _))
				removed++;
		}

		return removed;
	}

	public int DeleteNonPlayerObjectsForInstance(int mapId, int instanceId)
	{
		// Java parity: InstanceService.destroyInstance deletes every non-player VisibleObject in the destroyed instance.
		var deleted = 0;
		foreach (var npc in _world.GetNpcs(mapId).OfType<WorldNpc>().Where(npc => npc.Position.InstanceId == instanceId).ToArray())
		{
			if (TryDespawnWorldNpc(npc.ObjectId))
				deleted++;
		}

		foreach (var staticObject in _world.GetStaticObjects(mapId).Where(staticObject => staticObject.Position.InstanceId == instanceId).ToArray())
		{
			if (TryDespawnStaticObject(staticObject.ObjectId))
				deleted++;
		}

		return deleted;
	}

	private void SpawnStaticDoorsForInstance(
		global::Aion.GameServer.World.WorldMapInstanceRuntimeState instance,
		int mapId,
		StaticDoorTable? staticDoors)
	{
		if (_staticPlaceables == null || staticDoors == null)
			return;

		// Java parity: StaticDoorSpawnManager.spawnTemplate(instance) seeds GeoService door state for every static door.
		foreach (var door in staticDoors.GetStaticDoors(mapId))
			_staticPlaceables.SetDoorState(mapId, instance.InstanceId, door.StaticId, door.IsOpen);
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
			staticObjectTemplates: null,
			gameMinutes,
			serverDayOfWeek,
			TemporarySpawnEvaluationMode.HourlySpawn,
			includeAlwaysOn: false,
			includeTemporary: true,
			difficultId: 0,
			instanceId: 1,
			changedMapIds: changedMapIds,
			cancellationToken: cancellationToken);

		await StartNpcWalkingForWorldsAsync(changedMapIds, cancellationToken);
		await RefreshNpcVisibilityAsync(changedMapIds, cancellationToken);
		return new TemporarySpawnHourChangeResult(result.SpawnedCount, despawned, result.SkippedCount);
	}

	private WorldNpcSpawnResult SpawnWorldNpcs(
		NpcSpawnTable spawns,
		NpcTemplateTable npcTemplates,
		IEnumerable<int>? allowedMapIds,
		ItemTemplateTable? staticObjectTemplates,
		int gameMinutes,
		DayOfWeek serverDayOfWeek,
		TemporarySpawnEvaluationMode temporarySpawnMode,
		bool includeAlwaysOn,
		bool includeTemporary,
		byte difficultId,
		int instanceId,
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

			if (IsStaticObjectSpawn(groupKey))
			{
				if (staticObjectTemplates == null)
				{
					skipped += groupSpawns.Length;
					continue;
				}

				var staticTemplate = staticObjectTemplates.GetItemTemplate(groupKey.NpcId);
				if (staticTemplate == null)
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
					&& groupSpawns.Any(spawn => HasTemporarySpawnObject(spawn, instanceId)))
				{
					skipped += groupSpawns.Length;
					continue;
				}

				foreach (var spawn in SelectActivePoolSpots(groupSpawns))
				{
					cancellationToken.ThrowIfCancellationRequested();
					var objectId = SpawnStaticObject(spawn, staticTemplate, instanceId);
					if (objectId.HasValue)
					{
						TrackTemporarySpawnObject(spawn, instanceId, objectId.Value);
						changedMapIds?.Add(spawn.MapId);
					}
					else
					{
						skipped++;
					}
				}

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
				&& groupSpawns.Any(spawn => HasTemporarySpawnObject(spawn, instanceId)))
			{
				skipped += groupSpawns.Length;
				continue;
			}

			// Java parity: Aion.GameServer.SpawnEngine.SpawnEngine.spawnInstance checks spot-level TemporarySpawn.isInSpawnTime in the non-pool branch.
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
				if (IsTemporarySpawn(spawn) && HasTemporarySpawnObject(spawn, instanceId))
				{
					skipped++;
					continue;
				}

				var objectId = SpawnNpc(spawn, template, instanceId);
				if (objectId.HasValue)
				{
					spawned++;
					TrackTemporarySpawnObject(spawn, instanceId, objectId.Value);
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
			staticObjectTemplates: null,
			_gameTimeService?.GameMinutes ?? 0,
			DateTimeOffset.Now.DayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId: 0,
			instanceId: 1,
			changedMapIds: null,
			cancellationToken: cancellationToken);
	}

	private int? SpawnNpc(NpcSpawnSummary spawn, NpcTemplateSummary template, int instanceId = 1)
	{
		var objectId = _idFactory.NextId();
		var position = new global::Aion.GameServer.World.WorldPosition(spawn.MapId, spawn.X, spawn.Y, spawn.Z, spawn.Heading, instanceId);
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

		_spawnedWorldNpcs[objectId] = new SpawnedWorldNpcRegistration(spawn, template, instanceId);
		RevalidateNpcCreaturePvpZones(worldNpc);
		// Java parity: spawnengine/VisibleObjectSpawner creates a fresh Npc/NpcAI/NpcLifeStats runtime on spawn.
		_npcAiStates?.Clear(objectId);
		_npcLifeStatsClear?.Invoke(objectId);
		if (worldNpc.Template.MaxHp > 0)
			_npcLifeStatsInitialize?.Invoke(worldNpc);
		_staticPlaceables?.SpawnPlaceableObject(worldNpc.Position.WorldId, worldNpc.StaticId);
		return objectId;
	}

	private int? SpawnStaticObject(NpcSpawnSummary spawn, ItemTemplateSummary template, int instanceId)
	{
		var objectId = _idFactory.NextId();
		var position = new global::Aion.GameServer.World.WorldPosition(spawn.MapId, spawn.X, spawn.Y, spawn.Z, spawn.Heading, instanceId);
		var staticObject = new WorldStaticObject(
			objectId,
			template.TemplateId,
			template,
			position,
			spawn.StaticId,
			position);
		if (!_world.TryAddObject(objectId, staticObject))
		{
			_idFactory.ReleaseId(objectId);
			return null;
		}

		_staticPlaceables?.SpawnPlaceableObject(staticObject.Position.WorldId, staticObject.StaticId);
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

	public bool TryDespawnStaticPlaceableForWorldNpc(int objectId)
	{
		// Java parity: controllers/NpcController.onDie despawns static placeables after RespawnService.scheduleDecayTask.
		if (_staticPlaceables == null
			|| !_world.TryGetObject(objectId, out var gameObject)
			|| gameObject is not WorldNpc worldNpc
			|| worldNpc.StaticId <= 0)
		{
			return false;
		}

		_staticPlaceables.DespawnPlaceableObject(worldNpc.Position.WorldId, worldNpc.StaticId);
		return true;
	}

	public bool TryScheduleWorldNpcDeath(int objectId, TimeSpan? decayDelay = null)
	{
		// Java parity: controllers/NpcController.onDie -> RespawnService.scheduleDecayTask reads the current drop map.
		return TryScheduleWorldNpcDeath(objectId, HasRegisteredDrops(objectId), decayDelay);
	}

	public bool TryScheduleWorldNpcDecayTask(int objectId, bool hasRegisteredDrops, TimeSpan? delay = null)
	{
		// Java parity: services/RespawnService.scheduleDecayTask chooses 2s without drops and 5m with drops.
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc)
			return false;

		var decayDelay = delay ?? SelectWorldNpcDecayDelay(hasRegisteredDrops);
		if (_threadPoolManager == null)
			return TryDespawnWorldNpc(objectId);

		decayDelay = decayDelay <= TimeSpan.Zero ? ImmediateDecayDelay : decayDelay;
		var pendingDecay = new PendingWorldNpcDecay(DateTimeOffset.UtcNow + decayDelay);
		pendingDecay.ScheduledTask = _threadPoolManager.Schedule(
			cancellationToken =>
			{
				RunDecayAsync(objectId, pendingDecay, cancellationToken);
				return ValueTask.CompletedTask;
			},
			decayDelay);
		if (!_pendingDecays.TryAdd(objectId, pendingDecay))
		{
			pendingDecay.ScheduledTask.Cancel();
			return false;
		}
		return true;
	}

	public bool TryScheduleWorldNpcDecayTask(int objectId, TimeSpan? delay = null)
	{
		// Java parity: services/RespawnService.scheduleDecayTask(Npc) derives delay from registered drops.
		return TryScheduleWorldNpcDecayTask(objectId, HasRegisteredDrops(objectId), delay);
	}

	public TimeSpan SelectWorldNpcDecayDelay(int objectId)
	{
		return SelectWorldNpcDecayDelay(HasRegisteredDrops(objectId));
	}

	public static TimeSpan SelectWorldNpcDecayDelay(bool hasRegisteredDrops)
	{
		// Java parity: services/RespawnService.IMMEDIATE_DECAY / WITH_DROP_DECAY.
		return hasRegisteredDrops ? WithDropDecayDelay : ImmediateDecayDelay;
	}

	public bool HasRespawnTask(int objectId)
	{
		// Java parity: services/RespawnService.hasRespawnTask.
		return _pendingRespawns.ContainsKey(objectId);
	}

	public bool HasDecayTask(int objectId)
	{
		// Java parity: controllers/CreatureController TaskId.DECAY lookup used by DropService.
		return _pendingDecays.ContainsKey(objectId);
	}

	public TimeSpan? CancelDecay(int objectId)
	{
		// Java parity: DropService.requestDropList cancels TaskId.DECAY and stores remaining delay.
		if (!_pendingDecays.TryRemove(objectId, out var pendingDecay))
			return null;

		pendingDecay.ScheduledTask?.Cancel();
		var remaining = pendingDecay.DueAt - DateTimeOffset.UtcNow;
		return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
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

	private bool HasRegisteredDrops(int objectId)
	{
		return _dropRegistrationLookup?.HasRegisteredDrops(objectId) == true;
	}

	private void RunDecayAsync(int objectId, PendingWorldNpcDecay pendingDecay, CancellationToken cancellationToken)
	{
		// Java parity: RespawnService.scheduleDecayTask delayed delete removes the DECAY task before deleting the corpse.
		if (!_pendingDecays.TryGetValue(objectId, out var currentDecay)
			|| !ReferenceEquals(currentDecay, pendingDecay)
			|| !_pendingDecays.TryRemove(objectId, out _)
			|| cancellationToken.IsCancellationRequested)
		{
			return;
		}

		TryDespawnWorldNpc(objectId);
	}

	private bool TryDespawnWorldNpc(int objectId, bool releaseObjectId)
	{
		if (!_world.TryRemoveObject(objectId, out var gameObject) || gameObject is not WorldNpc worldNpc)
			return false;

		_spawnedWorldNpcs.TryRemove(objectId, out _);
		_npcAiStates?.Clear(objectId);
		_npcLifeStatsClear?.Invoke(objectId);
		ClearNpcCreaturePvpZones(objectId);
		if (_pendingDecays.TryRemove(objectId, out var pendingDecay))
			pendingDecay.ScheduledTask?.Cancel();
		_staticPlaceables?.DespawnPlaceableObject(worldNpc.Position.WorldId, worldNpc.StaticId);
		if (releaseObjectId)
			_idFactory.ReleaseId(objectId);
		return true;
	}

	private void RevalidateNpcCreaturePvpZones(WorldNpc npc)
	{
		// Java parity: World.spawnObject -> MapRegion.revalidateZones enters zone memberships for spawned creatures.
		var staticData = _runtimeContext.DataManager?.StaticData;
		CreaturePvpZoneRevalidationService.Revalidate(
			npc.ObjectId,
			npc.Position,
			staticData?.CreaturePvpZones,
			_creaturePvpZoneCounterService);
	}

	private void RevalidateNpcCreaturePvpZones(int objectId)
	{
		// Java parity: spawnengine/InstanceWalkerFormations may move the selected walker variant before it starts walking.
		if (_world.TryGetObject(objectId, out var gameObject) && gameObject is WorldNpc npc)
			RevalidateNpcCreaturePvpZones(npc);
	}

	private void ClearNpcCreaturePvpZones(int objectId)
	{
		// Java parity: World.despawn -> MapRegion.revalidateZones on an unspawned Creature leaves tracked zones.
		_creaturePvpZoneCounterService?.ClearCounters(objectId);
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
		// Java parity: services/RespawnService.RespawnTask.run unregisters, then Aion.GameServer.SpawnEngine.SpawnEngine.spawnObject.
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
		if (newObjectId.HasValue)
			TrackTemporarySpawnObject(
				pendingRespawn.Registration.Spawn,
				pendingRespawn.Registration.InstanceId,
				newObjectId.Value);
		if (newObjectId.HasValue)
		{
			NotifyNpcRespawned(oldObjectId, newObjectId.Value);
		}
		return ValueTask.CompletedTask;
	}

	private void NotifyNpcRespawned(int oldObjectId, int newObjectId)
	{
		// Java parity: services/RespawnService.RespawnTask.respawn notifies RiftService.updateSpawned after Aion.GameServer.SpawnEngine.SpawnEngine.spawnObject.
		if (_respawnedNpcCallback == null)
			return;
		if (!_world.TryGetObject(newObjectId, out var gameObject) || gameObject is not WorldNpc respawn)
			return;

		_respawnedNpcCallback(oldObjectId, respawn);
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
			var key = pair.Key;
			var spawn = key.Spawn;
			var schedule = spawn.SpotTemporarySchedule ?? spawn.GroupTemporarySchedule;
			if (schedule == null || !schedule.CanDespawn(gameMinutes, serverDayOfWeek))
				continue;

			if (_temporarySpawnObjectIds.TryRemove(key, out var objectId)
				&& TryDespawnTemporaryObject(objectId))
			{
				changedMapIds.Add(spawn.MapId);
				despawned++;
			}
		}

		return despawned;
	}

	private bool TryDespawnTemporaryObject(int objectId)
	{
		if (!_world.TryGetObject(objectId, out var gameObject))
			return false;

		if (gameObject is WorldNpc)
			return TryDespawnWorldNpc(objectId);

		return gameObject is WorldStaticObject && TryDespawnStaticObject(objectId);
	}

	private bool TryDespawnStaticObject(int objectId)
	{
		if (!_world.TryRemoveObject(objectId, out var gameObject) || gameObject is not WorldStaticObject staticObject)
			return false;

		_staticPlaceables?.DespawnPlaceableObject(staticObject.Position.WorldId, staticObject.StaticId);
		_idFactory.ReleaseId(objectId);
		return true;
	}

	private void TrackTemporarySpawnObject(NpcSpawnSummary spawn, int instanceId, int objectId)
	{
		if (IsTemporarySpawn(spawn))
			_temporarySpawnObjectIds[new TemporarySpawnKey(spawn, instanceId)] = objectId;
	}

	private bool HasTemporarySpawnObject(NpcSpawnSummary spawn, int instanceId)
	{
		return _temporarySpawnObjectIds.ContainsKey(new TemporarySpawnKey(spawn, instanceId));
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

	private async Task StartNpcWalkingForWorldsAsync(
		IEnumerable<int> worldIds,
		CancellationToken cancellationToken)
	{
		if (_randomWalking == null)
			return;

		foreach (var worldId in worldIds.Distinct())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (_randomWalking != null)
				await _randomWalking.StartWorldRandomWalkingAsync(worldId, cancellationToken);
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

	private static bool IsStaticObjectSpawn(NpcSpawnGroupKey spawn)
	{
		// Java parity: SpawnEngine handler type STATIC delegates to StaticObjectSpawnManager.spawnTemplate.
		return string.Equals(spawn.Handler, "STATIC", StringComparison.OrdinalIgnoreCase);
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

	private static bool IsTemporarySpawn(NpcSpawnSummary spawn)
	{
		return spawn.GroupTemporarySchedule != null || spawn.SpotTemporarySchedule != null;
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

	private readonly record struct TemporarySpawnKey(NpcSpawnSummary Spawn, int InstanceId);

	private readonly record struct SpawnedWorldNpcRegistration(NpcSpawnSummary Spawn, NpcTemplateSummary Template, int InstanceId);

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

	private sealed class PendingWorldNpcDecay
	{
		public PendingWorldNpcDecay(DateTimeOffset dueAt)
		{
			DueAt = dueAt;
		}

		public DateTimeOffset DueAt { get; }

		public ScheduledTask? ScheduledTask { get; set; }
	}
}

public readonly record struct WorldNpcSpawnResult(int SpawnedCount, int SkippedCount);

public readonly record struct TemporarySpawnHourChangeResult(int SpawnedCount, int DespawnedCount, int SkippedCount);
