using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPositionSelf : GameServerPacket
{
	public const int PacketOpCode = 21;

	private readonly PositionSelfSnapshot _position;

	public SmPositionSelf(PositionSelfSnapshot position)
		: base(PacketOpCode)
	{
		_position = position;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_POSITION_SELF.writeImpl writes
		// x/y/z floats and heading. The client responds with CM_POSITION_SELF.
		buffer.WriteF(_position.X);
		buffer.WriteF(_position.Y);
		buffer.WriteF(_position.Z);
		buffer.WriteC(_position.Heading);
	}
}

public sealed record PositionSelfSnapshot(
	float X,
	float Y,
	float Z,
	int Heading);
