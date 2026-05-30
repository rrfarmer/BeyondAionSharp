using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SummonCommandReleaseSchedulePlanServiceTests
{
	[Fact]
	public void CreatePlan_ComposesFollowerWarningAheadOfSummonUpdateAndStoresReleaseTaskIntent()
	{
		var plan = SummonCommandReleaseSchedulePlanService.CreatePlan("Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonCommandReleaseSchedulePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToMaster);
		Assert.True(plan.ShouldStoreReleaseTask);
		Assert.Equal(5000, plan.ScheduledReleaseDelayMilliseconds);
		Assert.NotNull(plan.WarningPacket);
		Assert.NotNull(plan.SummonUpdatePacketPlan);
		Assert.Equal(SummonUpdatePacketPlanStatus.PacketCreated, plan.SummonUpdatePacketPlan!.Status);
		Assert.Equal(1200011, plan.WarningPacket!.MessageId);
		Assert.Equal(2, plan.ImmediatePacketsInOrder.Count);
		Assert.Equal(1200011, Assert.IsType<SmSystemMessage>(plan.ImmediatePacketsInOrder[0]).MessageId);
		Assert.IsType<SmSummonUpdate>(plan.ImmediatePacketsInOrder[1]);
		Assert.Contains("ThreadPoolManager.schedule(this, 5000)", plan.JavaSource);
		Assert.Contains("STR_SKILL_SUMMON_UNSUMMON_FOLLOWER", plan.JavaSource);
		Assert.Contains("summon.setReleaseTask", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_BlocksEmptySummonName()
	{
		var plan = SummonCommandReleaseSchedulePlanService.CreatePlan(" ", CreateSnapshot());

		Assert.Equal(SummonCommandReleaseSchedulePlanStatus.BlockedEmptySummonName, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.False(plan.ShouldStoreReleaseTask);
		Assert.Equal(0, plan.ScheduledReleaseDelayMilliseconds);
		Assert.Null(plan.WarningPacket);
		Assert.Null(plan.SummonUpdatePacketPlan);
		Assert.Empty(plan.ImmediatePacketsInOrder);
	}

	[Fact]
	public void CreatePlan_BlocksInvalidSummonUpdateSnapshotBeforeCompositeSend()
	{
		var invalidSnapshot = CreateSnapshot() with { MagicCritical = new SummonUpdateStatSnapshot(Current: 12, Base: -1) };

		var plan = SummonCommandReleaseSchedulePlanService.CreatePlan("Wind Spirit", invalidSnapshot);

		Assert.Equal(SummonCommandReleaseSchedulePlanStatus.BlockedInvalidSummonUpdateSnapshot, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.False(plan.ShouldStoreReleaseTask);
		Assert.Equal(0, plan.ScheduledReleaseDelayMilliseconds);
		Assert.NotNull(plan.WarningPacket);
		Assert.NotNull(plan.SummonUpdatePacketPlan);
		Assert.Equal(SummonUpdatePacketPlanStatus.BlockedInvalidSnapshot, plan.SummonUpdatePacketPlan!.Status);
		Assert.Empty(plan.ImmediatePacketsInOrder);
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
