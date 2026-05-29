using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmRideRobotPacketTests
{
	[Fact]
	public void SmRideRobot_WritesPlayerObjectIdAndRobotIdLikeJava()
	{
		var packet = new SmRideRobot(new RideRobotSnapshot(PlayerObjectId: 8002, RobotId: 185000137));

		AssertSmRideRobotPayload(packet, playerObjectId: 8002, robotId: 185000137);
	}

	[Fact]
	public void SmRideRobot_AllowsZeroRobotIdForDismountPreviewLikeJavaConstructor()
	{
		var packet = new SmRideRobot(new RideRobotSnapshot(PlayerObjectId: 8002, RobotId: 0));

		AssertSmRideRobotPayload(packet, playerObjectId: 8002, robotId: 0);
	}

	[Fact]
	public void CreateBroadcastReceivePlan_CreatesPacketAndBroadcastReceiveIntent()
	{
		var plan = RideRobotPacketPlanService.CreateBroadcastReceivePlan(
			new RideRobotSnapshot(PlayerObjectId: 8002, RobotId: 185000137));

		Assert.Equal(RideRobotPacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldBroadcastPacket);
		Assert.True(plan.ShouldBroadcastAndReceive);
		Assert.False(plan.ShouldSendToOwner);
		Assert.NotNull(plan.Packet);
		Assert.Contains("broadcastPacketAndReceive", plan.JavaSource);
		AssertSmRideRobotPayload(plan.Packet!, playerObjectId: 8002, robotId: 185000137);
	}

	[Fact]
	public void CreateBroadcastReceivePlan_BlocksInvalidPlayerBeforePacketCreation()
	{
		var plan = RideRobotPacketPlanService.CreateBroadcastReceivePlan(
			new RideRobotSnapshot(PlayerObjectId: 0, RobotId: 185000137));

		Assert.Equal(RideRobotPacketPlanStatus.BlockedInvalidPlayer, plan.Status);
		Assert.False(plan.ShouldBroadcastPacket);
		Assert.False(plan.ShouldBroadcastAndReceive);
		Assert.False(plan.ShouldSendToOwner);
		Assert.Null(plan.Packet);
	}

	private static void AssertSmRideRobotPayload(SmRideRobot packet, int playerObjectId, int robotId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmRideRobot.PacketOpCode, packet.OpCode);
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(robotId, reader.ReadD());
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
