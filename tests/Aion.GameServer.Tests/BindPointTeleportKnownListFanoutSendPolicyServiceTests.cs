using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKnownListFanoutSendPolicyServiceTests
{
	[Fact]
	public void CreatePolicy_UsesOnlineGateForSourceAndKnownListRecipients()
	{
		var trace = CreateTrace();

		var policy = BindPointTeleportKnownListFanoutSendPolicyService.CreatePolicy(
			trace,
			onlinePlayerObjectIds: [SourcePlayerObjectId, KnownVisiblePlayerObjectId]);

		Assert.Equal(BindPointTeleportKnownListFanoutSendPolicyStatus.Projected, policy.Status);
		Assert.True(policy.UsesPacketSendUtilitySendPacket);
		Assert.True(policy.UsesPlayerIsOnlineGate);
		Assert.False(policy.IsLive);
		Assert.Equal(
			[
				BindPointTeleportKnownListFanoutRecipientSendStatus.WouldSend,
				BindPointTeleportKnownListFanoutRecipientSendStatus.WouldSend,
				BindPointTeleportKnownListFanoutRecipientSendStatus.SkippedOffline,
			],
			policy.Recipients.Select(recipient => recipient.Status));
		Assert.All(policy.Recipients, recipient => Assert.True(recipient.UsesPlayerIsOnlineGate));
	}

	[Fact]
	public void CreatePolicy_ModelsKnownListRecipientFailureAsLogAndContinue()
	{
		var trace = CreateTrace();

		var policy = BindPointTeleportKnownListFanoutSendPolicyService.CreatePolicy(
			trace,
			onlinePlayerObjectIds: [SourcePlayerObjectId, KnownVisiblePlayerObjectId, KnownInvisiblePlayerObjectId],
			failingPlayerObjectIds: [KnownVisiblePlayerObjectId],
			failureReason: "send failed");

		Assert.True(policy.ContinuesAfterRecipientFailure);
		Assert.Equal(
			[
				BindPointTeleportKnownListFanoutRecipientSendStatus.WouldSend,
				BindPointTeleportKnownListFanoutRecipientSendStatus.FailedAndContinued,
				BindPointTeleportKnownListFanoutRecipientSendStatus.WouldSend,
			],
			policy.Recipients.Select(recipient => recipient.Status));
		Assert.Equal("send failed", policy.Recipients[1].FailureReason);
		Assert.All(policy.Recipients, recipient => Assert.True(recipient.ContinuesAfterFailure));
	}

	[Fact]
	public void CreatePolicy_NoPacketTraceDoesNotProjectRecipientSends()
	{
		var trace = BindPointTeleportKnownListFanoutTraceService.CreateTrace(
			fanoutPlan: null,
			knownListPlayerObjectIds: [KnownVisiblePlayerObjectId]);

		var policy = BindPointTeleportKnownListFanoutSendPolicyService.CreatePolicy(
			trace,
			onlinePlayerObjectIds: [KnownVisiblePlayerObjectId]);

		Assert.Equal(BindPointTeleportKnownListFanoutSendPolicyStatus.NoPacket, policy.Status);
		Assert.False(policy.UsesPacketSendUtilitySendPacket);
		Assert.True(policy.UsesPlayerIsOnlineGate);
		Assert.True(policy.ContinuesAfterRecipientFailure);
		Assert.Empty(policy.Recipients);
		Assert.False(policy.IsLive);
	}

	private static BindPointTeleportKnownListFanoutTrace CreateTrace()
	{
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			SourcePlayerObjectId,
			SmBindPointTeleport.Cooldown(SourcePlayerObjectId, LocId, CooldownSeconds));

		return BindPointTeleportKnownListFanoutTraceService.CreateTrace(
			fanoutPlan,
			[KnownVisiblePlayerObjectId, KnownInvisiblePlayerObjectId]);
	}

	private const int SourcePlayerObjectId = 8701;
	private const int KnownVisiblePlayerObjectId = 8702;
	private const int KnownInvisiblePlayerObjectId = 8703;
	private const int LocId = 6501;
	private const int CooldownSeconds = 600;
}
