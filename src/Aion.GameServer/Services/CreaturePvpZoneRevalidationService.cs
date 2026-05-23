using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class CreaturePvpZoneRevalidationService
{
	public static CreaturePvpZoneRevalidationResult Revalidate(
		int objectId,
		WorldPosition position,
		CreaturePvpZoneTable? zones,
		CreaturePvpZoneCounterService? counterService)
	{
		if (objectId <= 0 || zones == null || counterService == null)
			return CreaturePvpZoneRevalidationResult.Empty;

		var transitions = new List<CreaturePvpZoneRevalidationTransition>();
		foreach (var zone in zones.Zones)
		{
			var transition = zone.Contains(position)
				? counterService.ApplyZoneEnter(objectId, zone.ZoneId, ToCounterType(zone.ZoneType))
				: counterService.ApplyZoneLeave(objectId, zone.ZoneId, ToCounterType(zone.ZoneType));
			if (!transition.Applied)
				continue;

			transitions.Add(new CreaturePvpZoneRevalidationTransition(
				zone.ZoneId,
				zone.ZoneType,
				transition.Status,
				transition.Counters));
		}

		return transitions.Count == 0
			? CreaturePvpZoneRevalidationResult.Empty
			: new CreaturePvpZoneRevalidationResult(transitions);
	}

	private static CreaturePvpZoneCounterType ToCounterType(CreaturePvpZoneType zoneType)
	{
		return zoneType switch
		{
			// Java parity: PvPZoneInstance.onEnter/onLeave mutates ZoneType.PVP.
			CreaturePvpZoneType.Pvp => CreaturePvpZoneCounterType.Pvp,

			// Java parity: FortressLocation.onEnterZone/onLeaveZone mutates ZoneType.SIEGE.
			CreaturePvpZoneType.Siege => CreaturePvpZoneCounterType.Siege,
			_ => CreaturePvpZoneCounterType.Pvp,
		};
	}
}

public sealed record CreaturePvpZoneRevalidationResult(
	IReadOnlyList<CreaturePvpZoneRevalidationTransition> Transitions)
{
	public static CreaturePvpZoneRevalidationResult Empty { get; } = new(Array.Empty<CreaturePvpZoneRevalidationTransition>());
}

public sealed record CreaturePvpZoneRevalidationTransition(
	string ZoneId,
	CreaturePvpZoneType ZoneType,
	CreaturePvpZoneMembershipTransitionStatus Status,
	CreaturePvpZoneCounters Counters);
