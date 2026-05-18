using Aion.Commons.Network;
using Aion.LoginServer.Network;

namespace Aion.LoginServer.Network.GameServer;

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
		return PacketFrameCodec.CreateFrame(SerializePayload());
	}

	protected abstract void WritePayload(PacketBuffer buffer);
}
