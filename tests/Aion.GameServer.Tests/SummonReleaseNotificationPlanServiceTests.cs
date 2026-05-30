using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SummonReleaseNotificationPlanServiceTests
{
	[Fact]
	public void CreatePlan_DistanceComposesTooDistanceMessageAheadOfReleasePackets()
	{
		var plan = SummonReleaseNotificationPlanService.CreatePlan(
			SummonReleaseUnsummonType.Distance,
			summonName: null,
			summonedBySkillId: 12001,
			summonObjectId: 8001
		);

		Assert.Equal(SummonReleaseNotificationPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToMaster);
		Assert.NotNull(plan.NotificationPacket);
		Assert.NotNull(plan.ReleasePacketSequencePlan);
		Assert.Equal(1300073, plan.NotificationPacket.MessageId);
		Assert.Equal(3, plan.PacketsInOrder.Count);
		Assert.Equal(1300073, Assert.IsType<SmSystemMessage>(plan.PacketsInOrder[0]).MessageId);
		Assert.IsType<SmSummonPanelRemove>(plan.PacketsInOrder[1]);
		Assert.IsType<SmSummonOwnerRemove>(plan.PacketsInOrder[2]);
		Assert.Contains("STR_SKILL_SUMMON_UNSUMMON_BY_TOO_DISTANCE", plan.JavaSource);
	}

	[Theory]
	[InlineData(SummonReleaseUnsummonType.Command)]
	[InlineData(SummonReleaseUnsummonType.Unspecified)]
	public void CreatePlan_CommandLikeBranchesComposeUnsummonedMessageAheadOfReleasePackets(SummonReleaseUnsummonType unsummonType)
	{
		var plan = SummonReleaseNotificationPlanService.CreatePlan(
			unsummonType,
			summonName: "Wind Spirit",
			summonedBySkillId: 12001,
			summonObjectId: 8001
		);

		Assert.Equal(SummonReleaseNotificationPlanStatus.PlanCreated, plan.Status);
		Assert.True(plan.ShouldSendToMaster);
		Assert.NotNull(plan.NotificationPacket);
		Assert.NotNull(plan.ReleasePacketSequencePlan);
		Assert.Equal(1200006, plan.NotificationPacket.MessageId);
		Assert.Equal(3, plan.PacketsInOrder.Count);
		Assert.Equal(1200006, Assert.IsType<SmSystemMessage>(plan.PacketsInOrder[0]).MessageId);
		Assert.IsType<SmSummonPanelRemove>(plan.PacketsInOrder[1]);
		Assert.IsType<SmSummonOwnerRemove>(plan.PacketsInOrder[2]);
		Assert.Contains("STR_SKILL_SUMMON_UNSUMMONED", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_LogoutSkipsNotificationAndReleasePackets()
	{
		var plan = SummonReleaseNotificationPlanService.CreatePlan(
			SummonReleaseUnsummonType.Logout,
			summonName: "Wind Spirit",
			summonedBySkillId: 12001,
			summonObjectId: 8001
		);

		Assert.Equal(SummonReleaseNotificationPlanStatus.SkippedLogout, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.Null(plan.NotificationPacket);
		Assert.Null(plan.ReleasePacketSequencePlan);
		Assert.Empty(plan.PacketsInOrder);
	}

	[Theory]
	[InlineData(SummonReleaseUnsummonType.Command)]
	[InlineData(SummonReleaseUnsummonType.Unspecified)]
	public void CreatePlan_CommandLikeBranchesBlockEmptySummonName(SummonReleaseUnsummonType unsummonType)
	{
		var plan = SummonReleaseNotificationPlanService.CreatePlan(unsummonType, summonName: " ", summonedBySkillId: 12001, summonObjectId: 8001);

		Assert.Equal(SummonReleaseNotificationPlanStatus.BlockedEmptySummonName, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.Null(plan.NotificationPacket);
		Assert.Null(plan.ReleasePacketSequencePlan);
		Assert.Empty(plan.PacketsInOrder);
	}

	[Fact]
	public void CreatePlan_BlocksNegativeSkillIdBeforeCompositeSend()
	{
		var plan = SummonReleaseNotificationPlanService.CreatePlan(
			SummonReleaseUnsummonType.Distance,
			summonName: null,
			summonedBySkillId: -1,
			summonObjectId: 8001
		);

		Assert.Equal(SummonReleaseNotificationPlanStatus.BlockedNegativeSkillId, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.NotNull(plan.NotificationPacket);
		Assert.NotNull(plan.ReleasePacketSequencePlan);
		Assert.Empty(plan.PacketsInOrder);
	}
}
