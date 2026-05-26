using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKnownListFanoutTraceServiceTests
{
	[Fact]
	public void CreateTrace_ProjectsSourceFirstThenKnownListPlayersWithoutDistanceFiltering()
	{
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			SourcePlayerObjectId,
			SmBindPointTeleport.Cooldown(SourcePlayerObjectId, LocId, CooldownSeconds));

		var trace = BindPointTeleportKnownListFanoutTraceService.CreateTrace(
			fanoutPlan,
			[KnownVisiblePlayerObjectId, KnownInvisiblePlayerObjectId]);

		Assert.Equal(BindPointTeleportKnownListFanoutTraceStatus.Projected, trace.Status);
		Assert.Same(fanoutPlan, trace.FanoutPlan);
		Assert.True(trace.SendsSourceFirst);
		Assert.True(trace.UsesKnownListTraversal);
		Assert.False(trace.IsLive);
		Assert.Equal(
			[
				SourcePlayerObjectId,
				KnownVisiblePlayerObjectId,
				KnownInvisiblePlayerObjectId,
			],
			trace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.Equal(BindPointTeleportKnownListFanoutRecipientKind.SourceSelf, trace.Recipients[0].Kind);
		Assert.All(trace.Recipients.Skip(1), recipient =>
			Assert.Equal(BindPointTeleportKnownListFanoutRecipientKind.KnownListPlayer, recipient.Kind));
		Assert.Equal(
			BindPointTeleportKnownListFanoutKnownListOrdering.ConcurrentHashMapUnspecified,
			trace.KnownListOrdering);

		var packet = Assert.IsType<SmBindPointTeleport>(fanoutPlan.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(SourcePlayerObjectId, reader.ReadD());
		Assert.Equal(LocId, reader.ReadD());
		Assert.Equal(CooldownSeconds, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void CreateTrace_ModelsNormalKnownListOwnerExclusionAndDeduplicatesKnownListInput()
	{
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			SourcePlayerObjectId,
			SmBindPointTeleport.Cooldown(SourcePlayerObjectId, LocId, CooldownSeconds));

		var trace = BindPointTeleportKnownListFanoutTraceService.CreateTrace(
			fanoutPlan,
			[KnownVisiblePlayerObjectId, KnownVisiblePlayerObjectId]);

		Assert.True(trace.KnownListExcludesOwnerByNormalAddPath);
		Assert.True(trace.DuplicateKnownObjectIdsCollapsed);
		Assert.Equal(
			[SourcePlayerObjectId, KnownVisiblePlayerObjectId],
			trace.Recipients.Select(recipient => recipient.PlayerObjectId));
	}

	[Fact]
	public void CreateTrace_WithoutPacketPlanReturnsNoPacketTrace()
	{
		var trace = BindPointTeleportKnownListFanoutTraceService.CreateTrace(
			fanoutPlan: null,
			knownListPlayerObjectIds: [KnownVisiblePlayerObjectId]);

		Assert.Equal(BindPointTeleportKnownListFanoutTraceStatus.NoPacket, trace.Status);
		Assert.False(trace.SendsSourceFirst);
		Assert.False(trace.UsesKnownListTraversal);
		Assert.Empty(trace.Recipients);
		Assert.False(trace.IsLive);
	}

	[Fact]
	public void CreateTrace_LoginCooldownPreservesBroadcastPacketAndReceiveSource()
	{
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.LoginCooldownBroadcast,
			SourcePlayerObjectId,
			SmBindPointTeleport.Cooldown(SourcePlayerObjectId, LocId, CooldownSeconds));

		var trace = BindPointTeleportKnownListFanoutTraceService.CreateTrace(
			fanoutPlan,
			[KnownVisiblePlayerObjectId]);

		Assert.Equal("PacketSendUtility.broadcastPacketAndReceive(player, packet)", trace.JavaUtilityMethod);
		Assert.True(trace.SendsSourceFirst);
		Assert.Equal(
			[SourcePlayerObjectId, KnownVisiblePlayerObjectId],
			trace.Recipients.Select(recipient => recipient.PlayerObjectId));
	}

	private const int SourcePlayerObjectId = 8401;
	private const int KnownVisiblePlayerObjectId = 8402;
	private const int KnownInvisiblePlayerObjectId = 8403;
	private const int LocId = 6501;
	private const int CooldownSeconds = 600;

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
