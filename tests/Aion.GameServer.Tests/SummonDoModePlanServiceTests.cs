using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SummonDoModePlanServiceTests
{
	[Fact]
	public void CreatePlan_SkipsDeadSummonBeforeAllOtherGuards()
	{
		var plan = SummonDoModePlanService.CreatePlan(
			SummonDoModeType.Rest, isDead: true, hasMaster: true,
			unsummonType: null, targetObjectId: 0, summonName: "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonDoModePlanStatus.SkippedDeadSummon, plan.Status);
		Assert.False(plan.ShouldCancelReleaseTask);
		Assert.Null(plan.ModeChangePlan);
		Assert.Null(plan.ReleasedUnsummonType);
		Assert.Contains("isDead", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_SkipsNullMasterAfterDeadGuard()
	{
		var plan = SummonDoModePlanService.CreatePlan(
			SummonDoModeType.Guard, isDead: false, hasMaster: false,
			unsummonType: null, targetObjectId: 0, summonName: "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonDoModePlanStatus.SkippedNoMaster, plan.Status);
		Assert.False(plan.ShouldCancelReleaseTask);
		Assert.Null(plan.ModeChangePlan);
		Assert.Null(plan.ReleasedUnsummonType);
		Assert.Contains("getMaster", plan.JavaSource);
	}

	[Theory]
	[InlineData(SummonDoModeType.Rest)]
	[InlineData(SummonDoModeType.Guard)]
	[InlineData(SummonDoModeType.Attack)]
	[InlineData(SummonDoModeType.Unk)]
	public void CreatePlan_CommandUnsummonTypeSetsCancelReleaseTaskForNonReleaseModes(SummonDoModeType modeType)
	{
		var plan = SummonDoModePlanService.CreatePlan(
			modeType, isDead: false, hasMaster: true,
			unsummonType: SummonReleaseUnsummonType.Command, targetObjectId: 0, summonName: "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonDoModePlanStatus.PlanCreated, plan.Status);
		Assert.True(plan.ShouldCancelReleaseTask);
	}

	[Theory]
	[InlineData(SummonReleaseUnsummonType.Distance)]
	[InlineData(SummonReleaseUnsummonType.Logout)]
	[InlineData(SummonReleaseUnsummonType.Unspecified)]
	public void CreatePlan_NonCommandUnsummonTypeDoesNotSetCancelReleaseTask(SummonReleaseUnsummonType unsummonType)
	{
		var plan = SummonDoModePlanService.CreatePlan(
			SummonDoModeType.Rest, isDead: false, hasMaster: true,
			unsummonType, targetObjectId: 0, summonName: "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonDoModePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.ShouldCancelReleaseTask);
	}

	[Fact]
	public void CreatePlan_NullUnsummonTypeDoesNotSetCancelReleaseTask()
	{
		var plan = SummonDoModePlanService.CreatePlan(
			SummonDoModeType.Rest, isDead: false, hasMaster: true,
			unsummonType: null, targetObjectId: 0, summonName: "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonDoModePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.ShouldCancelReleaseTask);
	}

	[Theory]
	[InlineData(SummonDoModeType.Rest)]
	[InlineData(SummonDoModeType.Guard)]
	[InlineData(SummonDoModeType.Attack)]
	[InlineData(SummonDoModeType.Unk)]
	public void CreatePlan_RoutesModeChangeToModeChangePlan(SummonDoModeType modeType)
	{
		var summonName = modeType != SummonDoModeType.Unk ? "Wind Spirit" : null;

		var plan = SummonDoModePlanService.CreatePlan(
			modeType, isDead: false, hasMaster: true,
			unsummonType: null, targetObjectId: 0, summonName: summonName, CreateSnapshot());

		Assert.Equal(SummonDoModePlanStatus.PlanCreated, plan.Status);
		Assert.NotNull(plan.ModeChangePlan);
		Assert.Equal(SummonModeChangePlanStatus.PlanCreated, plan.ModeChangePlan!.Status);
		Assert.Null(plan.ReleasedUnsummonType);
	}

	[Fact]
	public void CreatePlan_ReleaseModeWithCommandUnsummonTypeDoesNotSetCancelReleaseTask()
	{
		// Java: COMMAND+RELEASE does NOT call cancelReleaseTask (the release IS the task)
		var plan = SummonDoModePlanService.CreatePlan(
			SummonDoModeType.Release, isDead: false, hasMaster: true,
			unsummonType: SummonReleaseUnsummonType.Command, targetObjectId: 0, summonName: "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonDoModePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.ShouldCancelReleaseTask);
		Assert.Equal(SummonReleaseUnsummonType.Command, plan.ReleasedUnsummonType);
		Assert.Null(plan.ModeChangePlan);
	}

	[Fact]
	public void CreatePlan_ReleaseModeWithNullUnsummonTypeSkipsRelease()
	{
		// Java: RELEASE + null unsummonType = no-op (controller.release not called)
		var plan = SummonDoModePlanService.CreatePlan(
			SummonDoModeType.Release, isDead: false, hasMaster: true,
			unsummonType: null, targetObjectId: 0, summonName: "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonDoModePlanStatus.SkippedReleaseNullUnsummonType, plan.Status);
		Assert.False(plan.ShouldCancelReleaseTask);
		Assert.Null(plan.ReleasedUnsummonType);
		Assert.Null(plan.ModeChangePlan);
	}

	[Fact]
	public void CreatePlan_AttackModeRecordsTargetObjectIdInSource()
	{
		var plan = SummonDoModePlanService.CreatePlan(
			SummonDoModeType.Attack, isDead: false, hasMaster: true,
			unsummonType: null, targetObjectId: 42, summonName: "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonDoModePlanStatus.PlanCreated, plan.Status);
		Assert.Equal(42, plan.TargetObjectId);
		Assert.Contains("targetObjId=42", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_NoSnapshotSkipsModeChangePlan()
	{
		var plan = SummonDoModePlanService.CreatePlan(
			SummonDoModeType.Rest, isDead: false, hasMaster: true,
			unsummonType: null, targetObjectId: 0, summonName: "Wind Spirit", summonUpdateSnapshot: null);

		Assert.Equal(SummonDoModePlanStatus.PlanCreated, plan.Status);
		Assert.Null(plan.ModeChangePlan);
	}

	private static SummonUpdateSnapshot CreateSnapshot()
	{
		return new SummonUpdateSnapshot(
			Level: 55,
			Mode: SummonUpdateModeId.Release,
			CurrentHp: 7100,
			MaxHp: new SummonUpdateStatSnapshot(Current: 8200, Base: 7600),
			MainHandPhysicalAttack: new SummonUpdateStatSnapshot(Current: 415, Base: 390),
			PhysicalDefense: new SummonUpdateStatSnapshot(Current: 771, Base: 720),
			MagicResist: new SummonUpdateStatSnapshot(Current: 490, Base: 440),
			MagicDefense: new SummonUpdateStatSnapshot(Current: 615, Base: 580),
			MainHandPhysicalAccuracy: new SummonUpdateStatSnapshot(Current: 1320, Base: 1275),
			MainHandPhysicalCritical: new SummonUpdateStatSnapshot(Current: 250, Base: 230),
			MagicBoost: new SummonUpdateStatSnapshot(Current: 950, Base: 900),
			MagicBoostResist: new SummonUpdateStatSnapshot(Current: 85, Base: 70),
			MagicAccuracy: new SummonUpdateStatSnapshot(Current: 1100, Base: 1050),
			MagicCritical: new SummonUpdateStatSnapshot(Current: 120, Base: 100),
			Parry: new SummonUpdateStatSnapshot(Current: 330, Base: 300),
			Evasion: new SummonUpdateStatSnapshot(Current: 405, Base: 375));
	}
}
