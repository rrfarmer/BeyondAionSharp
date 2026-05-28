namespace Aion.GameServer.Services;

public static class WorldMapRegionZoneSortService
{
	public static IReadOnlyList<WorldMapRegionZoneSortCandidate> SortByJavaMapRegionOrder(
		IEnumerable<WorldMapRegionZoneSortCandidate> zones)
	{
		ArgumentNullException.ThrowIfNull(zones);

		// Java parity breadcrumb: MapRegion.zoneComparator compares ZoneTemplate.getZoneType(),
		// then ZoneTemplate.getPriority(), then ZoneTemplate.getName().id(); Arrays.sort(Object[])
		// preserves equal-key order for ZoneInstance references.
		return zones
			.Select((zone, index) => new { Zone = zone, Index = index })
			.OrderBy(item => item.Zone.ZoneClassName)
			.ThenBy(item => item.Zone.Priority)
			.ThenBy(item => item.Zone.ZoneNameId)
			.ThenBy(item => item.Index)
			.Select(item => item.Zone)
			.ToArray();
	}

	public static int GetJavaZoneNameId(string zoneName)
	{
		ArgumentNullException.ThrowIfNull(zoneName);

		var hash = 0;
		foreach (var character in zoneName.ToUpperInvariant())
			unchecked
			{
				hash = (31 * hash) + character;
			}

		return hash;
	}
}

public sealed record WorldMapRegionZoneSortCandidate(
	string ZoneId,
	WorldMapRegionZoneSortClassName ZoneClassName,
	int Priority,
	int ZoneNameId);

public enum WorldMapRegionZoneSortClassName
{
	Dummy = 0,
	Sub = 1,
	Fly = 2,
	NoFly = 3,
	Artifact = 4,
	Fort = 5,
	Limit = 6,
	ItemUse = 7,
	Pvp = 8,
	Duel = 9,
	House = 10,
	Weather = 11,
	Dominion = 12,
}
