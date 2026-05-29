using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmRideRobot : GameServerPacket
{
	public const int PacketOpCode = 92;
	private readonly RideRobotSnapshot _snapshot;

	public SmRideRobot(RideRobotSnapshot snapshot) : base(PacketOpCode)
	{
		_snapshot = snapshot;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_RIDE_ROBOT.writeImpl writes
		// player object id followed by robot id.
		buffer.WriteD(_snapshot.PlayerObjectId);
		buffer.WriteD(_snapshot.RobotId);
	}
}

public sealed record RideRobotSnapshot(int PlayerObjectId, int RobotId);
