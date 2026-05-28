namespace Aion.GameServer.Services;

public static class WorldMapRegionZoneScanPlanService
{
	public const string AbyssCastleAreaZoneName = "_ABYSS_CASTLE_AREA_";

	public static WorldMapRegionZoneRevalidationPlan CreateRevalidationPlan(
		bool creatureIsSpawned,
		IEnumerable<WorldMapRegionZoneScanCandidate> constructorOrderedZones)
	{
		ArgumentNullException.ThrowIfNull(constructorOrderedZones);

		// Java parity breadcrumb: MapRegion.revalidateZones scans zonesSortedByTypeAndPriority,
		// resets priority suppression when ZoneClassName changes, leaves on failed revalidation,
		// and only lets one priority zone enter per type group.
		WorldMapRegionZoneSortClassName? zoneType = null;
		var enteredPriorityZone = false;
		var actions = new List<WorldMapRegionZoneRevalidationAction>();

		foreach (var zone in constructorOrderedZones)
		{
			if (zoneType != zone.ZoneClassName)
			{
				zoneType = zone.ZoneClassName;
				enteredPriorityZone = false;
			}

			if (!creatureIsSpawned || enteredPriorityZone || !zone.RevalidateSucceeds)
			{
				actions.Add(new WorldMapRegionZoneRevalidationAction(
					zone.ZoneId,
					zone.ZoneClassName,
					WorldMapRegionZoneRevalidationActionType.Leave));
				continue;
			}

			if (zone.Priority != 0)
				enteredPriorityZone = true;

			actions.Add(new WorldMapRegionZoneRevalidationAction(
				zone.ZoneId,
				zone.ZoneClassName,
				WorldMapRegionZoneRevalidationActionType.Enter));
		}

		return new WorldMapRegionZoneRevalidationPlan(
			actions,
			"MapRegion.revalidateZones over zonesSortedByTypeAndPriority; live zone handlers disabled");
	}

	public static IReadOnlyList<string> FindInsideZones(IEnumerable<WorldMapRegionZoneScanCandidate> constructorOrderedZones)
	{
		ArgumentNullException.ThrowIfNull(constructorOrderedZones);

		// Java parity breadcrumb: MapRegion.findZones adds every zone whose isInsideCreature returns true.
		return constructorOrderedZones
			.Where(zone => zone.IsInsideCreature)
			.Select(zone => zone.ZoneId)
			.ToArray();
	}

	public static bool IsInsideZoneByName(
		IEnumerable<WorldMapRegionZoneScanCandidate> constructorOrderedZones,
		int zoneNameId,
		WorldMapRegionZoneInsideMode mode)
	{
		ArgumentNullException.ThrowIfNull(constructorOrderedZones);

		// Java parity breadcrumb: MapRegion.isInsideZone returns the first same ZoneName result.
		var zone = constructorOrderedZones.FirstOrDefault(zone => zone.ZoneNameId == zoneNameId);
		return zone is not null && GetInsideResult(zone, mode);
	}

	public static bool IsInsideItemUseZone(
		IEnumerable<WorldMapRegionZoneScanCandidate> constructorOrderedZones,
		string zoneName,
		WorldMapRegionZoneInsideMode mode)
	{
		ArgumentNullException.ThrowIfNull(constructorOrderedZones);
		ArgumentNullException.ThrowIfNull(zoneName);

		// Java parity breadcrumb: MapRegion.isInsideItemUseZone checks FORT zones for
		// _ABYSS_CASTLE_AREA_, otherwise ZoneTemplate.getXmlName().startsWith(zoneName).
		var checkFortresses = string.Equals(zoneName, AbyssCastleAreaZoneName, StringComparison.Ordinal);
		foreach (var zone in constructorOrderedZones)
		{
			if (checkFortresses)
			{
				if (zone.ZoneClassName != WorldMapRegionZoneSortClassName.Fort)
					continue;
			}
			else if (!zone.XmlName.StartsWith(zoneName, StringComparison.Ordinal))
			{
				continue;
			}

			if (GetInsideResult(zone, mode))
				return true;
		}

		return false;
	}

	private static bool GetInsideResult(WorldMapRegionZoneScanCandidate zone, WorldMapRegionZoneInsideMode mode)
	{
		return mode switch
		{
			WorldMapRegionZoneInsideMode.Creature => zone.IsInsideCreature,
			WorldMapRegionZoneInsideMode.Coordinate => zone.IsInsideCoordinate,
			_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported Java zone inside-check mode."),
		};
	}
}

public sealed record WorldMapRegionZoneScanCandidate(
	string ZoneId,
	WorldMapRegionZoneSortClassName ZoneClassName,
	int Priority,
	int ZoneNameId,
	string XmlName,
	bool RevalidateSucceeds,
	bool IsInsideCreature,
	bool IsInsideCoordinate);

public sealed record WorldMapRegionZoneRevalidationPlan(
	IReadOnlyList<WorldMapRegionZoneRevalidationAction> Actions,
	string JavaSource);

public sealed record WorldMapRegionZoneRevalidationAction(
	string ZoneId,
	WorldMapRegionZoneSortClassName ZoneClassName,
	WorldMapRegionZoneRevalidationActionType ActionType);

public enum WorldMapRegionZoneRevalidationActionType
{
	Enter,
	Leave,
}

public enum WorldMapRegionZoneInsideMode
{
	Creature,
	Coordinate,
}
