using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class StaggerStumbleCalculatePlanServiceTests
{
	[Fact]
	public void CreatePlan_BlocksWhenAnyForcedMoveAbnormalAlreadyExistsLikeJava()
	{
		var plan = StaggerStumbleCalculatePlanService.CreatePlan(CreateInput(
			ForcedMoveEffectKind.Stagger,
			hasStaggerAbnormal: true,
			closestCollision: new GeoCollisionSnapshot(3, 4, 5)));

		Assert.Equal(StaggerStumbleCalculatePlanStatus.BlockedExistingAbnormal, plan.Status);
		Assert.False(plan.ShouldRequestGeoCollision);
		Assert.False(plan.ShouldSetSubEffectType);
		Assert.False(plan.ShouldSetTargetLocation);
		Assert.Equal("STAGGER_RESISTANCE", plan.ResistanceStatName);
		Assert.Equal("STAGGER", plan.SpellStatusName);
	}

	[Fact]
	public void CreatePlan_BlocksWhenBaseCalculateFailsLikeJavaResistanceGate()
	{
		var plan = StaggerStumbleCalculatePlanService.CreatePlan(CreateInput(
			ForcedMoveEffectKind.Stumble,
			baseCalculateSucceeded: false,
			closestCollision: new GeoCollisionSnapshot(3, 4, 5)));

		Assert.Equal(StaggerStumbleCalculatePlanStatus.BlockedCalculateFailed, plan.Status);
		Assert.False(plan.ShouldRequestGeoCollision);
		Assert.False(plan.ShouldSetTargetLocation);
		Assert.Equal("STUMBLE_RESISTANCE", plan.ResistanceStatName);
		Assert.Equal("STUMBLE", plan.SpellStatusName);
	}

	[Fact]
	public void CreatePlan_ForStaggerNpcSubEffect_RequestsTwoMeterGeoProbeAndSetsSubEffectType()
	{
		var plan = StaggerStumbleCalculatePlanService.CreatePlan(CreateInput(
			ForcedMoveEffectKind.Stagger,
			isSubEffect: true,
			isEffectedPlayer: false,
			closestCollision: new GeoCollisionSnapshot(3.25f, 0.5f, 7)));

		Assert.Equal(StaggerStumbleCalculatePlanStatus.PlannedTargetLocation, plan.Status);
		Assert.True(plan.ShouldRequestGeoCollision);
		Assert.True(plan.ShouldSetSubEffectType);
		Assert.Equal("STAGGER", plan.SubEffectTypeName);
		Assert.Equal(0, plan.HeadingTowardsEffected);
		Assert.Equal(0, plan.MovementAngle);
		Assert.Equal(3, plan.RequestedCollisionX);
		Assert.Equal(0, plan.RequestedCollisionY);
		Assert.Equal(7, plan.RequestedCollisionZ);
		Assert.Equal(new GeoCollisionSnapshot(3.25f, 0.5f, 7), plan.TargetLocation);
		Assert.True(plan.ShouldSetTargetLocation);
		Assert.Contains("StaggerEffect.calculate", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_ForStumblePlayer_DoesNotSetSubEffectTypeButKeepsTargetLocation()
	{
		var plan = StaggerStumbleCalculatePlanService.CreatePlan(CreateInput(
			ForcedMoveEffectKind.Stumble,
			isSubEffect: true,
			isEffectedPlayer: true,
			closestCollision: new GeoCollisionSnapshot(3.25f, 0.5f, 7)));

		Assert.Equal(StaggerStumbleCalculatePlanStatus.PlannedTargetLocation, plan.Status);
		Assert.False(plan.ShouldSetSubEffectType);
		Assert.Null(plan.SubEffectTypeName);
		Assert.Equal("STUMBLE_RESISTANCE", plan.ResistanceStatName);
		Assert.Equal("STUMBLE", plan.SpellStatusName);
		Assert.Equal(new GeoCollisionSnapshot(3.25f, 0.5f, 7), plan.TargetLocation);
		Assert.Contains("StumbleEffect.calculate", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_WithoutCollisionResultRecordsGeoDependencyBeforeTargetLocation()
	{
		var plan = StaggerStumbleCalculatePlanService.CreatePlan(CreateInput(
			ForcedMoveEffectKind.Stagger,
			closestCollision: null));

		Assert.Equal(StaggerStumbleCalculatePlanStatus.NeedsGeoCollision, plan.Status);
		Assert.True(plan.ShouldRequestGeoCollision);
		Assert.False(plan.ShouldSetTargetLocation);
		Assert.Null(plan.TargetLocation);
		Assert.Equal(3, plan.RequestedCollisionX);
		Assert.Equal(0, plan.RequestedCollisionY);
		Assert.Equal(7, plan.RequestedCollisionZ);
	}

	private static StaggerStumbleCalculatePlanInput CreateInput(
		ForcedMoveEffectKind effectKind,
		bool hasPulledAbnormal = false,
		bool hasStaggerAbnormal = false,
		bool hasOpenAerialAbnormal = false,
		bool hasStumbleAbnormal = false,
		bool baseCalculateSucceeded = true,
		bool isSubEffect = false,
		bool isEffectedPlayer = false,
		GeoCollisionSnapshot? closestCollision = null)
	{
		return new StaggerStumbleCalculatePlanInput(
			effectKind,
			new ObjectPositionSnapshot(ObjectId: 8002, X: 1, Y: 0, Z: 7, Heading: 105),
			EffectorX: 0,
			EffectorY: 0,
			hasPulledAbnormal,
			hasStaggerAbnormal,
			hasOpenAerialAbnormal,
			hasStumbleAbnormal,
			baseCalculateSucceeded,
			isSubEffect,
			isEffectedPlayer,
			closestCollision);
	}
}
