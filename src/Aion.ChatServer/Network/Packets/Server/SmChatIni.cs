using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Server;

public sealed class SmChatIni : AbstractServerPacket
{
	public SmChatIni()
		: base(ServerPacketFactory.SmChatIni)
	{
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(0x40);
		buffer.WriteD(0x02);
		buffer.WriteH(0x00);
	}
}
