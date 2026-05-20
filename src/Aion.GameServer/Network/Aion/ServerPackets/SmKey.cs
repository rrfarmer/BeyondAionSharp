using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmKey : GameServerPacket
{
	public const int PacketOpCode = 72;

	public SmKey()
		: base(PacketOpCode)
	{
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_KEY.writeImpl.
		buffer.WriteD(crypt.EnableKey());
	}
}
