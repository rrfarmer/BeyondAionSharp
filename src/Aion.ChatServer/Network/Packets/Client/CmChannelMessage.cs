using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Client;

public sealed class CmChannelMessage : AbstractClientPacket
{
	public CmChannelMessage(byte opCode)
		: base(opCode)
	{
	}

	public int ChannelId { get; private set; }

	public byte[] Content { get; private set; } = [];

	protected override void ReadPayload(PacketBuffer buffer)
	{
		buffer.ReadH();
		buffer.ReadC();
		buffer.ReadD();
		buffer.ReadD();
		buffer.ReadD();
		buffer.ReadD();
		ChannelId = buffer.ReadD();
		buffer.ReadC();
		var contentLength = buffer.ReadH() * 2;
		Content = buffer.ReadB(contentLength);
	}
}
