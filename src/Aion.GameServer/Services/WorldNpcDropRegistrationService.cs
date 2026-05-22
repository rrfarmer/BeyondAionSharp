using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class WorldNpcDropRegistrationService : IWorldNpcDropRegistrationLookup
{
	private readonly ConcurrentDictionary<int, IReadOnlyList<WorldNpcDropItem>> _currentDropMap = new();
	private readonly ConcurrentDictionary<int, WorldNpcDropRegistration> _dropRegistrationMap = new();

	public void RegisterDrop(
		int npcObjectId,
		int looterObjectId,
		IEnumerable<WorldNpcDropItem>? drops = null,
		IEnumerable<int>? allowedLooterObjectIds = null)
	{
		// Java parity: services/drop/DropRegistrationService.registerDrop populates currentDropMap and dropRegistrationMap after NPC death.
		var dropItems = drops?.ToArray() ?? Array.Empty<WorldNpcDropItem>();
		_currentDropMap[npcObjectId] = dropItems;

		var allowedLooters = allowedLooterObjectIds?.ToHashSet() ?? [];
		if (looterObjectId > 0)
			allowedLooters.Add(looterObjectId);
		_dropRegistrationMap[npcObjectId] = new WorldNpcDropRegistration(npcObjectId, allowedLooters);
	}

	public bool HasRegisteredDrops(int npcObjectId)
	{
		// Java parity: RespawnService.scheduleDecayTask checks DropRegistrationService.currentDropMap for a non-empty drop set.
		return _currentDropMap.TryGetValue(npcObjectId, out var drops) && drops.Count > 0;
	}

	public IReadOnlyList<WorldNpcDropItem> GetCurrentDrops(int npcObjectId)
	{
		return _currentDropMap.TryGetValue(npcObjectId, out var drops) ? drops : Array.Empty<WorldNpcDropItem>();
	}

	public bool TryGetCurrentDrop(int npcObjectId, int itemIndex, out WorldNpcDropItem? dropItem)
	{
		// Java parity: DropService.requestDropItem scans the synchronized currentDropMap set by drop index.
		if (_currentDropMap.TryGetValue(npcObjectId, out var drops))
		{
			dropItem = drops.FirstOrDefault(drop => drop.Index == itemIndex);
			return dropItem != null;
		}

		dropItem = null;
		return false;
	}

	public IReadOnlyList<WorldNpcDropItem> ApplyCollectedCount(int npcObjectId, int itemIndex, long remainingCount)
	{
		// Java parity: DropService.requestDropItem updates DropItem.count and removes it when count reaches zero.
		if (!_currentDropMap.TryGetValue(npcObjectId, out var drops))
			return Array.Empty<WorldNpcDropItem>();

		var updatedDrops = drops
			.Select(drop => drop.Index == itemIndex ? drop with { Count = remainingCount } : drop)
			.Where(drop => drop.Count > 0)
			.ToArray();
		_currentDropMap[npcObjectId] = updatedDrops;
		return updatedDrops;
	}

	public bool TryGetRegistration(int npcObjectId, out WorldNpcDropRegistration? registration)
	{
		return _dropRegistrationMap.TryGetValue(npcObjectId, out registration);
	}

	public bool UnregisterDrop(int npcObjectId)
	{
		// Java parity: services/drop/DropService.unregisterDrop removes both drop maps on NPC despawn.
		var removedCurrent = _currentDropMap.TryRemove(npcObjectId, out _);
		var removedRegistration = _dropRegistrationMap.TryRemove(npcObjectId, out _);
		return removedCurrent || removedRegistration;
	}
}

public sealed record WorldNpcDropItem(
	int Index,
	int ItemId,
	long Count,
	IReadOnlySet<int>? PlayerObjectIds = null,
	int OptionalSocket = 0,
	int NpcObjectId = 0,
	bool IsDistributeItem = false)
{
	public int LootEffectId
	{
		get
		{
			// Java parity: model/drop/DropItem.getLootEffectId.
			return ItemId switch
			{
				166020000 or 166020001 or 166020002 or 166020003 => 1003,
				168000034 or 168000035 or 168000073 or 168000074 or 168000117 or 168000118 or 168000120 or 168000121
					or 168000161 or 168000162 or 168000164 or 168000165 or 168000213 or 168000216 or 168000223
					or 168000228 or 168000230 or 168000233 or 168000240 or 168000245 => 1003,
				188053083 => 1003,
				188053547 or 188053548 or 188053646 or 188053647 => 1002,
				190100004 or 190100052 => 1003,
				_ => 0,
			};
		}
	}

	public bool CanViewDropItem(int playerObjectId)
	{
		// Java parity: model/drop/DropItem.canViewDropItem.
		return PlayerObjectIds == null || PlayerObjectIds.Count == 0 || PlayerObjectIds.Contains(playerObjectId);
	}

	public bool IsOnlyPossibleLooter(int playerObjectId)
	{
		// Java parity: model/drop/DropItem.isOnlyPossibleLooter.
		return PlayerObjectIds is { Count: 1 } && PlayerObjectIds.Contains(playerObjectId);
	}
}

public sealed class WorldNpcDropRegistration
{
	private readonly HashSet<int> _allowedLooters;
	private readonly object _sync = new();

	public WorldNpcDropRegistration(int npcObjectId, IEnumerable<int> allowedLooters)
	{
		// Java parity: model/gameobjects/DropNpc tracks allowed looters and free-for-all state.
		NpcObjectId = npcObjectId;
		_allowedLooters = allowedLooters.ToHashSet();
	}

	public int NpcObjectId { get; }

	public bool IsFreeForAll { get; private set; }

	public long RemainingDecayTimeMillis { get; set; }

	public IReadOnlySet<int> AllowedLooters => _allowedLooters;

	public int? LootingPlayerObjectId { get; private set; }

	public bool IsBeingLooted => LootingPlayerObjectId != null;

	public bool IsAllowedToLoot(int playerObjectId)
	{
		lock (_sync)
		{
			return IsFreeForAll || _allowedLooters.Contains(playerObjectId);
		}
	}

	public bool TryBeginLooting(int playerObjectId, out int? currentLootingPlayerObjectId)
	{
		// Java parity: model/gameobjects/DropNpc.setLootingPlayer plus isBeingLooted guard in DropService.requestDropList.
		lock (_sync)
		{
			currentLootingPlayerObjectId = LootingPlayerObjectId;
			if (currentLootingPlayerObjectId != null && currentLootingPlayerObjectId.Value != playerObjectId)
				return false;

			LootingPlayerObjectId = playerObjectId;
			currentLootingPlayerObjectId = playerObjectId;
			return true;
		}
	}

	public bool ClearLootingPlayer(int playerObjectId)
	{
		// Java parity: DropService.closeDropList only clears the looting player that opened the corpse.
		lock (_sync)
		{
			if (LootingPlayerObjectId != playerObjectId)
				return false;

			LootingPlayerObjectId = null;
			return true;
		}
	}

	public void StartFreeForAll()
	{
		// Java parity: model/gameobjects/DropNpc.startFreeForAll clears explicit looters and opens looting.
		lock (_sync)
		{
			IsFreeForAll = true;
			_allowedLooters.Clear();
		}
	}
}
