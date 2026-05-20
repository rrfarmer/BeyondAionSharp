using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Client;

public sealed class CmChannelLeave : AbstractClientPacket
{
	public CmChannelLeave(byte opCode)
		: base(opCode)
	{
	}

	public int ChannelId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		buffer.ReadC();
		buffer.ReadH();
		buffer.ReadB(16);
		ChannelId = buffer.ReadD();
	}
}
