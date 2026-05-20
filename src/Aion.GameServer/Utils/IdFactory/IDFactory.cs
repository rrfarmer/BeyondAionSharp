namespace Aion.GameServer.Utils.IdFactory;

public sealed class IDFactory
{
	private const int InvalidIdBitMask = 0b0010011111100110101111111111100;
	private const int InvalidIdBitCheck = 0b0000000000000000001100101010100;

	private readonly HashSet<int> _usedIds = [];
	private readonly object _lock = new();
	private int _nextMinId = 1;

	public IDFactory(IEnumerable<int>? usedIds = null)
	{
		LockIdsUnlocked([0]);
		if (usedIds != null)
			LockIdsUnlocked(usedIds);
	}

	public int NextId()
	{
		// Java parity: utils/idfactory/IDFactory.nextId.
		lock (_lock)
		{
			var id = NextValidId(_nextMinId);
			_usedIds.Add(id);
			_nextMinId = unchecked(id + 1);
			return id;
		}
	}

	public bool ReleaseId(int id)
	{
		// Java parity: utils/idfactory/IDFactory.releaseId.
		lock (_lock)
		{
			if (!_usedIds.Remove(id))
				return false;

			if (id > 0 && (id < _nextMinId || _nextMinId <= 0))
				_nextMinId = id;

			return true;
		}
	}

	public void LockIds(IEnumerable<int> ids)
	{
		// Java parity: utils/idfactory/IDFactory.lockIds during startup preload.
		lock (_lock)
		{
			LockIdsUnlocked(ids);
		}
	}

	public int GetUsedCount()
	{
		lock (_lock)
		{
			return _usedIds.Count;
		}
	}

	public static bool IsInvalidId(int id)
	{
		// Java parity: IDFactory.INVALID_ID_BIT_MASK / INVALID_ID_BITCHECK.
		return (id & InvalidIdBitMask) == InvalidIdBitCheck;
	}

	private int NextValidId(int searchIndex)
	{
		// Java parity: utils/idfactory/IDFactory.nextValidId.
		var id = Math.Max(searchIndex, 1);
		while (id > 0)
		{
			if (!_usedIds.Contains(id) && !IsInvalidId(id))
				return id;

			if (id == int.MaxValue)
				break;

			id++;
		}

		throw new IDFactoryException("All IDs are used, please clear your database");
	}

	private void LockIdsUnlocked(IEnumerable<int> ids)
	{
		foreach (var id in ids)
		{
			if (!_usedIds.Add(id))
				throw new IDFactoryException($"ID {id} is already taken, fatal error!!!");
		}
	}
}
