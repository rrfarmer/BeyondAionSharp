namespace Aion.GameServer.Services;

public static class WorldMapRegionZoneConstructionService
{
	private static readonly HashSet<string> InvasionZoneNames = new(StringComparer.Ordinal)
	{
		"WAILING_CLIFFS_220050000",
		"BALTASAR_CEMETERY_220050000",
		"THE_LEGEND_SHRINE_220050000",
		"SUDORVILLE_220050000",
		"BALTASAR_HILL_VILLAGE_220050000",
		"BRUSTHONIN_MITHRIL_MINE_220050000",
		"JAMANOK_INN_210060000",
		"THE_STALKING_GROUNDS_210060000",
		"BLACK_ROCK_HOT_SPRING_210060000",
		"FREGIONS_FLAME_210060000",
	};

	public static WorldMapRegionZoneConstructionPlan CreatePlan(
		WorldMapRegionZoneConstructionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: ZoneService.getZoneInstancesByWorldId creates a DUMMY full-map
		// ZoneInstance first, then selects specialized ZoneInstance subclasses from ZoneClassName.
		var zones = new List<WorldMapRegionZoneConstructionEntry>
		{
			new(
				ZoneId: context.MapId.ToString(),
				WorldMapRegionZoneSortClassName.Dummy,
				WorldMapRegionZoneInstanceKind.Base,
				HandlerAttached: true,
				IsFullMapZone: true,
				FullMapBounds: CreateFullMapBounds(context),
				SideEffects: ["full-map WorldZoneTemplate handler attached"]),
		};

		foreach (var zone in context.Zones)
			zones.Add(CreateZoneEntry(zone, context));

		var finalZonesByName = new Dictionary<string, WorldMapRegionZoneConstructionEntry>(StringComparer.Ordinal);
		var replacedZoneIds = new List<string>();
		foreach (var zone in zones)
		{
			if (finalZonesByName.ContainsKey(zone.ZoneId))
				replacedZoneIds.Add(zone.ZoneId);
			finalZonesByName[zone.ZoneId] = zone;
		}

		return new WorldMapRegionZoneConstructionPlan(
			context.MapId,
			zones,
			finalZonesByName.Keys.ToArray(),
			replacedZoneIds,
			JavaSource: "ZoneService.getZoneInstancesByWorldId non-live construction plan; live ZoneInstance storage disabled");
	}

	private static WorldMapRegionZoneFullMapBounds CreateFullMapBounds(
		WorldMapRegionZoneConstructionContext context)
	{
		var maxZ = (int)MathF.Round((float)context.WorldSize / context.RegionSize) * context.RegionSize;
		return new WorldMapRegionZoneFullMapBounds(
			MinX: -1,
			MinY: -1,
			MaxX: context.WorldSize + 1,
			MaxY: context.WorldSize + 1,
			Bottom: -1,
			Top: maxZ + 1,
			context.WorldFlags);
	}

	private static WorldMapRegionZoneConstructionEntry CreateZoneEntry(
		WorldMapRegionZoneConstructionCandidate zone,
		WorldMapRegionZoneConstructionContext context)
	{
		var sideEffects = new List<string>();
		var kind = zone.ZoneClassName switch
		{
			WorldMapRegionZoneSortClassName.Fly => WorldMapRegionZoneInstanceKind.Fly,
			WorldMapRegionZoneSortClassName.NoFly => WorldMapRegionZoneInstanceKind.NoFly,
			WorldMapRegionZoneSortClassName.Fort => CreateFortEntry(zone, context, sideEffects),
			WorldMapRegionZoneSortClassName.Artifact => CreateArtifactEntry(zone, context, sideEffects),
			WorldMapRegionZoneSortClassName.Pvp => WorldMapRegionZoneInstanceKind.Pvp,
			_ => CreateDefaultEntry(zone, context, sideEffects),
		};

		sideEffects.Add("zone handler attached");
		return new WorldMapRegionZoneConstructionEntry(
			zone.ZoneId,
			zone.ZoneClassName,
			kind,
			HandlerAttached: true,
			IsFullMapZone: false,
			FullMapBounds: null,
			sideEffects);
	}

	private static WorldMapRegionZoneInstanceKind CreateFortEntry(
		WorldMapRegionZoneConstructionCandidate zone,
		WorldMapRegionZoneConstructionContext context,
		List<string> sideEffects)
	{
		var siegeId = zone.SiegeIds.FirstOrDefault();
		if (siegeId != 0 && context.AvailableSiegeLocationIds.Contains(siegeId))
		{
			sideEffects.Add("siege location addZone");
			sideEffects.Add("ShieldService.attachShield");
		}
		else
		{
			sideEffects.Add("missing siege location leaves zone without shield attachment");
		}

		return WorldMapRegionZoneInstanceKind.Siege;
	}

	private static WorldMapRegionZoneInstanceKind CreateArtifactEntry(
		WorldMapRegionZoneConstructionCandidate zone,
		WorldMapRegionZoneConstructionContext context,
		List<string> sideEffects)
	{
		foreach (var artifactId in zone.SiegeIds)
		{
			sideEffects.Add(context.AvailableArtifactLocationIds.Contains(artifactId)
				? $"artifact {artifactId} addZone"
				: $"missing artifact siege location {artifactId}");
		}

		return WorldMapRegionZoneInstanceKind.Siege;
	}

	private static WorldMapRegionZoneInstanceKind CreateDefaultEntry(
		WorldMapRegionZoneConstructionCandidate zone,
		WorldMapRegionZoneConstructionContext context,
		List<string> sideEffects)
	{
		if (InvasionZoneNames.Contains(zone.ZoneId))
		{
			if (context.VortexMapIds.Contains(zone.MapId))
			{
				sideEffects.Add("vortex addZone");
				return WorldMapRegionZoneInstanceKind.Invasion;
			}

			sideEffects.Add("invasion name matched but no vortex location");
		}

		return WorldMapRegionZoneInstanceKind.Base;
	}
}

public sealed record WorldMapRegionZoneConstructionContext(
	int MapId,
	int WorldSize,
	int RegionSize,
	int WorldFlags,
	IReadOnlyList<WorldMapRegionZoneConstructionCandidate> Zones,
	IReadOnlySet<int> AvailableSiegeLocationIds,
	IReadOnlySet<int> AvailableArtifactLocationIds,
	IReadOnlySet<int> VortexMapIds);

public sealed record WorldMapRegionZoneConstructionCandidate(
	string ZoneId,
	int MapId,
	WorldMapRegionZoneSortClassName ZoneClassName,
	IReadOnlyList<int> SiegeIds);

public sealed record WorldMapRegionZoneConstructionPlan(
	int MapId,
	IReadOnlyList<WorldMapRegionZoneConstructionEntry> Zones,
	IReadOnlyList<string> FinalZoneIds,
	IReadOnlyList<string> ReplacedZoneIds,
	string JavaSource);

public sealed record WorldMapRegionZoneConstructionEntry(
	string ZoneId,
	WorldMapRegionZoneSortClassName ZoneClassName,
	WorldMapRegionZoneInstanceKind InstanceKind,
	bool HandlerAttached,
	bool IsFullMapZone,
	WorldMapRegionZoneFullMapBounds? FullMapBounds,
	IReadOnlyList<string> SideEffects);

public sealed record WorldMapRegionZoneFullMapBounds(
	float MinX,
	float MinY,
	float MaxX,
	float MaxY,
	float Bottom,
	float Top,
	int Flags);

public enum WorldMapRegionZoneInstanceKind
{
	Base,
	Fly,
	NoFly,
	Siege,
	Pvp,
	Invasion,
}
