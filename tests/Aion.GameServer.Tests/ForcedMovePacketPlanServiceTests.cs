using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ForcedMovePacketPlanServiceTests
{
	[Fact]
	public void CreateBroadcastReceivePlan_CreatesSmForcedMoveAndBroadcastReceiveIntent()
	{
		var plan = ForcedMovePacketPlanService.CreateBroadcastReceivePlan(
			new ForcedMoveSnapshot(SourceObjectId: 7001, TargetObjectId: 8002, X: 123.5f, Y: -45.25f, Z: 98.75f)
		);

		Assert.Equal(ForcedMovePacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldBroadcastPacket);
		Assert.True(plan.ShouldBroadcastAndReceive);
		Assert.False(plan.ShouldSendToOwner);
		Assert.NotNull(plan.Packet);
		Assert.Contains("broadcastPacketAndReceive", plan.JavaSource);
		AssertSmForcedMovePayload(plan.Packet!, sourceObjectId: 7001, targetObjectId: 8002, x: 123.5f, y: -45.25f, z: 98.75f);
	}

	[Fact]
	public void CreateBroadcastReceivePlan_BlocksInvalidSourceBeforePacketCreation()
	{
		var plan = ForcedMovePacketPlanService.CreateBroadcastReceivePlan(
			new ForcedMoveSnapshot(SourceObjectId: 0, TargetObjectId: 8002, X: 123.5f, Y: -45.25f, Z: 98.75f)
		);

		Assert.Equal(ForcedMovePacketPlanStatus.BlockedInvalidSource, plan.Status);
		Assert.False(plan.ShouldBroadcastPacket);
		Assert.False(plan.ShouldBroadcastAndReceive);
		Assert.False(plan.ShouldSendToOwner);
		Assert.Null(plan.Packet);
	}

	[Fact]
	public void CreateBroadcastReceivePlan_BlocksInvalidTargetBeforePacketCreation()
	{
		var plan = ForcedMovePacketPlanService.CreateBroadcastReceivePlan(
			new ForcedMoveSnapshot(SourceObjectId: 7001, TargetObjectId: 0, X: 123.5f, Y: -45.25f, Z: 98.75f)
		);

		Assert.Equal(ForcedMovePacketPlanStatus.BlockedInvalidTarget, plan.Status);
		Assert.False(plan.ShouldBroadcastPacket);
		Assert.False(plan.ShouldBroadcastAndReceive);
		Assert.False(plan.ShouldSendToOwner);
		Assert.Null(plan.Packet);
	}

	private static void AssertSmForcedMovePayload(SmForcedMove packet, int sourceObjectId, int targetObjectId, float x, float y, float z)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmForcedMove.PacketOpCode, packet.OpCode);
		Assert.Equal(sourceObjectId, reader.ReadD());
		Assert.Equal(targetObjectId, reader.ReadD());
		Assert.Equal(16, reader.ReadC());
		Assert.Equal(x, reader.ReadF());
		Assert.Equal(y, reader.ReadF());
		Assert.Equal(z, reader.ReadF());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
