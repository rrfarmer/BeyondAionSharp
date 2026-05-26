using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKnownListFanoutMembershipAdapterServiceTests
{
	[Fact]
	public void CreateTrace_ProjectsMembershipSnapshotIntoSourceFirstKnownListTrace()
	{
		var membership = new PlayerKnownListMembershipService();
		var membershipSnapshot = membership.UpsertKnownPlayers(
			SourcePlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(KnownVisiblePlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(KnownInvisiblePlayerObjectId, IsVisibleToOwner: false),
			]);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			SourcePlayerObjectId,
			SmBindPointTeleport.Cooldown(SourcePlayerObjectId, LocId, CooldownSeconds));

		var trace = BindPointTeleportKnownListFanoutMembershipAdapterService.CreateTrace(fanoutPlan, membershipSnapshot);

		Assert.Equal(BindPointTeleportKnownListFanoutTraceStatus.Projected, trace.Status);
		Assert.True(trace.SendsSourceFirst);
		Assert.True(trace.UsesKnownListTraversal);
		Assert.False(trace.IsLive);
		Assert.Equal(
			[SourcePlayerObjectId, KnownVisiblePlayerObjectId, KnownInvisiblePlayerObjectId],
			trace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.Equal(BindPointTeleportKnownListFanoutRecipientKind.SourceSelf, trace.Recipients[0].Kind);
		Assert.All(trace.Recipients.Skip(1), recipient =>
			Assert.Equal(BindPointTeleportKnownListFanoutRecipientKind.KnownListPlayer, recipient.Kind));
	}

	[Fact]
	public void CreateTrace_WithoutMembershipSnapshotReturnsSourceOnlyProjectedTrace()
	{
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			SourcePlayerObjectId,
			SmBindPointTeleport.Cooldown(SourcePlayerObjectId, LocId, CooldownSeconds));

		var trace = BindPointTeleportKnownListFanoutMembershipAdapterService.CreateTrace(
			fanoutPlan,
			membershipSnapshot: null);

		Assert.Equal(BindPointTeleportKnownListFanoutTraceStatus.Projected, trace.Status);
		Assert.Equal([SourcePlayerObjectId], trace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.True(trace.SendsSourceFirst);
	}

	private const int SourcePlayerObjectId = 8601;
	private const int KnownVisiblePlayerObjectId = 8602;
	private const int KnownInvisiblePlayerObjectId = 8603;
	private const int LocId = 6501;
	private const int CooldownSeconds = 600;
}
