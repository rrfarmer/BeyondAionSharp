using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPosition : GameServerPacket
{
	public const int PacketOpCode = 204;

	private readonly ObjectPositionSnapshot _position;

	public SmPosition(ObjectPositionSnapshot position)
		: base(PacketOpCode)
	{
		_position = position;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_POSITION.writeImpl writes
		// object id, x/y/z floats, and heading.
		buffer.WriteD(_position.ObjectId);
		buffer.WriteF(_position.X);
		buffer.WriteF(_position.Y);
		buffer.WriteF(_position.Z);
		buffer.WriteC(_position.Heading);
	}
}

public sealed record ObjectPositionSnapshot(
	int ObjectId,
	float X,
	float Y,
	float Z,
	int Heading);
