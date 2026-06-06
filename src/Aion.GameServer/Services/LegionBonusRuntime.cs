using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class LegionBonusRuntime
{
	public const int OnlineMemberThreshold = 10;

	private readonly ConcurrentDictionary<int, byte> _activeLegions = new();

	public bool IsActive(int legionId)
	{
		return legionId > 0 && _activeLegions.ContainsKey(legionId);
	}

	public bool TryActivate(int legionId, int onlineMemberCount)
	{
		return legionId > 0
			&& onlineMemberCount >= OnlineMemberThreshold
			&& _activeLegions.TryAdd(legionId, 0);
	}

	public bool TryDeactivate(int legionId, int onlineMemberCount)
	{
		return legionId > 0
			&& onlineMemberCount < OnlineMemberThreshold
			&& _activeLegions.TryRemove(legionId, out _);
	}
}
