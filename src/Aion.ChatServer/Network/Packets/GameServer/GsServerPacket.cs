using Aion.Commons.Network;
using Aion.ChatServer.Network;

namespace Aion.ChatServer.Network.Packets.GameServer;

public abstract class GsServerPacket
{
	public byte[] SerializePayload()
	{
		using var buffer = new PacketBuffer();
		WritePayload(buffer);
		return buffer.ToArray();
	}

	public byte[] SerializeFrame()
	{
		return ChatPacketFrameCodec.CreateFrame(SerializePayload());
	}

	protected abstract void WritePayload(PacketBuffer buffer);
}
