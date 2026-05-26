using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKnownListFanoutExecutionPlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_ComposesMembershipTraceAndSendPolicyWithoutSendingPackets()
	{
		var membership = new PlayerKnownListMembershipService();
		var snapshot = membership.UpsertKnownPlayers(
			SourcePlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(KnownVisiblePlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(KnownInvisiblePlayerObjectId, IsVisibleToOwner: false),
			]);
		var fanoutPlan = CreateFanoutPlan();

		var plan = BindPointTeleportKnownListFanoutExecutionPlanService.CreateDisabledPlan(
			fanoutPlan,
			snapshot,
			onlinePlayerObjectIds: [SourcePlayerObjectId, KnownVisiblePlayerObjectId]);

		Assert.Equal(BindPointTeleportKnownListFanoutExecutionPlanStatus.Disabled, plan.Status);
		Assert.False(plan.SendsPackets);
		Assert.False(plan.IsLive);
		Assert.True(plan.UsesMembershipSnapshot);
		Assert.True(plan.UsesSourceFirstOrdering);
		Assert.Equal(
			[SourcePlayerObjectId, KnownVisiblePlayerObjectId, KnownInvisiblePlayerObjectId],
			plan.Trace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.Equal(
			[
				BindPointTeleportKnownListFanoutRecipientSendStatus.WouldSend,
				BindPointTeleportKnownListFanoutRecipientSendStatus.WouldSend,
				BindPointTeleportKnownListFanoutRecipientSendStatus.SkippedOffline,
			],
			plan.SendPolicy.Recipients.Select(recipient => recipient.Status));
	}

	[Fact]
	public void CreateDisabledPlan_PropagatesRecipientFailurePolicyAndContinuesProjection()
	{
		var membership = new PlayerKnownListMembershipService();
		var snapshot = membership.UpsertKnownPlayers(
			SourcePlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(KnownVisiblePlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(KnownInvisiblePlayerObjectId, IsVisibleToOwner: false),
			]);

		var plan = BindPointTeleportKnownListFanoutExecutionPlanService.CreateDisabledPlan(
			CreateFanoutPlan(),
			snapshot,
			onlinePlayerObjectIds: [SourcePlayerObjectId, KnownVisiblePlayerObjectId, KnownInvisiblePlayerObjectId],
			failingPlayerObjectIds: [KnownVisiblePlayerObjectId],
			failureReason: "send failed");

		Assert.True(plan.ContinuesAfterRecipientFailure);
		Assert.Equal(BindPointTeleportKnownListFanoutRecipientSendStatus.FailedAndContinued, plan.SendPolicy.Recipients[1].Status);
		Assert.Equal(BindPointTeleportKnownListFanoutRecipientSendStatus.WouldSend, plan.SendPolicy.Recipients[2].Status);
		Assert.Equal("send failed", plan.SendPolicy.Recipients[1].FailureReason);
	}

	[Fact]
	public void CreateDisabledPlan_WithoutPacketPlanReturnsNoPacketAndNoRecipients()
	{
		var plan = BindPointTeleportKnownListFanoutExecutionPlanService.CreateDisabledPlan(
			fanoutPlan: null,
			membershipSnapshot: null,
			onlinePlayerObjectIds: [SourcePlayerObjectId]);

		Assert.Equal(BindPointTeleportKnownListFanoutExecutionPlanStatus.NoPacket, plan.Status);
		Assert.False(plan.SendsPackets);
		Assert.False(plan.UsesMembershipSnapshot);
		Assert.Empty(plan.Trace.Recipients);
		Assert.Empty(plan.SendPolicy.Recipients);
		Assert.False(plan.IsLive);
	}

	private static BindPointTeleportFanoutPlan CreateFanoutPlan() =>
		BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			SourcePlayerObjectId,
			SmBindPointTeleport.Cooldown(SourcePlayerObjectId, LocId, CooldownSeconds));

	private const int SourcePlayerObjectId = 8801;
	private const int KnownVisiblePlayerObjectId = 8802;
	private const int KnownInvisiblePlayerObjectId = 8803;
	private const int LocId = 6501;
	private const int CooldownSeconds = 600;
}
