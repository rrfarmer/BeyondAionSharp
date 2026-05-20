using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAfterTimeCheck475 : GameServerPacket
{
	public const int PacketOpCode = 292;

	public SmAfterTimeCheck475()
		: base(PacketOpCode)
	{
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_AFTER_TIME_CHECK_4_7_5.writeImpl.
		buffer.WriteH(1);
		buffer.WriteD(0);
	}
}
