using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class MovementCorrectionPacketPlanServiceTests
{
	[Fact]
	public void CreateBroadcastObjectPlan_CreatesSmPositionAndBroadcastReceiveIntentLikeJavaEffects()
	{
		var plan = MovementCorrectionPacketPlanService.CreateBroadcastObjectPlan(new ObjectPositionSnapshot(
			ObjectId: 5001,
			X: 123.5f,
			Y: -45.25f,
			Z: 98.75f,
			Heading: 105));

		Assert.Equal(MovementCorrectionPacketPlanStatus.ObjectPositionPacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldBroadcastPacket);
		Assert.True(plan.ShouldBroadcastAndReceive);
		Assert.False(plan.ShouldSendToOwner);
		Assert.False(plan.ExpectsClientPositionSelfResponse);
		Assert.NotNull(plan.ObjectPacket);
		Assert.Null(plan.SelfPacket);
		Assert.Contains("broadcastPacketAndReceive", plan.JavaSource);
		AssertSmPositionPayload(plan.ObjectPacket!, objectId: 5001, x: 123.5f, y: -45.25f, z: 98.75f, heading: 105);
	}

	[Fact]
	public void CreateBroadcastObjectPlan_CanModelSimpleRootBroadcastWithoutReceive()
	{
		var plan = MovementCorrectionPacketPlanService.CreateBroadcastObjectPlan(
			new ObjectPositionSnapshot(ObjectId: 5001, X: 1, Y: 2, Z: 3, Heading: 11),
			receiveAfterBroadcast: false);

		Assert.Equal(MovementCorrectionPacketPlanStatus.ObjectPositionPacketCreated, plan.Status);
		Assert.True(plan.ShouldBroadcastPacket);
		Assert.False(plan.ShouldBroadcastAndReceive);
		Assert.False(plan.ShouldSendToOwner);
		Assert.Contains("broadcastPacket(object", plan.JavaSource);
		AssertSmPositionPayload(plan.ObjectPacket!, objectId: 5001, x: 1, y: 2, z: 3, heading: 11);
	}

	[Fact]
	public void CreateBroadcastObjectPlan_BlocksInvalidObjectBeforePacketCreation()
	{
		var plan = MovementCorrectionPacketPlanService.CreateBroadcastObjectPlan(new ObjectPositionSnapshot(
			ObjectId: 0,
			X: 123.5f,
			Y: -45.25f,
			Z: 98.75f,
			Heading: 105));

		Assert.Equal(MovementCorrectionPacketPlanStatus.BlockedInvalidObject, plan.Status);
		Assert.False(plan.ShouldBroadcastPacket);
		Assert.False(plan.ShouldBroadcastAndReceive);
		Assert.False(plan.ShouldSendToOwner);
		Assert.Null(plan.Packet);
		Assert.Null(plan.ObjectPacket);
		Assert.Null(plan.SelfPacket);
	}

	[Fact]
	public void CreateSelfPlan_CreatesSmPositionSelfAndOwnerResponseIntentLikeJavaPacketDoc()
	{
		var plan = MovementCorrectionPacketPlanService.CreateSelfPlan(new PositionSelfSnapshot(
			X: 123.5f,
			Y: -45.25f,
			Z: 98.75f,
			Heading: 105));

		Assert.Equal(MovementCorrectionPacketPlanStatus.SelfPositionPacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldBroadcastPacket);
		Assert.False(plan.ShouldBroadcastAndReceive);
		Assert.True(plan.ShouldSendToOwner);
		Assert.True(plan.ExpectsClientPositionSelfResponse);
		Assert.Null(plan.ObjectPacket);
		Assert.NotNull(plan.SelfPacket);
		Assert.Contains("CM_POSITION_SELF", plan.JavaSource);
		AssertSmPositionSelfPayload(plan.SelfPacket!, x: 123.5f, y: -45.25f, z: 98.75f, heading: 105);
	}

	private static void AssertSmPositionPayload(SmPosition packet, int objectId, float x, float y, float z, int heading)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmPosition.PacketOpCode, packet.OpCode);
		Assert.Equal(objectId, reader.ReadD());
		Assert.Equal(x, reader.ReadF());
		Assert.Equal(y, reader.ReadF());
		Assert.Equal(z, reader.ReadF());
		Assert.Equal(heading, reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertSmPositionSelfPayload(SmPositionSelf packet, float x, float y, float z, int heading)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmPositionSelf.PacketOpCode, packet.OpCode);
		Assert.Equal(x, reader.ReadF());
		Assert.Equal(y, reader.ReadF());
		Assert.Equal(z, reader.ReadF());
		Assert.Equal(heading, reader.ReadC());
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
