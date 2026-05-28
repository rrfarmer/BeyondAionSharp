using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionMaterialZoneConstructionServiceTests
{
	[Fact]
	public void CreatePlan_NoneZoneNameReturnsWithoutMutation()
	{
		var plan = WorldMapRegionMaterialZoneConstructionService.CreatePlan(CreateContext(zoneName: "NONE"));

		Assert.Equal(WorldMapRegionMaterialZoneConstructionStatus.IgnoredNoneZoneName, plan.Status);
		Assert.Equal(WorldMapRegionMaterialZoneHandlerKind.None, plan.HandlerKind);
		Assert.False(plan.ZoneInfoCreated);
		Assert.Contains("ZoneName.NONE", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_ShieldMaterialRequiresShieldHandler()
	{
		var blocked = WorldMapRegionMaterialZoneConstructionService.CreatePlan(CreateContext(
			materialId: WorldMapRegionMaterialZoneConstructionService.ShieldMaterialId,
			shieldCanRegister: false));
		var created = WorldMapRegionMaterialZoneConstructionService.CreatePlan(CreateContext(
			geometryName: "CYLINDER_SHIELD",
			materialId: WorldMapRegionMaterialZoneConstructionService.ShieldMaterialId,
			shieldCanRegister: true));

		Assert.Equal(WorldMapRegionMaterialZoneConstructionStatus.BlockedMissingShieldHandler, blocked.Status);
		Assert.Equal(WorldMapRegionMaterialZoneHandlerKind.None, blocked.HandlerKind);
		Assert.Equal(WorldMapRegionMaterialZoneHandlerKind.Shield, created.HandlerKind);
		Assert.Equal(WorldMapRegionMaterialZoneAreaKind.Cylinder, created.AreaKind);
		Assert.Contains("ShieldService.tryRegisterShield", created.SideEffects);
	}

	[Fact]
	public void CreatePlan_NonShieldMaterialRequiresMaterialTemplate()
	{
		var blocked = WorldMapRegionMaterialZoneConstructionService.CreatePlan(CreateContext(
			materialId: 14,
			materialTemplateExists: false));
		var created = WorldMapRegionMaterialZoneConstructionService.CreatePlan(CreateContext(
			materialId: 14,
			materialTemplateExists: true));

		Assert.Equal(WorldMapRegionMaterialZoneConstructionStatus.BlockedMissingMaterialTemplate, blocked.Status);
		Assert.Equal(WorldMapRegionMaterialZoneHandlerKind.None, blocked.HandlerKind);
		Assert.Equal(WorldMapRegionMaterialZoneHandlerKind.Material, created.HandlerKind);
		Assert.Contains("MaterialZoneHandler created", created.SideEffects);
	}

	[Theory]
	[InlineData("AION_MATERIAL_CYLINDER", WorldMapRegionMaterialZoneAreaKind.Cylinder)]
	[InlineData("AION_MATERIAL_CONE", WorldMapRegionMaterialZoneAreaKind.Cylinder)]
	[InlineData("AION_MATERIAL_H_COLUME", WorldMapRegionMaterialZoneAreaKind.Cylinder)]
	[InlineData("AION_MATERIAL_SEMISPHERE", WorldMapRegionMaterialZoneAreaKind.Semisphere)]
	[InlineData("AION_MATERIAL_SPHERE", WorldMapRegionMaterialZoneAreaKind.Sphere)]
	public void CreatePlan_SelectsJavaMaterialZoneAreaKind(string geometryName, WorldMapRegionMaterialZoneAreaKind expectedAreaKind)
	{
		var plan = WorldMapRegionMaterialZoneConstructionService.CreatePlan(CreateContext(
			geometryName: geometryName,
			materialTemplateExists: true));

		Assert.Equal(expectedAreaKind, plan.AreaKind);
		Assert.True(plan.ZoneInfoCreated);
		Assert.Contains("ZoneInfo added to zoneByMapIdMap", plan.SideEffects);
	}

	[Fact]
	public void CreatePlan_DuplicateHandlerStillChecksZoneInfoAndWarns()
	{
		var plan = WorldMapRegionMaterialZoneConstructionService.CreatePlan(CreateContext(
			existingCollidableHandler: true,
			existingZoneInfo: false,
			materialTemplateExists: false));

		Assert.Equal(WorldMapRegionMaterialZoneHandlerKind.Existing, plan.HandlerKind);
		Assert.Equal(WorldMapRegionMaterialZoneConstructionStatus.Created, plan.Status);
		Assert.True(plan.ZoneInfoCreated);
		Assert.Contains("Duplicate material mesh warning", plan.SideEffects);
	}

	[Fact]
	public void CreatePlan_ExistingZoneInfoSkipsMaterialZoneInfoCreation()
	{
		var plan = WorldMapRegionMaterialZoneConstructionService.CreatePlan(CreateContext(
			existingZoneInfo: true,
			worldHadNoAreaList: true));

		Assert.Equal(WorldMapRegionMaterialZoneConstructionStatus.ReusedExistingZoneInfo, plan.Status);
		Assert.Equal(WorldMapRegionMaterialZoneAreaKind.Existing, plan.AreaKind);
		Assert.False(plan.ZoneInfoCreated);
		Assert.Contains("zoneByMapIdMap list created", plan.SideEffects);
	}

	private static WorldMapRegionMaterialZoneConstructionContext CreateContext(
		int worldId = 210010000,
		string zoneName = "MATERIAL_ZONE_210010000",
		string geometryName = "AION_MATERIAL_SPHERE",
		int materialId = 14,
		bool existingCollidableHandler = false,
		bool shieldCanRegister = true,
		bool materialTemplateExists = true,
		bool worldHadNoAreaList = false,
		bool existingZoneInfo = false)
	{
		return new WorldMapRegionMaterialZoneConstructionContext(
			worldId,
			zoneName,
			geometryName,
			materialId,
			existingCollidableHandler,
			shieldCanRegister,
			materialTemplateExists,
			worldHadNoAreaList,
			existingZoneInfo);
	}
}
