using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class PlayerKiskRegistry
{
	private readonly ConcurrentDictionary<int, PlayerKiskOwnership> _ownerKisks = new();
	private readonly ConcurrentDictionary<int, int> _ownersByKiskId = new();

	public PlayerKiskOwnership RegisterKisk(int ownerObjectId, int kiskObjectId, int npcId)
	{
		// Java parity: services/KiskService.regKisk stores the spawned Kisk by creator object id.
		var ownership = new PlayerKiskOwnership(kiskObjectId, ownerObjectId, npcId);
		_ownerKisks.AddOrUpdate(
			ownerObjectId,
			ownership,
			(_, previous) =>
			{
				// Java normally removes the old kisk before replacing ownership; clear this reverse link defensively.
				if (previous.KiskObjectId != kiskObjectId)
					_ownersByKiskId.TryRemove(previous.KiskObjectId, out _);
				return ownership;
			});
		_ownersByKiskId[kiskObjectId] = ownerObjectId;
		return ownership;
	}

	public bool HaveKisk(int ownerObjectId)
	{
		// Java parity: services/KiskService.haveKisk.
		return _ownerKisks.ContainsKey(ownerObjectId);
	}

	public PlayerKiskOwnership? GetOwnerKisk(int ownerObjectId)
	{
		return _ownerKisks.GetValueOrDefault(ownerObjectId);
	}

	public bool RemoveKisk(int kiskObjectId)
	{
		// Java parity: services/KiskService.removeKisk removes ownerPlayer entries pointing at the deleted kisk.
		if (!_ownersByKiskId.TryRemove(kiskObjectId, out var ownerObjectId))
			return false;

		return _ownerKisks.TryRemove(ownerObjectId, out _);
	}
}

public sealed record PlayerKiskOwnership(
	int KiskObjectId,
	int OwnerObjectId,
	int NpcId);
