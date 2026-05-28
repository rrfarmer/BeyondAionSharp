using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionMaterialZoneActorPlanServiceTests
{
	[Fact]
	public void CreateMovePlan_TouchTransitionSchedulesTaskAndReportsClosestGeometry()
	{
		var plan = WorldMapRegionMaterialZoneActorPlanService.CreateMovePlan(new WorldMapRegionMaterialZoneActorMoveContext(
			GeometryName: "AION_MATERIAL_FIRE",
			CollisionGeometryNames: ["FIRE_COLLISION_A", "FIRE_COLLISION_B"],
			WasTouched: false,
			HasScheduledTask: false,
			ShowDetailsToStaff: true,
			Skills: [Skill(101)]));

		Assert.Equal(WorldMapRegionMaterialZoneActorMoveStatus.TouchStarted, plan.Status);
		Assert.True(plan.IsTouched);
		Assert.Equal("Touched FIRE_COLLISION_A", plan.DebugMessage);
		Assert.Equal(
			["ThreadPoolManager.scheduleAtFixedRate", "creature controller task added: ZONE_MATERIAL_ACTION", "staff debug message: Touched FIRE_COLLISION_A"],
			plan.SideEffects);
		Assert.Contains("ZoneCollisionMaterialActor.onMoved", plan.JavaSource);
	}

	[Fact]
	public void CreateMovePlan_UntouchTransitionAbortsTaskAndReportsMaterialGeometry()
	{
		var plan = WorldMapRegionMaterialZoneActorPlanService.CreateMovePlan(new WorldMapRegionMaterialZoneActorMoveContext(
			GeometryName: "AION_MATERIAL_FIRE",
			CollisionGeometryNames: [],
			WasTouched: true,
			HasScheduledTask: true,
			ShowDetailsToStaff: true,
			Skills: [Skill(101)]));

		Assert.Equal(WorldMapRegionMaterialZoneActorMoveStatus.TouchEnded, plan.Status);
		Assert.False(plan.IsTouched);
		Assert.Equal("Untouched AION_MATERIAL_FIRE", plan.DebugMessage);
		Assert.Equal(
			["creature controller task cancelled: ZONE_MATERIAL_ACTION", "staff debug message: Untouched AION_MATERIAL_FIRE"],
			plan.SideEffects);
	}

	[Fact]
	public void CreateTickPlan_HonorsJavaFrequencyTouchAndProtectionGuards()
	{
		var frequency = WorldMapRegionMaterialZoneActorPlanService.CreateTickPlan(CreateTickContext(
			secondsElapsed: 3,
			previousSkillFrequency: 2));
		var notTouched = WorldMapRegionMaterialZoneActorPlanService.CreateTickPlan(CreateTickContext(isTouched: false));
		var inactive = WorldMapRegionMaterialZoneActorPlanService.CreateTickPlan(CreateTickContext(isSpawned: false));
		var protectedPlayer = WorldMapRegionMaterialZoneActorPlanService.CreateTickPlan(CreateTickContext(isPlayerProtectionActive: true));

		Assert.Equal(WorldMapRegionMaterialZoneActorTickStatus.SkippedFrequencyGate, frequency.Status);
		Assert.Equal(WorldMapRegionMaterialZoneActorTickStatus.SkippedNotTouched, notTouched.Status);
		Assert.Equal(WorldMapRegionMaterialZoneActorTickStatus.SkippedInactiveCreature, inactive.Status);
		Assert.Equal(WorldMapRegionMaterialZoneActorTickStatus.SkippedPlayerProtection, protectedPlayer.Status);
	}

	[Fact]
	public void CreateTickPlan_SelectsFirstSkillWithMatchingConditionAndAppliesMaterialSkill()
	{
		var plan = WorldMapRegionMaterialZoneActorPlanService.CreateTickPlan(CreateTickContext(
			showDetailsToStaff: true,
			dayTime: WorldMapRegionMaterialZoneDayTime.Day,
			weatherName: "RAIN_HEAVY",
			weatherIsBefore: true,
			skills:
			[
				Skill(101, conditions: [WorldMapRegionMaterialZoneActCondition.Night]),
				Skill(102, level: 3, conditions: [WorldMapRegionMaterialZoneActCondition.Sunny]),
				Skill(103),
			]));

		Assert.Equal(WorldMapRegionMaterialZoneActorTickStatus.SkillApplied, plan.Status);
		Assert.Equal(102, plan.AppliedSkillId);
		Assert.Equal(3, plan.AppliedSkillLevel);
		Assert.Equal(WorldMapRegionMaterialZoneActorPlanService.ForceType, plan.AppliedForceType);
		Assert.Equal(
			["staff debug message: ZoneCollisionMaterialActor use skill=102", "SkillEngine.applyEffectDirectly 102:3 MATERIAL_SKILL"],
			plan.SideEffects);
	}

	[Fact]
	public void CreateTickPlan_SkipsWhenNoMaterialActConditionMatches()
	{
		var plan = WorldMapRegionMaterialZoneActorPlanService.CreateTickPlan(CreateTickContext(
			dayTime: WorldMapRegionMaterialZoneDayTime.Day,
			weatherName: "RAIN_HEAVY",
			weatherIsBefore: false,
			skills:
			[
				Skill(101, conditions: [WorldMapRegionMaterialZoneActCondition.Night]),
				Skill(102, conditions: [WorldMapRegionMaterialZoneActCondition.Sunny]),
			]));

		Assert.Equal(WorldMapRegionMaterialZoneActorTickStatus.SkippedNoMatchingCondition, plan.Status);
		Assert.Null(plan.AppliedSkillId);
		Assert.Empty(plan.SideEffects);
	}

	private static WorldMapRegionMaterialZoneActorTickContext CreateTickContext(
		int secondsElapsed = 0,
		int? previousSkillFrequency = null,
		bool isTouched = true,
		bool isSpawned = true,
		bool isDead = false,
		bool isPlayerProtectionActive = false,
		bool showDetailsToStaff = false,
		WorldMapRegionMaterialZoneDayTime dayTime = WorldMapRegionMaterialZoneDayTime.Day,
		string? weatherName = null,
		bool weatherIsBefore = false,
		IReadOnlyList<WorldMapRegionMaterialZoneActorSkillSnapshot>? skills = null)
	{
		return new WorldMapRegionMaterialZoneActorTickContext(
			secondsElapsed,
			previousSkillFrequency,
			isTouched,
			isSpawned,
			isDead,
			isPlayerProtectionActive,
			showDetailsToStaff,
			dayTime,
			weatherName,
			weatherIsBefore,
			skills ?? [Skill(101)]);
	}

	private static WorldMapRegionMaterialZoneActorSkillSnapshot Skill(
		int skillId,
		int level = 1,
		int frequency = 1,
		IReadOnlyList<WorldMapRegionMaterialZoneActCondition>? conditions = null)
	{
		return new WorldMapRegionMaterialZoneActorSkillSnapshot(
			skillId,
			level,
			frequency,
			conditions ?? []);
	}
}
