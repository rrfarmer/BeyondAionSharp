using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionMaterialZoneSerializationPlanServiceTests
{
	[Fact]
	public void CreatePlan_RecordsJavaJaxbSchemaAndOutputBoundary()
	{
		var plan = WorldMapRegionMaterialZoneSerializationPlanService.CreatePlan(
		[
			Template("MATERIAL_CYLINDER", 210010000, WorldMapRegionMaterialZoneAreaKind.Cylinder, CylinderGeometry()),
			Template("MATERIAL_SPHERE", 210020000, WorldMapRegionMaterialZoneAreaKind.Sphere, SphereGeometry()),
			Template("MATERIAL_SEMISPHERE", 210030000, WorldMapRegionMaterialZoneAreaKind.Semisphere, SphereGeometry()),
		]);

		Assert.Equal(WorldMapRegionMaterialZoneSerializationStatus.Ready, plan.Status);
		Assert.Equal("zones", plan.RootElementName);
		Assert.Equal("zone", plan.ZoneElementName);
		Assert.Equal("./data/static_data/zones/zones.xsd", plan.SchemaPath);
		Assert.Equal(WorldMapRegionMaterialZoneSavePlanService.GeneratedZonesPath, plan.OutputPath);
		Assert.True(plan.FormattedOutput);
		Assert.Contains("ZoneData.saveData", plan.JavaSource);
		Assert.Equal(["x", "y", "r", "top", "bottom"], plan.Entries[0].RequiredShapeAttributes);
		Assert.Equal(["x", "y", "z", "r"], plan.Entries[1].RequiredShapeAttributes);
		Assert.Equal(["x", "y", "z", "r"], plan.Entries[2].RequiredShapeAttributes);
	}

	[Fact]
	public void CreatePlan_BlocksTemplatesMissingShapeFields()
	{
		var plan = WorldMapRegionMaterialZoneSerializationPlanService.CreatePlan(
		[
			Template("MISSING_CYLINDER_BOUNDS", 210010000, WorldMapRegionMaterialZoneAreaKind.Cylinder, SphereGeometry()),
			Template("MISSING_SPHERE_GEOMETRY", 210020000, WorldMapRegionMaterialZoneAreaKind.Sphere, geometry: null),
		]);

		Assert.Equal(WorldMapRegionMaterialZoneSerializationStatus.BlockedInvalidTemplate, plan.Status);
		Assert.Equal(["top", "bottom"], plan.Entries[0].MissingShapeAttributes);
		Assert.Equal(["x", "y", "z", "r"], plan.Entries[1].MissingShapeAttributes);
	}

	private static WorldMapRegionMaterialZoneSerializableTemplate Template(
		string zoneName,
		int mapId,
		WorldMapRegionMaterialZoneAreaKind areaKind,
		WorldMapRegionMaterialZoneGeometry? geometry)
	{
		return new WorldMapRegionMaterialZoneSerializableTemplate(zoneName, mapId, areaKind, geometry);
	}

	private static WorldMapRegionMaterialZoneGeometry CylinderGeometry()
	{
		return new WorldMapRegionMaterialZoneGeometry(
			CenterX: 10,
			CenterY: 20,
			CenterZ: 30,
			Radius: 6,
			Top: 36,
			Bottom: 24);
	}

	private static WorldMapRegionMaterialZoneGeometry SphereGeometry()
	{
		return new WorldMapRegionMaterialZoneGeometry(
			CenterX: 10,
			CenterY: 20,
			CenterZ: 30,
			Radius: 6,
			Top: null,
			Bottom: null);
	}
}
