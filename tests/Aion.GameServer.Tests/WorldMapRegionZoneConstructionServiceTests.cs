using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionZoneConstructionServiceTests
{
	[Fact]
	public void CreatePlan_AlwaysCreatesFullMapDummyZoneFirst()
	{
		var plan = WorldMapRegionZoneConstructionService.CreatePlan(CreateContext(zones: []));

		var fullMap = Assert.Single(plan.Zones);
		Assert.Equal("210010000", fullMap.ZoneId);
		Assert.Equal(WorldMapRegionZoneSortClassName.Dummy, fullMap.ZoneClassName);
		Assert.Equal(WorldMapRegionZoneInstanceKind.Base, fullMap.InstanceKind);
		Assert.True(fullMap.HandlerAttached);
		Assert.True(fullMap.IsFullMapZone);
		Assert.Equal(["210010000"], plan.FinalZoneIds);
		Assert.Contains("ZoneService.getZoneInstancesByWorldId", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_FullMapZoneCarriesJavaWorldZoneTemplateBoundsAndFlags()
	{
		var plan = WorldMapRegionZoneConstructionService.CreatePlan(CreateContext(
			zones: [],
			worldSize: 257,
			regionSize: 128,
			worldFlags: 72));

		var bounds = Assert.IsType<WorldMapRegionZoneFullMapBounds>(Assert.Single(plan.Zones).FullMapBounds);
		Assert.Equal(-1, bounds.MinX);
		Assert.Equal(-1, bounds.MinY);
		Assert.Equal(258, bounds.MaxX);
		Assert.Equal(258, bounds.MaxY);
		Assert.Equal(-1, bounds.Bottom);
		Assert.Equal(257, bounds.Top);
		Assert.Equal(72, bounds.Flags);
	}

	[Fact]
	public void CreatePlan_SelectsJavaZoneInstanceKindsByZoneType()
	{
		var zones = new[]
		{
			Create("fly", WorldMapRegionZoneSortClassName.Fly),
			Create("no-fly", WorldMapRegionZoneSortClassName.NoFly),
			Create("pvp", WorldMapRegionZoneSortClassName.Pvp),
			Create("sub", WorldMapRegionZoneSortClassName.Sub),
		};

		var plan = WorldMapRegionZoneConstructionService.CreatePlan(CreateContext(zones));

		Assert.Equal(
		[
			("210010000", WorldMapRegionZoneInstanceKind.Base),
			("fly", WorldMapRegionZoneInstanceKind.Fly),
			("no-fly", WorldMapRegionZoneInstanceKind.NoFly),
			("pvp", WorldMapRegionZoneInstanceKind.Pvp),
			("sub", WorldMapRegionZoneInstanceKind.Base),
		], plan.Zones.Select(zone => (zone.ZoneId, zone.InstanceKind)));
		Assert.All(plan.Zones, zone => Assert.True(zone.HandlerAttached));
	}

	[Fact]
	public void CreatePlan_FinalZoneIdsPreserveJavaHashMapPutReplacementSemantics()
	{
		var zones = new[]
		{
			Create("duplicate", WorldMapRegionZoneSortClassName.Fly),
			Create("unique", WorldMapRegionZoneSortClassName.Sub),
			Create("duplicate", WorldMapRegionZoneSortClassName.Pvp),
		};

		var plan = WorldMapRegionZoneConstructionService.CreatePlan(CreateContext(zones));

		Assert.Equal(["210010000", "duplicate", "unique"], plan.FinalZoneIds);
		Assert.Equal(["duplicate"], plan.ReplacedZoneIds);
		Assert.Equal(WorldMapRegionZoneInstanceKind.Pvp, plan.Zones.Last(zone => zone.ZoneId == "duplicate").InstanceKind);
	}

	[Fact]
	public void CreatePlan_FortZoneUsesSiegeInstanceAndShieldSideEffectsWhenSiegeExists()
	{
		var zones = new[]
		{
			Create("fort", WorldMapRegionZoneSortClassName.Fort, siegeIds: [101]),
		};

		var plan = WorldMapRegionZoneConstructionService.CreatePlan(CreateContext(
			zones,
			availableSiegeLocationIds: new HashSet<int> { 101 }));

		var fort = plan.Zones.Single(zone => zone.ZoneId == "fort");
		Assert.Equal(WorldMapRegionZoneInstanceKind.Siege, fort.InstanceKind);
		Assert.Contains("siege location addZone", fort.SideEffects);
		Assert.Contains("ShieldService.attachShield", fort.SideEffects);
	}

	[Fact]
	public void CreatePlan_ArtifactZoneUsesSiegeInstanceAndReportsMissingArtifacts()
	{
		var zones = new[]
		{
			Create("artifact", WorldMapRegionZoneSortClassName.Artifact, siegeIds: [201, 202]),
		};

		var plan = WorldMapRegionZoneConstructionService.CreatePlan(CreateContext(
			zones,
			availableArtifactLocationIds: new HashSet<int> { 201 }));

		var artifact = plan.Zones.Single(zone => zone.ZoneId == "artifact");
		Assert.Equal(WorldMapRegionZoneInstanceKind.Siege, artifact.InstanceKind);
		Assert.Contains("artifact 201 addZone", artifact.SideEffects);
		Assert.Contains("missing artifact siege location 202", artifact.SideEffects);
	}

	[Fact]
	public void CreatePlan_InvasionZoneNameUsesVortexBackedInvasionInstance()
	{
		var zones = new[]
		{
			Create("WAILING_CLIFFS_220050000", WorldMapRegionZoneSortClassName.Sub, mapId: 220050000),
			Create("BALTASAR_CEMETERY_220050000", WorldMapRegionZoneSortClassName.Sub, mapId: 220050000),
		};

		var plan = WorldMapRegionZoneConstructionService.CreatePlan(CreateContext(
			zones,
			mapId: 220050000,
			vortexMapIds: new HashSet<int> { 220050000 }));

		Assert.Equal(
		[
			WorldMapRegionZoneInstanceKind.Base,
			WorldMapRegionZoneInstanceKind.Invasion,
			WorldMapRegionZoneInstanceKind.Invasion,
		], plan.Zones.Select(zone => zone.InstanceKind));
		Assert.All(plan.Zones.Skip(1), zone => Assert.Contains("vortex addZone", zone.SideEffects));
	}

	[Fact]
	public void CreatePlan_InvasionNameWithoutVortexFallsBackToBaseZone()
	{
		var zones = new[]
		{
			Create("JAMANOK_INN_210060000", WorldMapRegionZoneSortClassName.Sub, mapId: 210060000),
		};

		var plan = WorldMapRegionZoneConstructionService.CreatePlan(CreateContext(
			zones,
			mapId: 210060000));

		var zone = plan.Zones.Single(entry => entry.ZoneId == "JAMANOK_INN_210060000");
		Assert.Equal(WorldMapRegionZoneInstanceKind.Base, zone.InstanceKind);
		Assert.Contains("invasion name matched but no vortex location", zone.SideEffects);
	}

	private static WorldMapRegionZoneConstructionContext CreateContext(
		IReadOnlyList<WorldMapRegionZoneConstructionCandidate> zones,
		int mapId = 210010000,
		int worldSize = 256,
		int regionSize = 128,
		int worldFlags = 0,
		IReadOnlySet<int>? availableSiegeLocationIds = null,
		IReadOnlySet<int>? availableArtifactLocationIds = null,
		IReadOnlySet<int>? vortexMapIds = null)
	{
		return new WorldMapRegionZoneConstructionContext(
			mapId,
			worldSize,
			regionSize,
			worldFlags,
			zones,
			availableSiegeLocationIds ?? new HashSet<int>(),
			availableArtifactLocationIds ?? new HashSet<int>(),
			vortexMapIds ?? new HashSet<int>());
	}

	private static WorldMapRegionZoneConstructionCandidate Create(
		string zoneId,
		WorldMapRegionZoneSortClassName zoneClassName,
		int mapId = 210010000,
		IReadOnlyList<int>? siegeIds = null)
	{
		return new WorldMapRegionZoneConstructionCandidate(
			zoneId,
			mapId,
			zoneClassName,
			siegeIds ?? []);
	}
}
