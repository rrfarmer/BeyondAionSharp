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
	private readonly WorldNpcWalkerRouteWalkingService? _walkerRouteWalking;
	private readonly WorldNpcRandomWalkService? _randomWalking;
	private readonly IWorldNpcDropRegistrationLookup? _dropRegistrationLookup;
	private readonly Func<int, WorldNpc, bool>? _respawnedNpcCallback;
	private readonly ILogger<WorldNpcSpawnService> _logger;
	private readonly ConcurrentDictionary<NpcSpawnSummary, int> _temporarySpawnObjectIds = new();
	private readonly ConcurrentDictionary<int, SpawnedWorldNpcRegistration> _spawnedWorldNpcs = new();
	private readonly ConcurrentDictionary<int, WorldNpc> _inactiveWalkerVariants = new();
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
		IWorldNpcWalkerSpawnPlanCacheService? walkerSpawnPlans,
		WorldNpcWalkerPlacementApplicationService? walkerPlacementApplication,
		ILogger<WorldNpcSpawnService> logger,
		WorldNpcWalkerRouteWalkingService? walkerRouteWalking = null,
		WorldNpcRandomWalkService? randomWalking = null,
		IWorldNpcDropRegistrationLookup? dropRegistrationLookup = null,
		Func<int, WorldNpc, bool>? respawnedNpcCallback = null)
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
		_walkerRouteWalking = walkerRouteWalking;
		_randomWalking = randomWalking;
		_dropRegistrationLookup = dropRegistrationLookup;
		_respawnedNpcCallback = respawnedNpcCallback;
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

	public int PendingDecayCount => _pendingDecays.Count;

	public int InactiveWalkerVariantCount => _inactiveWalkerVariants.Count;

	public bool TryGetInactiveWalkerVariant(int objectId, out WorldNpc? npc)
	{
		return _inactiveWalkerVariants.TryGetValue(objectId, out npc);
	}

	public bool TrySwapInactiveWalkerVariant(int activeObjectId, int inactiveObjectId)
	{
		// Java parity: spawnengine/InstanceWalkerFormations.changeWalker spawns one not-spawned version variant, then despawns the current one.
		if (!_inactiveWalkerVariants.TryGetValue(inactiveObjectId, out var inactiveNpc)
			|| !_world.TryGetObject(activeObjectId, out var activeObject)
			|| activeObject is not WorldNpc activeNpc)
		{
			return false;
		}

		var spawnedInactiveNpc = inactiveNpc with { Position = inactiveNpc.SpawnLocation };
		if (!_world.TryAddObject(inactiveObjectId, spawnedInactiveNpc))
			return false;

		if (!_inactiveWalkerVariants.TryRemove(inactiveObjectId, out _))
		{
			_world.TryRemoveObject(inactiveObjectId, out _);
			return false;
		}

		if (!_world.TryRemoveObject(activeObjectId, out _))
		{
			_inactiveWalkerVariants[inactiveObjectId] = inactiveNpc;
			_world.TryRemoveObject(inactiveObjectId, out _);
			return false;
		}

		_inactiveWalkerVariants[activeObjectId] = activeNpc;
		_staticPlaceables?.SpawnPlaceableObject(spawnedInactiveNpc.Position.WorldId, spawnedInactiveNpc.StaticId);
		_staticPlaceables?.DespawnPlaceableObject(activeNpc.Position.WorldId, activeNpc.StaticId);
		return true;
	}

	public bool TrySwapInactiveWalkerFormationVariant(
		IReadOnlyCollection<int> activeObjectIds,
		IReadOnlyCollection<int> inactiveObjectIds)
	{
		// Java parity: spawnengine/InstanceWalkerFormations.changeCluster spawns one not-spawned WalkerGroup, then despawns the current group.
		var activeIds = activeObjectIds.Distinct().ToArray();
		var inactiveIds = inactiveObjectIds.Distinct().ToArray();
		if (activeIds.Length == 0
			|| inactiveIds.Length == 0
			|| activeIds.Length != activeObjectIds.Count
			|| inactiveIds.Length != inactiveObjectIds.Count
			|| activeIds.Intersect(inactiveIds).Any())
		{
			return false;
		}

		if (!TryGetLiveWalkerNpcs(activeIds, out var activeNpcs)
			|| !TryGetInactiveWalkerNpcs(inactiveIds, out var inactiveNpcs))
		{
			return false;
		}

		var worldId = inactiveNpcs[0].SpawnLocation.WorldId;
		if (inactiveNpcs.Any(npc => npc.SpawnLocation.WorldId != worldId)
			|| activeNpcs.Any(npc => npc.Position.WorldId != worldId))
		{
			return false;
		}

		if (!TryGetInactiveFormationVariantPlacements(worldId, inactiveIds, inactiveNpcs, out var inactivePlacements))
			return false;

		var spawnedInactiveNpcs = new List<(int ObjectId, WorldNpc Npc)>(inactivePlacements.Count);
		foreach (var placement in inactivePlacements)
		{
			var inactiveNpc = inactiveNpcs.Single(npc => npc.ObjectId == placement.ObjectId);
			var spawnedInactiveNpc = inactiveNpc with
			{
				Position = new global::Aion.GameServer.World.WorldPosition(
					inactiveNpc.SpawnLocation.WorldId,
					placement.X,
					placement.Y,
					placement.Z,
					placement.Heading),
			};
			if (!_world.TryAddObject(placement.ObjectId, spawnedInactiveNpc))
			{
				RollBackSpawnedInactiveVariants(spawnedInactiveNpcs);
				return false;
			}

			spawnedInactiveNpcs.Add((placement.ObjectId, spawnedInactiveNpc));
		}

		var removedInactiveNpcs = new List<WorldNpc>(inactiveIds.Length);
		foreach (var inactiveId in inactiveIds)
		{
			if (!_inactiveWalkerVariants.TryRemove(inactiveId, out var removedInactiveNpc))
			{
				RollBackInactiveVariantSpawn(spawnedInactiveNpcs, removedInactiveNpcs);
				return false;
			}

			removedInactiveNpcs.Add(removedInactiveNpc);
		}

		var removedActiveNpcs = new List<(int ObjectId, WorldNpc Npc)>(activeNpcs.Count);
		foreach (var activeNpc in activeNpcs)
		{
			if (!_world.TryRemoveObject(activeNpc.ObjectId, out var removedActiveObject)
				|| removedActiveObject is not WorldNpc removedActiveNpc)
			{
				RollBackFormationSwap(spawnedInactiveNpcs, removedInactiveNpcs, removedActiveNpcs);
				return false;
			}

			removedActiveNpcs.Add((activeNpc.ObjectId, removedActiveNpc));
		}

		foreach (var (objectId, activeNpc) in removedActiveNpcs)
			_inactiveWalkerVariants[objectId] = activeNpc;
		foreach (var (_, inactiveNpc) in spawnedInactiveNpcs)
			_staticPlaceables?.SpawnPlaceableObject(inactiveNpc.Position.WorldId, inactiveNpc.StaticId);
		foreach (var (_, activeNpc) in removedActiveNpcs)
			_staticPlaceables?.DespawnPlaceableObject(activeNpc.Position.WorldId, activeNpc.StaticId);
		return true;
	}

	public async ValueTask InitAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: GameServer.main calls SpawnEngine.spawnAll after DataManager and HousingService startup.
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
			_gameTimeService?.GameMinutes ?? 0,
			DateTimeOffset.Now.DayOfWeek,
			TemporarySpawnEvaluationMode.Startup,
			includeAlwaysOn: true,
			includeTemporary: true,
			difficultId: 0,
			changedMapIds: null,
			cancellationToken);
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
		_inactiveWalkerVariants.Clear();
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

		await StartNpcWalkingForWorldsAsync(changedMapIds, cancellationToken);
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
				.Concat(_inactiveWalkerVariants.Values)
				.ToArray(),
			walkerTemplates,
			walkerVersions,
			worldIds);
		if (_walkerPlacementApplication != null)
		{
			foreach (var plan in plans)
			{
				var result = _walkerPlacementApplication.ApplyActivePlacements(_world, plan.PlacementPlan);
				foreach (var objectId in result.UpdatedObjectIds)
					_inactiveWalkerVariants.TryRemove(objectId, out _);
				foreach (var inactiveNpc in result.RemovedInactiveNpcs)
				{
					_inactiveWalkerVariants[inactiveNpc.ObjectId] = inactiveNpc;
					_staticPlaceables?.DespawnPlaceableObject(inactiveNpc.Position.WorldId, inactiveNpc.StaticId);
				}
			}
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
		if (_pendingDecays.TryRemove(objectId, out var pendingDecay))
			pendingDecay.ScheduledTask?.Cancel();
		_inactiveWalkerVariants.TryRemove(objectId, out _);
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
		{
			RefreshWalkerSpawnPlansFromStaticData([pendingRespawn.Registration.Spawn.MapId]);
			NotifyNpcRespawned(oldObjectId, newObjectId.Value);
		}
		return ValueTask.CompletedTask;
	}

	private void NotifyNpcRespawned(int oldObjectId, int newObjectId)
	{
		// Java parity: services/RespawnService.RespawnTask.respawn notifies RiftService.updateSpawned after SpawnEngine.spawnObject.
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

	private async Task StartNpcWalkingForWorldsAsync(
		IEnumerable<int> worldIds,
		CancellationToken cancellationToken)
	{
		if (_randomWalking == null && _walkerRouteWalking == null)
			return;

		foreach (var worldId in worldIds.Distinct())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (_randomWalking != null)
				await _randomWalking.StartWorldRandomWalkingAsync(worldId, cancellationToken);
			if (_walkerRouteWalking != null)
				await _walkerRouteWalking.StartWorldRouteWalkingAsync(worldId, cancellationToken);
		}
	}

	private bool TryGetLiveWalkerNpcs(IReadOnlyList<int> objectIds, out IReadOnlyList<WorldNpc> npcs)
	{
		var result = new List<WorldNpc>(objectIds.Count);
		foreach (var objectId in objectIds)
		{
			if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			{
				npcs = Array.Empty<WorldNpc>();
				return false;
			}

			result.Add(npc);
		}

		npcs = result;
		return true;
	}

	private bool TryGetInactiveWalkerNpcs(IReadOnlyList<int> objectIds, out IReadOnlyList<WorldNpc> npcs)
	{
		var result = new List<WorldNpc>(objectIds.Count);
		foreach (var objectId in objectIds)
		{
			if (!_inactiveWalkerVariants.TryGetValue(objectId, out var npc))
			{
				npcs = Array.Empty<WorldNpc>();
				return false;
			}

			result.Add(npc);
		}

		npcs = result;
		return true;
	}

	private bool TryGetInactiveFormationVariantPlacements(
		int worldId,
		IReadOnlyList<int> inactiveObjectIds,
		IReadOnlyList<WorldNpc> inactiveNpcs,
		out IReadOnlyList<WorldNpcWalkerPlacement> placements)
	{
		placements = Array.Empty<WorldNpcWalkerPlacement>();
		var worldPlan = _walkerSpawnPlans?.GetWorldPlan(worldId);
		if (worldPlan == null)
			return false;

		var inactiveIdSet = inactiveObjectIds.ToHashSet();
		foreach (var formation in worldPlan.Organization.FormationVariants.Values.SelectMany(variants => variants))
		{
			if (!inactiveIdSet.SetEquals(formation.Members.Select(member => member.ObjectId)))
				continue;

			placements = formation.Members
				.Select(member =>
				{
					var sourceNpc = inactiveNpcs.Single(npc => npc.ObjectId == member.ObjectId);
					var spawnLocation = sourceNpc.SpawnLocation;
					return new WorldNpcWalkerPlacement(
						member.ObjectId,
						member.TemplateId,
						formation.RouteId,
						IsFormationMember: true,
						member.X,
						member.Y,
						spawnLocation.Z,
						spawnLocation.Heading);
				})
				.ToArray();
			return placements.Count == inactiveObjectIds.Count;
		}

		return false;
	}

	private void RollBackSpawnedInactiveVariants(IReadOnlyList<(int ObjectId, WorldNpc Npc)> spawnedInactiveNpcs)
	{
		foreach (var (objectId, _) in spawnedInactiveNpcs)
			_world.TryRemoveObject(objectId, out _);
	}

	private void RollBackInactiveVariantSpawn(
		IReadOnlyList<(int ObjectId, WorldNpc Npc)> spawnedInactiveNpcs,
		IReadOnlyList<WorldNpc> removedInactiveNpcs)
	{
		RollBackSpawnedInactiveVariants(spawnedInactiveNpcs);
		foreach (var inactiveNpc in removedInactiveNpcs)
			_inactiveWalkerVariants[inactiveNpc.ObjectId] = inactiveNpc;
	}

	private void RollBackFormationSwap(
		IReadOnlyList<(int ObjectId, WorldNpc Npc)> spawnedInactiveNpcs,
		IReadOnlyList<WorldNpc> removedInactiveNpcs,
		IReadOnlyList<(int ObjectId, WorldNpc Npc)> removedActiveNpcs)
	{
		RollBackInactiveVariantSpawn(spawnedInactiveNpcs, removedInactiveNpcs);
		foreach (var (objectId, activeNpc) in removedActiveNpcs)
			_world.TryAddObject(objectId, activeNpc);
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
