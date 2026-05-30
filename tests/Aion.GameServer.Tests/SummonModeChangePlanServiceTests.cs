using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SummonModeChangePlanServiceTests
{
	[Theory]
	[InlineData(SummonModeChangeType.Rest, 1200010, "STR_SKILL_SUMMON_REST_MODE")]
	[InlineData(SummonModeChangeType.Guard, 1200009, "STR_SKILL_SUMMON_GUARD_MODE")]
	[InlineData(SummonModeChangeType.Attack, 1200008, "STR_SKILL_SUMMON_ATTACK_MODE")]
	public void CreatePlan_ComposesModeModeMessageAheadOfSummonUpdate(SummonModeChangeType modeChangeType, int expectedMessageId, string javaSourceFragment)
	{
		var plan = SummonModeChangePlanService.CreatePlan(modeChangeType, "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonModeChangePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToMaster);
		Assert.NotNull(plan.ModeMessage);
		Assert.NotNull(plan.SummonUpdatePacketPlan);
		Assert.Equal(SummonUpdatePacketPlanStatus.PacketCreated, plan.SummonUpdatePacketPlan!.Status);
		Assert.Equal(expectedMessageId, plan.ModeMessage!.MessageId);
		Assert.Equal(2, plan.ImmediatePacketsInOrder.Count);
		Assert.Equal(expectedMessageId, Assert.IsType<SmSystemMessage>(plan.ImmediatePacketsInOrder[0]).MessageId);
		Assert.IsType<SmSummonUpdate>(plan.ImmediatePacketsInOrder[1]);
		Assert.Contains(javaSourceFragment, plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_UnkModeSkipsModeMessageAndSendsOnlySummonUpdate()
	{
		var plan = SummonModeChangePlanService.CreatePlan(SummonModeChangeType.Unk, null, CreateSnapshot());

		Assert.Equal(SummonModeChangePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToMaster);
		Assert.Null(plan.ModeMessage);
		Assert.NotNull(plan.SummonUpdatePacketPlan);
		Assert.Equal(SummonUpdatePacketPlanStatus.PacketCreated, plan.SummonUpdatePacketPlan!.Status);
		Assert.Single(plan.ImmediatePacketsInOrder);
		Assert.IsType<SmSummonUpdate>(plan.ImmediatePacketsInOrder[0]);
		Assert.Contains("setUnkMode", plan.JavaSource);
	}

	[Theory]
	[InlineData(SummonModeChangeType.Rest)]
	[InlineData(SummonModeChangeType.Guard)]
	[InlineData(SummonModeChangeType.Attack)]
	public void CreatePlan_BlocksEmptySummonNameForModesThatRequireIt(SummonModeChangeType modeChangeType)
	{
		var plan = SummonModeChangePlanService.CreatePlan(modeChangeType, " ", CreateSnapshot());

		Assert.Equal(SummonModeChangePlanStatus.BlockedEmptySummonName, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.Null(plan.ModeMessage);
		Assert.Null(plan.SummonUpdatePacketPlan);
		Assert.Empty(plan.ImmediatePacketsInOrder);
	}

	[Theory]
	[InlineData(SummonModeChangeType.Rest)]
	[InlineData(SummonModeChangeType.Guard)]
	[InlineData(SummonModeChangeType.Attack)]
	[InlineData(SummonModeChangeType.Unk)]
	public void CreatePlan_BlocksInvalidSummonUpdateSnapshotBeforeCompositeSend(SummonModeChangeType modeChangeType)
	{
		var invalidSnapshot = CreateSnapshot() with { MagicCritical = new SummonUpdateStatSnapshot(Current: 12, Base: -1) };
		var summonName = modeChangeType != SummonModeChangeType.Unk ? "Wind Spirit" : null;

		var plan = SummonModeChangePlanService.CreatePlan(modeChangeType, summonName, invalidSnapshot);

		Assert.Equal(SummonModeChangePlanStatus.BlockedInvalidSummonUpdateSnapshot, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.Null(plan.ModeMessage);
		Assert.NotNull(plan.SummonUpdatePacketPlan);
		Assert.Equal(SummonUpdatePacketPlanStatus.BlockedInvalidSnapshot, plan.SummonUpdatePacketPlan!.Status);
		Assert.Empty(plan.ImmediatePacketsInOrder);
	}

	[Theory]
	[InlineData(SummonModeChangeType.Rest, "triggerRestoreTask")]
	[InlineData(SummonModeChangeType.Guard, "triggerRestoreTask")]
	[InlineData(SummonModeChangeType.Attack, "cancelRestoreTask")]
	public void CreatePlan_RecordsDeferredLifeStatsCallInJavaSource(SummonModeChangeType modeChangeType, string deferredFragment)
	{
		var plan = SummonModeChangePlanService.CreatePlan(modeChangeType, "Wind Spirit", CreateSnapshot());

		Assert.Equal(SummonModeChangePlanStatus.PlanCreated, plan.Status);
		Assert.Contains(deferredFragment, plan.JavaSource);
		Assert.Contains("[deferred]", plan.JavaSource);
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
