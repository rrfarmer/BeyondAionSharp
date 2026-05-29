using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum RideRobotPacketPlanStatus
{
	PacketCreated,
	BlockedInvalidPlayer,
}

public sealed record RideRobotPacketPlan(
	RideRobotPacketPlanStatus Status,
	RideRobotSnapshot Snapshot,
	SmRideRobot? Packet,
	bool ShouldBroadcastPacket,
	bool ShouldBroadcastAndReceive,
	bool ShouldSendToOwner,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class RideRobotPacketPlanService
{
	public static RideRobotPacketPlan CreateBroadcastReceivePlan(RideRobotSnapshot snapshot)
	{
		// Java parity breadcrumb: RideRobotEffect.startEffect/endEffect and Preview command
		// create SM_RIDE_ROBOT and send it through PacketSendUtility. This planner models the
		// effect path's broadcastPacketAndReceive without live dispatch.
		if (snapshot.PlayerObjectId <= 0)
		{
			return new RideRobotPacketPlan(
				RideRobotPacketPlanStatus.BlockedInvalidPlayer,
				snapshot,
				Packet: null,
				ShouldBroadcastPacket: false,
				ShouldBroadcastAndReceive: false,
				ShouldSendToOwner: false,
				"SM_RIDE_ROBOT requires a live Player with a positive object id");
		}

		return new RideRobotPacketPlan(
			RideRobotPacketPlanStatus.PacketCreated,
			snapshot,
			new SmRideRobot(snapshot),
			ShouldBroadcastPacket: true,
			ShouldBroadcastAndReceive: true,
			ShouldSendToOwner: false,
			"PacketSendUtility.broadcastPacketAndReceive(player, new SM_RIDE_ROBOT(player))");
	}
}
