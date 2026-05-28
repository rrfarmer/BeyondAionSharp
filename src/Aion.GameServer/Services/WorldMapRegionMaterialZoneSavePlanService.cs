namespace Aion.GameServer.Services;

public static class WorldMapRegionMaterialZoneSavePlanService
{
	public const string GeneratedZonesPath = "./data/static_data/zones/generated_zones.xml";

	public static WorldMapRegionMaterialZoneSavePlan CreatePlan(
		WorldMapRegionMaterialZoneSaveContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: ZoneService.saveMaterialZones scans DataManager.WORLD_MAPS_DATA,
		// filters ZoneInfo entries whose Area zone name has a collidable handler, sorts templates by
		// ZoneTemplate.getMapid, then delegates XML persistence to ZoneData.saveData.
		var collidableHandlers = new HashSet<string>(
			context.CollidableHandlerZoneNames,
			StringComparer.Ordinal);
		var templates = new List<WorldMapRegionMaterialZoneTemplateSnapshot>();
		var visitedMapIds = new List<int>();
		var skippedMapIds = new List<int>();

		foreach (var mapId in context.WorldMapIds)
		{
			visitedMapIds.Add(mapId);
			var zones = context.Zones.FirstOrDefault(z => z.MapId == mapId);
			if (zones is null)
			{
				skippedMapIds.Add(mapId);
				continue;
			}

			foreach (var zone in zones.Zones)
			{
				if (collidableHandlers.Contains(zone.AreaZoneName))
					templates.Add(zone.Template);
			}
		}

		return new WorldMapRegionMaterialZoneSavePlan(
			templates.OrderBy(template => template.MapId).ToArray(),
			visitedMapIds,
			skippedMapIds,
			GeneratedZonesPath,
			JavaSource: "ZoneService.saveMaterialZones; ZoneData.saveData persistence boundary");
	}
}

public sealed record WorldMapRegionMaterialZoneSaveContext(
	IReadOnlyList<int> WorldMapIds,
	IReadOnlyList<WorldMapRegionMaterialZoneMapZones> Zones,
	IReadOnlyList<string> CollidableHandlerZoneNames);

public sealed record WorldMapRegionMaterialZoneMapZones(
	int MapId,
	IReadOnlyList<WorldMapRegionMaterialZoneInfoSnapshot> Zones);

public sealed record WorldMapRegionMaterialZoneInfoSnapshot(
	string AreaZoneName,
	WorldMapRegionMaterialZoneTemplateSnapshot Template);

public sealed record WorldMapRegionMaterialZoneTemplateSnapshot(
	string ZoneName,
	int MapId);

public sealed record WorldMapRegionMaterialZoneSavePlan(
	IReadOnlyList<WorldMapRegionMaterialZoneTemplateSnapshot> Templates,
	IReadOnlyList<int> VisitedMapIds,
	IReadOnlyList<int> SkippedMapIds,
	string PersistencePath,
	string JavaSource);
