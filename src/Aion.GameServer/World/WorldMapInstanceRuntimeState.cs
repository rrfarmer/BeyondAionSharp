namespace Aion.GameServer.World;

public sealed class WorldMapInstanceRuntimeState
{
	private readonly object _sync = new();
	private readonly HashSet<int> _registeredObjectIds = new();
	private readonly HashSet<int> _playerObjectIds = new();

	public WorldMapInstanceRuntimeState(int instanceId, int ownerId = 0, int maxPlayers = 0)
	{
		// Java parity: WorldMap.getWorldMapInstance/addInstance normalize instance id 0 to 1.
		InstanceId = instanceId == 0 ? 1 : instanceId;
		OwnerId = ownerId;
		MaxPlayers = maxPlayers;
	}

	public int InstanceId { get; }

	public int OwnerId { get; }

	public int MaxPlayers { get; }

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
}
