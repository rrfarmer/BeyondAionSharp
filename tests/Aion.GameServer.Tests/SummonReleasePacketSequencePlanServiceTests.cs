using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SummonReleasePacketSequencePlanServiceTests
{
	[Theory]
	[InlineData(SummonReleaseUnsummonType.Command)]
	[InlineData(SummonReleaseUnsummonType.Distance)]
	[InlineData(SummonReleaseUnsummonType.Unspecified)]
	public void CreatePlan_ComposesPanelRemoveThenOwnerRemoveInJavaOrder(SummonReleaseUnsummonType unsummonType)
	{
		var plan = SummonReleasePacketSequencePlanService.CreatePlan(unsummonType, summonedBySkillId: 12001, summonObjectId: 8001);

		Assert.Equal(SummonReleasePacketSequencePlanStatus.SequenceCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToMaster);
		Assert.True(plan.SendsPanelRemoveFirst);
		Assert.True(plan.SendsOwnerRemoveSecond);
		Assert.NotNull(plan.PanelRemovePlan);
		Assert.NotNull(plan.OwnerRemovePlan);
		Assert.Equal(2, plan.PacketsInOrder.Count);
		Assert.IsType<SmSummonPanelRemove>(plan.PacketsInOrder[0]);
		Assert.IsType<SmSummonOwnerRemove>(plan.PacketsInOrder[1]);
		Assert.Equal(SmSummonPanelRemove.PacketOpCode, plan.PacketsInOrder[0].OpCode);
		Assert.Equal(SmSummonOwnerRemove.PacketOpCode, plan.PacketsInOrder[1].OpCode);
		Assert.Contains("SM_SUMMON_PANEL_REMOVE -> SM_SUMMON_OWNER_REMOVE", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_LogoutSkipsReleasePackets()
	{
		var plan = SummonReleasePacketSequencePlanService.CreatePlan(SummonReleaseUnsummonType.Logout, summonedBySkillId: 12001, summonObjectId: 8001);

		Assert.Equal(SummonReleasePacketSequencePlanStatus.SkippedLogout, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.False(plan.SendsPanelRemoveFirst);
		Assert.False(plan.SendsOwnerRemoveSecond);
		Assert.Null(plan.PanelRemovePlan);
		Assert.Null(plan.OwnerRemovePlan);
		Assert.Empty(plan.PacketsInOrder);
		Assert.Contains("LOGOUT", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_BlocksNegativeSkillIdBeforeSequenceCreation()
	{
		var plan = SummonReleasePacketSequencePlanService.CreatePlan(SummonReleaseUnsummonType.Command, summonedBySkillId: -1, summonObjectId: 8001);

		Assert.Equal(SummonReleasePacketSequencePlanStatus.BlockedNegativeSkillId, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.NotNull(plan.PanelRemovePlan);
		Assert.Null(plan.OwnerRemovePlan);
		Assert.Empty(plan.PacketsInOrder);
	}

	[Fact]
	public void CreatePlan_BlocksInvalidSummonObjectIdBeforeSequenceCreation()
	{
		var plan = SummonReleasePacketSequencePlanService.CreatePlan(SummonReleaseUnsummonType.Distance, summonedBySkillId: 12001, summonObjectId: 0);

		Assert.Equal(SummonReleasePacketSequencePlanStatus.BlockedInvalidSummonObjectId, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.NotNull(plan.PanelRemovePlan);
		Assert.NotNull(plan.OwnerRemovePlan);
		Assert.Empty(plan.PacketsInOrder);
	}
}
