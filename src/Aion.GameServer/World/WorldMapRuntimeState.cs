using Aion.GameServer.Dataholders;

namespace Aion.GameServer.World;

public sealed class WorldMapRuntimeState
{
	private WorldZoneAttributes _currentFlags;
	private readonly object _instanceLock = new();
	private readonly HashSet<int> _removedInstanceIds = new();
	private readonly Dictionary<int, WorldMapInstanceRuntimeState> _instances = new();

	public WorldMapRuntimeState(WorldMapSummary summary)
	{
		// Java parity: world/WorldMap constructor initializes worldOptions from WorldMapTemplate.flags.
		Summary = summary;
		_currentFlags = summary.Flags;
	}

	public WorldMapSummary Summary { get; }

	public WorldZoneAttributes CurrentFlags => _currentFlags;

	public bool IsFlightAllowed => Summary.IsFlightAllowed(_currentFlags);

	public bool CanGlide => Summary.CanGlide(_currentFlags);

	public bool CanPutKisk => Summary.CanPutKisk(_currentFlags);

	public bool CanRecall => Summary.CanRecall(_currentFlags);

	public bool CanRide => Summary.CanRide(_currentFlags);

	public bool CanFlyRide => Summary.CanFlyRide(_currentFlags);

	public bool IsPvpAllowed => Summary.IsPvpAllowed(_currentFlags);

	public bool IsSameRaceDuelsAllowed => Summary.IsSameRaceDuelsAllowed(_currentFlags);

	public bool IsOtherRaceDuelsAllowed => Summary.IsOtherRaceDuelsAllowed(_currentFlags);

	public bool CanReturnToBattle => Summary.CanReturnToBattle(_currentFlags);

	public bool HasOverriddenOption(WorldZoneAttributes option)
	{
		return Summary.HasOverriddenOption(option, _currentFlags);
	}

	public void SetWorldOption(WorldZoneAttributes option)
	{
		// Java parity: world/WorldMap.setWorldOption.
		_currentFlags = Summary.SetWorldOption(_currentFlags, option);
	}

	public void RemoveWorldOption(WorldZoneAttributes option)
	{
		// Java parity: world/WorldMap.removeWorldOption.
		_currentFlags = Summary.RemoveWorldOption(_currentFlags, option);
	}

	public bool InstanceExists(int instanceId)
	{
		// Java parity: world/WorldMap.getWorldMapInstance normalizes instance id 0 to 1 before lookup.
		var normalizedInstanceId = NormalizeInstanceId(instanceId);
		lock (_instanceLock)
		{
			if (_removedInstanceIds.Contains(normalizedInstanceId))
				return false;
		}

		// Until the full Java WorldMapInstance lifecycle is ported, modeled maps are considered present
		// unless a test/runtime hook explicitly removes an instance id.
		return true;
	}

	public WorldMapInstanceRuntimeState AddWorldMapInstance(int instanceId, int ownerId = 0, int maxPlayers = 0)
	{
		// Java parity: WorldMap.addInstance stores the instance by normalized id.
		var normalizedInstanceId = NormalizeInstanceId(instanceId);
		lock (_instanceLock)
		{
			_removedInstanceIds.Remove(normalizedInstanceId);
			var instance = new WorldMapInstanceRuntimeState(normalizedInstanceId, ownerId, maxPlayers);
			_instances[normalizedInstanceId] = instance;
			return instance;
		}
	}

	public bool TryGetWorldMapInstance(int instanceId, out WorldMapInstanceRuntimeState? instance)
	{
		// Java parity: WorldMap.getWorldMapInstance returns the stored instance object or null.
		var normalizedInstanceId = NormalizeInstanceId(instanceId);
		lock (_instanceLock)
			return _instances.TryGetValue(normalizedInstanceId, out instance);
	}

	public WorldMapInstanceRuntimeState? GetRegisteredInstance(int objectId)
	{
		// Java parity: InstanceService.getRegisteredInstance scans instances and returns the first registered match.
		lock (_instanceLock)
			return _instances.Values.FirstOrDefault(instance => instance.IsRegistered(objectId));
	}

	public void RemoveWorldMapInstance(int instanceId)
	{
		// Java parity: world/WorldMap.removeWorldMapInstance removes the normalized instance id from the map's instance table.
		var normalizedInstanceId = NormalizeInstanceId(instanceId);
		lock (_instanceLock)
		{
			_removedInstanceIds.Add(normalizedInstanceId);
			_instances.Remove(normalizedInstanceId);
		}
	}

	private static int NormalizeInstanceId(int instanceId)
	{
		return instanceId == 0 ? 1 : instanceId;
	}
}
