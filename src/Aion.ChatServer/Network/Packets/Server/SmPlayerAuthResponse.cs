using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Server;

public sealed class SmPlayerAuthResponse : AbstractServerPacket
{
	public SmPlayerAuthResponse()
		: base(ServerPacketFactory.SmPlayerAuthResponse)
	{
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(0x40);
		buffer.WriteH(0x01);
		buffer.WriteD(0x00);
		buffer.WriteH(0x0822);
	}
}
