using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class LegionWarehouseRuntime
{
	private readonly ConcurrentDictionary<int, int> _currentUserByLegionId = new();

	public bool TrySetInUse(int legionId, int playerObjectId)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(legionId, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(playerObjectId, 0);

		// Java parity: model/team/legion/LegionWarehouse.setInUse compareAndSet(0, playerObjId).
		return _currentUserByLegionId.TryAdd(legionId, playerObjectId);
	}

	public int GetCurrentUser(int legionId)
	{
		if (legionId <= 0)
			return 0;

		// Java parity: model/team/legion/LegionWarehouse.getCurrentUser returns 0 when the warehouse is free.
		return _currentUserByLegionId.TryGetValue(legionId, out var currentUser) ? currentUser : 0;
	}

	public bool UnsetInUse(int legionId, int playerObjectId)
	{
		if (legionId <= 0 || playerObjectId <= 0)
			return false;

		// Java parity: model/team/legion/LegionWarehouse.unsetInUse compareAndSet(playerObjId, 0).
		return _currentUserByLegionId.TryRemove(new KeyValuePair<int, int>(legionId, playerObjectId));
	}
}
