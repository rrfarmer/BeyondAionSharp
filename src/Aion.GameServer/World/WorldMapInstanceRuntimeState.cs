namespace Aion.GameServer.World;

public sealed class WorldMapInstanceRuntimeState
{
	private readonly object _sync = new();
	private readonly HashSet<int> _registeredObjectIds = new();
	private readonly HashSet<int> _playerObjectIds = new();
	private readonly HashSet<int> _questIds = new();
	private bool _hasPendingNearbyQuestRefresh;
	private bool _instanceCreateNotified;

	public WorldMapInstanceRuntimeState(
		int instanceId,
		int ownerId = 0,
		int maxPlayers = 0,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null)
	{
		// Java parity: WorldMap.getWorldMapInstance/addInstance normalize instance id 0 to 1.
		InstanceId = instanceId == 0 ? 1 : instanceId;
		OwnerId = ownerId;
		MaxPlayers = maxPlayers;
		DifficultyId = difficultyId;
		InstanceHandler = instanceHandler ?? GeneralInstanceLifecycleHandler.Instance;
	}

	public int InstanceId { get; }

	public int OwnerId { get; }

	public int MaxPlayers { get; }

	public byte DifficultyId { get; }

	public IInstanceLifecycleHandler InstanceHandler { get; }

	public bool InstanceCreateNotified
	{
		get
		{
			lock (_sync)
				return _instanceCreateNotified;
		}
	}

	public bool IsPersonal => OwnerId != 0;

	public WorldPosition? StartPosition { get; private set; }

	public int RegisteredCount
	{
		get
		{
			lock (_sync)
				return _registeredObjectIds.Count;
		}
	}

	public int PlayerCount
	{
		get
		{
			lock (_sync)
				return _playerObjectIds.Count;
		}
	}

	public bool IsFull => MaxPlayers > 0 && PlayerCount >= MaxPlayers;

	public IReadOnlySet<int> RegisteredObjectIds
	{
		get
		{
			lock (_sync)
				return _registeredObjectIds.ToHashSet();
		}
	}

	public IReadOnlySet<int> QuestIds
	{
		get
		{
			lock (_sync)
				return _questIds.ToHashSet();
		}
	}

	public bool HasPendingNearbyQuestRefresh
	{
		get
		{
			lock (_sync)
				return _hasPendingNearbyQuestRefresh;
		}
	}

	public int? RegisteredTeamId { get; private set; }

	public void RegisterTeamId(int teamId)
	{
		// Java parity: WorldMapInstance.registerTeam stores one team id and also registers that id for instance lookup.
		lock (_sync)
		{
			if (RegisteredTeamId.HasValue)
				throw new InvalidOperationException($"A team for instance {InstanceId} is already registered.");

			RegisteredTeamId = teamId;
			_registeredObjectIds.Add(teamId);
		}
	}

	public void Register(int objectId)
	{
		// Java parity: WorldMapInstance.register stores player or team object ids in registeredObjects.
		lock (_sync)
			_registeredObjectIds.Add(objectId);
	}

	public bool IsRegistered(int objectId)
	{
		// Java parity: WorldMapInstance.isRegistered.
		lock (_sync)
			return _registeredObjectIds.Contains(objectId);
	}

	public WorldPosition SetStartPositionIfMissing(WorldPosition startPosition)
	{
		// Java parity: PortalService.transfer initializes WorldMapInstance.startPos only when it is null.
		lock (_sync)
		{
			StartPosition ??= startPosition;
			return StartPosition.Value;
		}
	}

	public void AddPlayer(int objectId)
	{
		// Java parity: WorldMapInstance.addObject tracks players in worldMapPlayers for getPlayerCount/isFull.
		lock (_sync)
			_playerObjectIds.Add(objectId);
	}

	public void RemovePlayer(int objectId)
	{
		// Java parity: WorldMapInstance.removeObject removes players from worldMapPlayers.
		lock (_sync)
			_playerObjectIds.Remove(objectId);
	}

	public bool RegisterQuestStartIds(IEnumerable<int> questIds)
	{
		// Java parity: WorldMapInstance.addObject(Npc) adds QuestNpc.getOnQuestStart ids to questIds.
		var addedAny = false;
		lock (_sync)
		{
			foreach (var questId in questIds)
				addedAny |= _questIds.Add(questId);
		}

		return addedAny;
	}

	public WorldMapNearbyQuestRefreshSchedulePlan RegisterQuestStartIdsAndPlanNearbyRefresh(IEnumerable<int> questIds)
	{
		// Java parity breadcrumb: WorldMapInstance.addObject(Npc) schedules one delayed updateNearbyQuests task
		// only when at least one QuestNpc.onQuestStart id is newly added and no refresh task is already pending.
		lock (_sync)
		{
			var newlyRegistered = new List<int>();
			foreach (var questId in questIds)
			{
				if (_questIds.Add(questId))
					newlyRegistered.Add(questId);
			}

			if (newlyRegistered.Count == 0)
				return WorldMapNearbyQuestRefreshSchedulePlan.NoNewQuestIds(_questIds);

			if (_hasPendingNearbyQuestRefresh)
				return WorldMapNearbyQuestRefreshSchedulePlan.AlreadyPending(newlyRegistered, _questIds);

			_hasPendingNearbyQuestRefresh = true;
			return WorldMapNearbyQuestRefreshSchedulePlan.Scheduled(newlyRegistered, _questIds);
		}
	}

	public bool CompletePendingNearbyQuestRefresh()
	{
		// Java parity: the delayed task clears updateNearbyQuestsTask before iterating players.
		lock (_sync)
		{
			if (!_hasPendingNearbyQuestRefresh)
				return false;

			_hasPendingNearbyQuestRefresh = false;
			return true;
		}
	}

	public bool NotifyInstanceCreated()
	{
		lock (_sync)
		{
			if (_instanceCreateNotified)
				return false;

			_instanceCreateNotified = true;
		}

		// Java parity: services/instance/InstanceService.getNextAvailableInstance calls instance.getInstanceHandler().onInstanceCreate().
		InstanceHandler.OnInstanceCreate(this);
		return true;
	}
}

public sealed record WorldMapNearbyQuestRefreshSchedulePlan(
	WorldMapNearbyQuestRefreshScheduleStatus Status,
	TimeSpan? Delay,
	IReadOnlySet<int> NewlyRegisteredQuestIds,
	IReadOnlySet<int> WorldQuestIds,
	string JavaSource)
{
	public bool WouldScheduleTask => Status == WorldMapNearbyQuestRefreshScheduleStatus.Scheduled;

	public static WorldMapNearbyQuestRefreshSchedulePlan Scheduled(
		IEnumerable<int> newlyRegisteredQuestIds,
		IEnumerable<int> worldQuestIds)
	{
		return new WorldMapNearbyQuestRefreshSchedulePlan(
			WorldMapNearbyQuestRefreshScheduleStatus.Scheduled,
			TimeSpan.FromMilliseconds(1500),
			newlyRegisteredQuestIds.ToHashSet(),
			worldQuestIds.ToHashSet(),
			"WorldMapInstance.addObject(Npc) -> ThreadPoolManager.schedule(updateNearbyQuests, 1500)");
	}

	public static WorldMapNearbyQuestRefreshSchedulePlan AlreadyPending(
		IEnumerable<int> newlyRegisteredQuestIds,
		IEnumerable<int> worldQuestIds)
	{
		return new WorldMapNearbyQuestRefreshSchedulePlan(
			WorldMapNearbyQuestRefreshScheduleStatus.AlreadyPending,
			null,
			newlyRegisteredQuestIds.ToHashSet(),
			worldQuestIds.ToHashSet(),
			"WorldMapInstance.addObject(Npc) suppresses duplicate updateNearbyQuestsTask while pending");
	}

	public static WorldMapNearbyQuestRefreshSchedulePlan NoNewQuestIds(IEnumerable<int> worldQuestIds)
	{
		return new WorldMapNearbyQuestRefreshSchedulePlan(
			WorldMapNearbyQuestRefreshScheduleStatus.NoNewQuestIds,
			null,
			new HashSet<int>(),
			worldQuestIds.ToHashSet(),
			"WorldMapInstance.addObject(Npc) schedules only after questIds.add(id) succeeds");
	}
}

public enum WorldMapNearbyQuestRefreshScheduleStatus
{
	Scheduled,
	AlreadyPending,
	NoNewQuestIds,
}
