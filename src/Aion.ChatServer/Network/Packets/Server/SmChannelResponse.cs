using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Server;

public sealed class SmChannelResponse : AbstractServerPacket
{
	public SmChannelResponse(int channelId, int channelRequestId)
		: base(ServerPacketFactory.SmChannelResponse)
	{
		ChannelId = channelId;
		ChannelRequestId = channelRequestId;
	}

	public int ChannelId { get; }

	public int ChannelRequestId { get; }

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(0x40);
		buffer.WriteD(ChannelRequestId);
		buffer.WriteH(0x00);
		buffer.WriteD(ChannelId);
	}
}
