using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionMaterialZoneHandlerPlanServiceTests
{
	[Fact]
	public void CreateEnterPlan_OwnerRaceGeometrySkipsRegistration()
	{
		var plan = WorldMapRegionMaterialZoneHandlerPlanService.CreateEnterPlan(CreateContext(
			geometryName: "BU_AB_DARKSP_01",
			creatureRace: WorldMapRegionMaterialZoneRace.Asmodians));

		Assert.Equal(WorldMapRegionMaterialZoneHandlerEnterStatus.IgnoredOwnerRace, plan.Status);
		Assert.Equal(WorldMapRegionMaterialZoneRace.Asmodians, plan.OwnerRace);
		Assert.Empty(plan.MatchingSkillIds);
		Assert.Equal(WorldMapRegionMaterialZoneCollisionCheckType.None, plan.CheckType);
		Assert.Contains("owner race", plan.JavaSource);
	}

	[Fact]
	public void CreateEnterPlan_FiltersMaterialSkillsAndUsesPassForLandingShieldMaterials()
	{
		var plan = WorldMapRegionMaterialZoneHandlerPlanService.CreateEnterPlan(CreateContext(
			materialId: 15,
			creatureKind: WorldMapRegionMaterialZoneCreatureKind.Summon,
			hasSummonMaster: true,
			showDetailsToStaff: true,
			skills:
			[
				new(101, WorldMapRegionMaterialZoneSkillTarget.Player),
				new(102, WorldMapRegionMaterialZoneSkillTarget.PlayerWithPet),
				new(103, WorldMapRegionMaterialZoneSkillTarget.All),
			]));

		Assert.Equal(WorldMapRegionMaterialZoneHandlerEnterStatus.ObserverRegistered, plan.Status);
		Assert.Equal([102, 103], plan.MatchingSkillIds);
		Assert.Equal(WorldMapRegionMaterialZoneCollisionCheckType.Pass, plan.CheckType);
		Assert.Contains("observer added", plan.SideEffects);
		Assert.Contains("actor.moved invoked", plan.SideEffects);
		Assert.Contains("staff debug message: Entered material zone", plan.SideEffects.Single(effect => effect.StartsWith("staff debug message", StringComparison.Ordinal)));
	}

	[Fact]
	public void CreateEnterPlan_NoMatchingSkillsSkipsObserverRegistration()
	{
		var plan = WorldMapRegionMaterialZoneHandlerPlanService.CreateEnterPlan(CreateContext(
			creatureKind: WorldMapRegionMaterialZoneCreatureKind.Npc,
			skills: [new(101, WorldMapRegionMaterialZoneSkillTarget.Player)]));

		Assert.Equal(WorldMapRegionMaterialZoneHandlerEnterStatus.NoMatchingSkills, plan.Status);
		Assert.Empty(plan.MatchingSkillIds);
		Assert.Empty(plan.SideEffects);
	}

	[Fact]
	public void CreateLeavePlan_RemovesObservedActorAndReportsStaffDebugMessage()
	{
		var plan = WorldMapRegionMaterialZoneHandlerPlanService.CreateLeavePlan(CreateContext(
			showDetailsToStaff: true,
			observedCreatureObjectIds: new HashSet<int> { 100, 200 }));

		Assert.Equal(WorldMapRegionMaterialZoneHandlerLeaveStatus.ObserverRemoved, plan.Status);
		Assert.Equal(
			["observed actor removed", "observer removed", "actor.abort invoked", "staff debug message: Left material zone AION_MATERIAL_FIRE"],
			plan.SideEffects);
		Assert.Contains("onLeaveZone", plan.JavaSource);
	}

	private static WorldMapRegionMaterialZoneHandlerContext CreateContext(
		string geometryName = "AION_MATERIAL_FIRE",
		int materialId = 14,
		int creatureObjectId = 100,
		WorldMapRegionMaterialZoneRace creatureRace = WorldMapRegionMaterialZoneRace.Elyos,
		WorldMapRegionMaterialZoneCreatureKind creatureKind = WorldMapRegionMaterialZoneCreatureKind.Player,
		bool hasSummonMaster = false,
		bool showDetailsToStaff = false,
		IReadOnlyList<WorldMapRegionMaterialZoneSkillSnapshot>? skills = null,
		IReadOnlySet<int>? observedCreatureObjectIds = null)
	{
		return new WorldMapRegionMaterialZoneHandlerContext(
			geometryName,
			materialId,
			creatureObjectId,
			creatureRace,
			creatureKind,
			hasSummonMaster,
			showDetailsToStaff,
			skills ?? [new WorldMapRegionMaterialZoneSkillSnapshot(101, WorldMapRegionMaterialZoneSkillTarget.All)],
			observedCreatureObjectIds ?? new HashSet<int>());
	}
}
