using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class CreaturePvpZoneCounterService
{
	private readonly ConcurrentDictionary<int, CreaturePvpZoneCounters> _countersByObjectId = new();

	public CreaturePvpZoneCounters EnterZone(int objectId, CreaturePvpZoneCounterType zoneType)
	{
		// Java parity: Creature.setInsideZoneType increments nested ZoneType counters.
		if (objectId <= 0)
			return CreaturePvpZoneCounters.Empty;

		return _countersByObjectId.AddOrUpdate(
			objectId,
			_ => CreaturePvpZoneCounters.Empty.Increment(zoneType),
			(_, current) => current.Increment(zoneType));
	}

	public CreaturePvpZoneCounters LeaveZone(int objectId, CreaturePvpZoneCounterType zoneType)
	{
		// Java parity: Creature.unsetInsideZoneType decrements after ZoneInstance confirms membership.
		if (objectId <= 0)
			return CreaturePvpZoneCounters.Empty;

		var updated = CreaturePvpZoneCounters.Empty;
		_countersByObjectId.AddOrUpdate(
			objectId,
			_ => CreaturePvpZoneCounters.Empty,
			(_, current) =>
			{
				updated = current.Decrement(zoneType);
				return updated;
			});
		if (updated.IsEmpty)
			_countersByObjectId.TryRemove(new KeyValuePair<int, CreaturePvpZoneCounters>(objectId, updated));
		return updated;
	}

	public CreaturePvpZoneCounters GetCounters(int objectId)
	{
		if (objectId <= 0)
			return CreaturePvpZoneCounters.Empty;

		return _countersByObjectId.TryGetValue(objectId, out var counters)
			? counters
			: CreaturePvpZoneCounters.Empty;
	}

	public bool ClearCounters(int objectId)
	{
		// Java parity: Creature zone counters disappear with the creature instance on despawn/delete.
		return objectId > 0 && _countersByObjectId.TryRemove(objectId, out _);
	}
}

public sealed record CreaturePvpZoneCounters(int SiegeZoneCount = 0, int PvpZoneCount = 0)
{
	public static CreaturePvpZoneCounters Empty { get; } = new();

	public bool IsEmpty => SiegeZoneCount == 0 && PvpZoneCount == 0;

	public bool IsInsidePvpZone => CreaturePvpZoneStateService.IsInsidePvpZone(SiegeZoneCount, PvpZoneCount);

	public CreaturePvpZoneCounters Increment(CreaturePvpZoneCounterType zoneType)
	{
		return zoneType switch
		{
			CreaturePvpZoneCounterType.Siege => this with { SiegeZoneCount = SiegeZoneCount + 1 },
			CreaturePvpZoneCounterType.Pvp => this with { PvpZoneCount = PvpZoneCount + 1 },
			_ => this,
		};
	}

	public CreaturePvpZoneCounters Decrement(CreaturePvpZoneCounterType zoneType)
	{
		return zoneType switch
		{
			CreaturePvpZoneCounterType.Siege => this with { SiegeZoneCount = Math.Max(SiegeZoneCount - 1, 0) },
			CreaturePvpZoneCounterType.Pvp => this with { PvpZoneCount = Math.Max(PvpZoneCount - 1, 0) },
			_ => this,
		};
	}
}

public enum CreaturePvpZoneCounterType
{
	// Java parity: model/templates/zone/ZoneType.SIEGE.
	Siege,

	// Java parity: model/templates/zone/ZoneType.PVP.
	Pvp,
}
