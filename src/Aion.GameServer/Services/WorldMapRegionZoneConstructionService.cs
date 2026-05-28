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
				SideEffects: ["full-map WorldZoneTemplate handler attached"]),
		};

		foreach (var zone in context.Zones)
			zones.Add(CreateZoneEntry(zone, context));

		return new WorldMapRegionZoneConstructionPlan(
			context.MapId,
			zones,
			JavaSource: "ZoneService.getZoneInstancesByWorldId non-live construction plan; live ZoneInstance storage disabled");
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
	string JavaSource);

public sealed record WorldMapRegionZoneConstructionEntry(
	string ZoneId,
	WorldMapRegionZoneSortClassName ZoneClassName,
	WorldMapRegionZoneInstanceKind InstanceKind,
	bool HandlerAttached,
	IReadOnlyList<string> SideEffects);

public enum WorldMapRegionZoneInstanceKind
{
	Base,
	Fly,
	NoFly,
	Siege,
	Pvp,
	Invasion,
}
