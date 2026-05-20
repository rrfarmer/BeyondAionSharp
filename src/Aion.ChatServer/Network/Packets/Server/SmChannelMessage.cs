using Aion.Commons.Network;
using Aion.ChatServer.Models;

namespace Aion.ChatServer.Network.Packets.Server;

public sealed class SmChannelMessage : AbstractServerPacket
{
	public SmChannelMessage(Message message)
		: base(ServerPacketFactory.SmChannelMessage)
	{
		Message = message;
	}

	public Message Message { get; }

	protected override void WritePayload(PacketBuffer buffer)
	{
		var identifier = Message.Sender.Identifier ?? [];
		buffer.WriteC(0x00);
		buffer.WriteD(0x00);
		buffer.WriteD(0x00);
		buffer.WriteD(Message.Channel.ChannelId);
		buffer.WriteD(Message.Sender.ClientId);
		buffer.WriteD(0x00);
		buffer.WriteC(0x00);
		buffer.WriteH(identifier.Length / 2);
		buffer.WriteB(identifier);
		buffer.WriteH(Message.Size / 2);
		buffer.WriteB(Message.Text);
	}
}
