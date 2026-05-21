using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPong : GameServerPacket
{
	public const int PacketOpCode = 142;

	public SmPong()
		: base(PacketOpCode)
	{
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_PONG.writeImpl.
		buffer.WriteC(0);
		buffer.WriteC(0);
	}
}
