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
	IReadOnlySet<int>? PlayerObjectIds = null)
{
	public bool CanViewDropItem(int playerObjectId)
	{
		// Java parity: model/drop/DropItem.canViewDropItem.
		return PlayerObjectIds == null || PlayerObjectIds.Count == 0 || PlayerObjectIds.Contains(playerObjectId);
	}
}

public sealed class WorldNpcDropRegistration
{
	private readonly HashSet<int> _allowedLooters;

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

	public bool IsAllowedToLoot(int playerObjectId)
	{
		return IsFreeForAll || _allowedLooters.Contains(playerObjectId);
	}

	public void StartFreeForAll()
	{
		// Java parity: model/gameobjects/DropNpc.startFreeForAll clears explicit looters and opens looting.
		IsFreeForAll = true;
		_allowedLooters.Clear();
	}
}
