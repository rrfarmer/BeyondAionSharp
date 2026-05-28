namespace Aion.GameServer.Services;

public static class WorldMapRegionMaterialZoneSerializationPlanService
{
	public const string RootElementName = "zones";
	public const string ZoneElementName = "zone";
	public const string SchemaPath = "./data/static_data/zones/zones.xsd";
	public const string OutputPath = WorldMapRegionMaterialZoneSavePlanService.GeneratedZonesPath;

	public static WorldMapRegionMaterialZoneSerializationPlan CreatePlan(
		IReadOnlyList<WorldMapRegionMaterialZoneSerializableTemplate> templates)
	{
		ArgumentNullException.ThrowIfNull(templates);

		// Java parity breadcrumb: ZoneData.saveData creates JAXBContext for ZoneData,
		// applies zones.xsd, enables formatted output, and marshals zoneList to
		// ./data/static_data/zones/generated_zones.xml.
		var entries = templates.Select(CreateEntry).ToArray();
		var status = entries.Any(entry => entry.Status != WorldMapRegionMaterialZoneSerializationStatus.Ready)
			? WorldMapRegionMaterialZoneSerializationStatus.BlockedInvalidTemplate
			: WorldMapRegionMaterialZoneSerializationStatus.Ready;

		return new WorldMapRegionMaterialZoneSerializationPlan(
			RootElementName,
			ZoneElementName,
			SchemaPath,
			OutputPath,
			FormattedOutput: true,
			entries,
			status,
			JavaSource: "ZoneData.saveData JAXB serialization boundary; live file writes disabled");
	}

	private static WorldMapRegionMaterialZoneSerializationEntry CreateEntry(
		WorldMapRegionMaterialZoneSerializableTemplate template)
	{
		var requiredAttributes = RequiredAttributes(template.AreaKind);
		var missingAttributes = MissingAttributes(template).ToArray();
		var status = missingAttributes.Length == 0
			? WorldMapRegionMaterialZoneSerializationStatus.Ready
			: WorldMapRegionMaterialZoneSerializationStatus.BlockedInvalidTemplate;

		return new WorldMapRegionMaterialZoneSerializationEntry(
			template.ZoneName,
			template.MapId,
			template.AreaKind,
			template.ZoneType,
			template.Flags,
			template.Priority,
			requiredAttributes,
			missingAttributes,
			status);
	}

	private static IReadOnlyList<string> RequiredAttributes(
		WorldMapRegionMaterialZoneAreaKind areaKind)
	{
		return areaKind switch
		{
			WorldMapRegionMaterialZoneAreaKind.Cylinder => ["x", "y", "r", "top", "bottom"],
			WorldMapRegionMaterialZoneAreaKind.Sphere => ["x", "y", "z", "r"],
			WorldMapRegionMaterialZoneAreaKind.Semisphere => ["x", "y", "z", "r"],
			_ => [],
		};
	}

	private static IEnumerable<string> MissingAttributes(
		WorldMapRegionMaterialZoneSerializableTemplate template)
	{
		var geometry = template.Geometry;
		if (geometry is null)
		{
			foreach (var attribute in RequiredAttributes(template.AreaKind))
				yield return attribute;
			yield break;
		}

		if (template.AreaKind is WorldMapRegionMaterialZoneAreaKind.Cylinder)
		{
			if (geometry.Top is null)
				yield return "top";
			if (geometry.Bottom is null)
				yield return "bottom";
		}
		else if (template.AreaKind is not WorldMapRegionMaterialZoneAreaKind.Sphere
			and not WorldMapRegionMaterialZoneAreaKind.Semisphere)
		{
			yield return "area";
		}
	}
}

public sealed record WorldMapRegionMaterialZoneSerializableTemplate(
	string ZoneName,
	int MapId,
	WorldMapRegionMaterialZoneAreaKind AreaKind,
	WorldMapRegionMaterialZoneGeometry? Geometry,
	string ZoneType = "MATERIAL",
	int Flags = -1,
	int Priority = 0);

public sealed record WorldMapRegionMaterialZoneSerializationPlan(
	string RootElementName,
	string ZoneElementName,
	string SchemaPath,
	string OutputPath,
	bool FormattedOutput,
	IReadOnlyList<WorldMapRegionMaterialZoneSerializationEntry> Entries,
	WorldMapRegionMaterialZoneSerializationStatus Status,
	string JavaSource);

public sealed record WorldMapRegionMaterialZoneSerializationEntry(
	string ZoneName,
	int MapId,
	WorldMapRegionMaterialZoneAreaKind AreaKind,
	string ZoneType,
	int Flags,
	int Priority,
	IReadOnlyList<string> RequiredShapeAttributes,
	IReadOnlyList<string> MissingShapeAttributes,
	WorldMapRegionMaterialZoneSerializationStatus Status);

public enum WorldMapRegionMaterialZoneSerializationStatus
{
	Ready,
	BlockedInvalidTemplate,
}
