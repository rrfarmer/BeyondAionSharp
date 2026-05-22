using System.Collections.Concurrent;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils.IdFactory;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class RiftService
{
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly RiftManagerService _riftManager;
	private readonly GameWorld _world;
	private readonly IDFactory _idFactory;
	private readonly GameServerOptions _options;
	private readonly Func<DateTimeOffset> _nowProvider;
	private readonly Func<bool> _nextBoolean;
	private readonly Func<int, int, int> _randomInclusive;
	private readonly ConcurrentDictionary<int, RiftLocationState> _activeRifts = new();

	public RiftService(
		GameServerRuntimeContext runtimeContext,
		RiftManagerService riftManager,
		GameWorld world,
		IDFactory idFactory,
		GameServerOptions? options = null,
		Func<DateTimeOffset>? nowProvider = null,
		Func<bool>? nextBoolean = null,
		Func<int, int, int>? randomInclusive = null)
	{
		_runtimeContext = runtimeContext;
		_riftManager = riftManager;
		_world = world;
		_idFactory = idFactory;
		_options = options ?? new GameServerOptions();
		_nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
		_nextBoolean = nextBoolean ?? (() => Random.Shared.Next(2) == 0);
		_randomInclusive = randomInclusive ?? ((minInclusive, maxInclusive) => Random.Shared.Next(minInclusive, maxInclusive + 1));
	}

	public int ActiveRiftCount => _activeRifts.Count;

	public bool IsValidId(int id)
	{
		// Java parity: services/RiftService.isValidId accepts either one rift id or one owning world id.
		var locations = _runtimeContext.DataManager?.StaticData?.RiftLocations;
		if (locations == null)
			return false;

		return IsRiftId(id)
			? locations.Contains(id)
			: locations.GetLocationsForWorld(id).Count > 0;
	}

	public bool IsRiftOpened(int riftId)
	{
		// Java parity: services/RiftService.isRiftOpened checks the active rift map by rift id.
		return _activeRifts.ContainsKey(riftId);
	}

	public RiftLocationState? GetActiveRift(int riftId)
	{
		return _activeRifts.GetValueOrDefault(riftId);
	}

	public IReadOnlyList<RiftLocationState> GetActiveRifts()
	{
		return _activeRifts.Values.OrderBy(rift => rift.Location.Id).ToArray();
	}

	public bool TryGetPortalByMasterObjectId(int objectId, out RiftPortalState? portal)
	{
		// Java parity: CM_SHOW_DIALOG reaches RVController through the targeted master rift NPC owner.
		foreach (var rift in _activeRifts.Values)
		{
			portal = rift.Portal;
			if (portal?.MasterNpc.ObjectId == objectId)
				return true;
		}

		portal = null;
		return false;
	}

	public RiftServiceResult OpenRifts(int id, bool guards)
	{
		// Java parity: services/RiftService.openRifts can open one rift id or every closed rift for a world id.
		if (!TryResolveLocations(id, out var locations, out var failure))
			return failure;

		var opened = new List<RiftLocationState>();
		var spawnResults = new List<RiftSpawnResult>();
		foreach (var location in locations)
		{
			if (_activeRifts.ContainsKey(location.Id))
				continue;

			var state = new RiftLocationState(location) { Opened = true, GuardsRequested = guards };
			if (!_activeRifts.TryAdd(location.Id, state))
				continue;

			if (guards)
			{
				foreach (var guardNpc in SpawnGuardNpcs(location))
					state.AddSpawned(guardNpc);
			}

			var spawnResult = _riftManager.SpawnRift(location.Id, guards);
			state.Definition = spawnResult.Definition;
			foreach (var npc in spawnResult.SpawnedNpcs)
				state.AddSpawned(npc);
			state.Portal = CreatePortalState(spawnResult, guards);

			opened.Add(state);
			spawnResults.Add(spawnResult);
		}

		return opened.Count == 0
			? RiftServiceResult.NotSucceeded(RiftServiceStatus.AlreadyOpen)
			: RiftServiceResult.Completed(RiftServiceStatus.Opened, opened, spawnResults);
	}

	public RiftServiceResult CloseRifts(int id)
	{
		// Java parity: services/RiftService.closeRifts can close one rift id or every open rift for a world id.
		if (!TryResolveLocations(id, out var locations, out var failure))
			return failure;

		var closed = new List<RiftLocationState>();
		foreach (var location in locations)
		{
			if (!_activeRifts.TryRemove(location.Id, out var state))
				continue;

			state.Opened = false;
			foreach (var npc in state.GetSpawnedSnapshot())
			{
				_riftManager.RemoveSpawnedRift(npc);
				if (_world.TryRemoveObject(npc.ObjectId, out var removedObject) && removedObject is WorldNpc)
					_idFactory.ReleaseId(npc.ObjectId);
			}

			state.ClearSpawned();
			state.Portal = null;
			closed.Add(state);
		}

		return closed.Count == 0
			? RiftServiceResult.NotSucceeded(RiftServiceStatus.NotOpen)
			: RiftServiceResult.Completed(RiftServiceStatus.Closed, closed, Array.Empty<RiftSpawnResult>());
	}

	public void CloseAutoCloseableRifts(int worldId)
	{
		// Java parity: services/RiftService.closeAutoCloseableRifts only closes open locations marked auto_closeable for the given world id.
		var locations = _runtimeContext.DataManager?.StaticData?.RiftLocations
			.GetLocationsForWorld(worldId)
			.Where(location => location.AutoCloseable)
			.ToArray();
		if (locations == null || locations.Length == 0)
			return;

		foreach (var location in locations)
			CloseRifts(location.Id);
	}

	public RiftPrepareOpeningResult PrepareRiftOpening(int worldId, bool guards)
	{
		// Java parity: services/RiftService.prepareRiftOpening filters possible locations, randomly caps them, then opens each chosen rift.
		if (worldId is not 210070000 and not 220080000 && !guards && _nextBoolean())
			return RiftPrepareOpeningResult.NotOpened(RiftPrepareOpeningStatus.SkippedByRandom);

		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
			return RiftPrepareOpeningResult.NotOpened(RiftPrepareOpeningStatus.MissingStaticData);

		var possibleLocations = staticData.RiftLocations
			.GetLocationsForWorld(worldId)
			.Where(location => location.AutoCloseable && (guards ? location.HasSpawns : !location.HasSpawns))
			.ToList();
		if (possibleLocations.Count == 0)
			return RiftPrepareOpeningResult.NotOpened(RiftPrepareOpeningStatus.NoLocations);

		var maxCount = guards
			? 1
			: worldId is 210050000 or 220070000
				? 3
				: 4;
		var count = _randomInclusive(1, maxCount);
		if (count < 1)
			count = 1;
		while (possibleLocations.Count > count)
		{
			var index = _randomInclusive(0, possibleLocations.Count - 1);
			if (index < 0 || index >= possibleLocations.Count)
				index = 0;
			possibleLocations.RemoveAt(index);
		}

		var opened = new List<RiftLocationState>();
		var spawnResults = new List<RiftSpawnResult>();
		foreach (var location in possibleLocations)
		{
			var result = OpenRifts(location.Id, guards);
			if (!result.Succeeded)
				continue;

			opened.AddRange(result.Locations);
			spawnResults.AddRange(result.SpawnResults);
		}

		return opened.Count == 0
			? RiftPrepareOpeningResult.NotOpened(RiftPrepareOpeningStatus.AlreadyOpen)
			: RiftPrepareOpeningResult.Opened(opened, spawnResults);
	}

	public bool UpdateSpawned(int oldObjectId, WorldNpc respawn)
	{
		// Java parity: services/RiftService.updateSpawned replaces a respawned rift-owned object id in its RiftLocation spawned map.
		foreach (var rift in _activeRifts.Values)
		{
			if (rift.ReplaceSpawned(oldObjectId, respawn))
				return true;
		}

		return false;
	}

	private bool TryResolveLocations(
		int id,
		out IReadOnlyList<RiftLocationSummary> locations,
		out RiftServiceResult failure)
	{
		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
		{
			locations = Array.Empty<RiftLocationSummary>();
			failure = RiftServiceResult.NotSucceeded(RiftServiceStatus.MissingStaticData);
			return false;
		}

		if (IsRiftId(id))
		{
			var location = staticData.RiftLocations.GetLocation(id);
			if (location == null)
			{
				locations = Array.Empty<RiftLocationSummary>();
				failure = RiftServiceResult.NotSucceeded(RiftServiceStatus.InvalidId);
				return false;
			}

			locations = [location];
			failure = RiftServiceResult.NotSucceeded(RiftServiceStatus.InvalidId);
			return true;
		}

		locations = staticData.RiftLocations.GetLocationsForWorld(id);
		if (locations.Count == 0)
		{
			failure = RiftServiceResult.NotSucceeded(RiftServiceStatus.InvalidId);
			return false;
		}

		failure = RiftServiceResult.NotSucceeded(RiftServiceStatus.InvalidId);
		return true;
	}

	private IReadOnlyList<WorldNpc> SpawnGuardNpcs(RiftLocationSummary location)
	{
		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
			return Array.Empty<WorldNpc>();

		var spawned = new List<WorldNpc>();
		foreach (var guardSpawn in staticData.NpcRiftSpawns.GetSpawnsForRift(location.Id))
		{
			// Java parity: RiftService.openRifts spawns every SpawnTemplate from SpawnsData.getRiftSpawnsByLocId before the portal pair.
			var template = staticData.NpcTemplates.GetNpcTemplate(guardSpawn.NpcId);
			if (template == null)
				continue;
			if (TrySpawnRiftOwnedNpc(guardSpawn, template, out var npc))
				spawned.Add(npc);
		}

		return spawned;
	}

	private bool TrySpawnRiftOwnedNpc(
		NpcRiftSpawnSummary spawn,
		NpcTemplateSummary template,
		out WorldNpc npc)
	{
		var objectId = _idFactory.NextId();
		var position = new global::Aion.GameServer.World.WorldPosition(spawn.MapId, spawn.X, spawn.Y, spawn.Z, spawn.Heading);
		npc = new WorldNpc(
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
		if (_world.TryAddObject(objectId, npc))
			return true;

		_idFactory.ReleaseId(objectId);
		return false;
	}

	private static bool IsRiftId(int id)
	{
		return id < 10000;
	}

	private RiftPortalState? CreatePortalState(RiftSpawnResult spawnResult, bool guardsRequested)
	{
		// Java parity: controllers/RVController constructor copies RiftEnum entry/level/type metadata and computes despawn time.
		if (!spawnResult.Spawned || spawnResult.Definition == null || spawnResult.SpawnedNpcs.Count < 2)
			return null;

		var definition = spawnResult.Definition;
		var slave = spawnResult.SpawnedNpcs
			.FirstOrDefault(npc => string.Equals(npc.Anchor, definition.SlaveAnchor, StringComparison.Ordinal))
			?? spawnResult.SpawnedNpcs[0];
		var master = spawnResult.SpawnedNpcs
			.FirstOrDefault(npc => string.Equals(npc.Anchor, definition.MasterAnchor, StringComparison.Ordinal))
			?? spawnResult.SpawnedNpcs[^1];
		var durationHours = definition.IsVortex
			? _options.Custom.VortexDuration
			: _options.Custom.RiftDuration;
		var despawnTime = _nowProvider().ToUnixTimeSeconds() + durationHours * 3600;
		return new RiftPortalState(
			definition,
			master,
			slave,
			guardsRequested,
			despawnTime);
	}
}

public sealed class RiftLocationState
{
	private readonly ConcurrentDictionary<int, WorldNpc> _spawned = new();

	public RiftLocationState(RiftLocationSummary location)
	{
		Location = location;
	}

	public RiftLocationSummary Location { get; }

	public bool Opened { get; internal set; }

	public bool GuardsRequested { get; internal set; }

	public RiftDefinition? Definition { get; internal set; }

	public RiftPortalState? Portal { get; internal set; }

	public int SpawnedCount => _spawned.Count;

	public IReadOnlyList<WorldNpc> Spawned => GetSpawnedSnapshot();

	internal void AddSpawned(WorldNpc npc)
	{
		// Java parity: model/rift/RiftLocation.addSpawned stores every rift-owned spawned object by object id.
		_spawned[npc.ObjectId] = npc;
	}

	internal IReadOnlyList<WorldNpc> GetSpawnedSnapshot()
	{
		return _spawned.Values.OrderBy(npc => npc.ObjectId).ToArray();
	}

	internal bool ReplaceSpawned(int oldObjectId, WorldNpc respawn)
	{
		// Java parity: model/rift/RiftLocation.replaceSpawned swaps the old object id for the new respawn object.
		if (!_spawned.TryRemove(oldObjectId, out _))
			return false;

		_spawned[respawn.ObjectId] = respawn;
		return true;
	}

	internal void ClearSpawned()
	{
		// Java parity: services/RiftService.closeRift clears RiftLocation.spawned after despawn/cancel-respawn work.
		_spawned.Clear();
	}
}

public sealed class RiftPortalState
{
	private readonly HashSet<int> _passedPlayerObjectIds = [];

	public RiftPortalState(
		RiftDefinition definition,
		WorldNpc masterNpc,
		WorldNpc slaveNpc,
		bool guardsRequested,
		long despawnTimeUnixSeconds)
	{
		Definition = definition;
		MasterNpc = masterNpc;
		SlaveNpc = slaveNpc;
		GuardsRequested = guardsRequested;
		DespawnTimeUnixSeconds = despawnTimeUnixSeconds;
	}

	public RiftDefinition Definition { get; }

	public WorldNpc MasterNpc { get; }

	public WorldNpc SlaveNpc { get; }

	public bool GuardsRequested { get; }

	public long DespawnTimeUnixSeconds { get; }

	public int MaxEntries => Definition.Entries;

	public int MinLevel => Definition.MinLevel;

	public int MaxLevel => Definition.MaxLevel;

	public string DestinationRace => Definition.DestinationRace;

	public bool IsVortex => Definition.IsVortex;

	public bool IsVolatile => Definition.CanBeVolatile && GuardsRequested;

	public bool IsInvasion => Definition.IsInvasionRift;

	public int UsedEntries { get; private set; }

	public int PassedPlayerCount => _passedPlayerObjectIds.Count;

	public bool AddPassedPlayer(int playerObjectId)
	{
		// Java parity: controllers/RVController.passedPlayers stores vortex passers by player object id.
		return _passedPlayerObjectIds.Add(playerObjectId);
	}

	public int GetRemainTime(DateTimeOffset now)
	{
		// Java parity: controllers/RVController.getRemainTime returns despawnTimeSeconds - currentUnixSeconds.
		return (int)(DespawnTimeUnixSeconds - now.ToUnixTimeSeconds());
	}

	public void SyncPassed(bool usePassedPlayerCount, int passedPlayerCount = 0)
	{
		// Java parity: controllers/RVController.syncPassed mutates usedEntries before rift info sync.
		UsedEntries = usePassedPlayerCount ? passedPlayerCount : UsedEntries + 1;
	}
}

public sealed record RiftServiceResult(
	bool Succeeded,
	RiftServiceStatus Status,
	IReadOnlyList<RiftLocationState> Locations,
	IReadOnlyList<RiftSpawnResult> SpawnResults)
{
	public static RiftServiceResult Completed(
		RiftServiceStatus status,
		IReadOnlyList<RiftLocationState> locations,
		IReadOnlyList<RiftSpawnResult> spawnResults)
	{
		return new RiftServiceResult(true, status, locations, spawnResults);
	}

	public static RiftServiceResult NotSucceeded(RiftServiceStatus status)
	{
		return new RiftServiceResult(
			false,
			status,
			Array.Empty<RiftLocationState>(),
			Array.Empty<RiftSpawnResult>());
	}
}

public enum RiftServiceStatus
{
	Opened,
	Closed,
	InvalidId,
	MissingStaticData,
	AlreadyOpen,
	NotOpen,
}

public sealed record RiftPrepareOpeningResult(
	bool Succeeded,
	RiftPrepareOpeningStatus Status,
	IReadOnlyList<RiftLocationState> Locations,
	IReadOnlyList<RiftSpawnResult> SpawnResults)
{
	public static RiftPrepareOpeningResult Opened(
		IReadOnlyList<RiftLocationState> locations,
		IReadOnlyList<RiftSpawnResult> spawnResults)
	{
		return new RiftPrepareOpeningResult(true, RiftPrepareOpeningStatus.Opened, locations, spawnResults);
	}

	public static RiftPrepareOpeningResult NotOpened(RiftPrepareOpeningStatus status)
	{
		return new RiftPrepareOpeningResult(
			false,
			status,
			Array.Empty<RiftLocationState>(),
			Array.Empty<RiftSpawnResult>());
	}
}

public enum RiftPrepareOpeningStatus
{
	Opened,
	SkippedByRandom,
	NoLocations,
	AlreadyOpen,
	MissingStaticData,
}
