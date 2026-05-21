using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPingResponse : GameServerPacket
{
	public const int PacketOpCode = 128;

	public SmPingResponse()
		: base(PacketOpCode)
	{
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_PING_RESPONSE.writeImpl.
		buffer.WriteC(4);
	}
}
