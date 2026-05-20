using Aion.Commons.Network;
using Aion.GameServer.Network;

namespace Aion.GameServer.Network.LoginServer;

public abstract class LoginServerPacket
{
	public byte[] SerializePayload()
	{
		using var buffer = new PacketBuffer();
		WritePayload(buffer);
		return buffer.ToArray();
	}

	public byte[] SerializeFrame()
	{
		return BridgePacketFrameCodec.CreateFrame(SerializePayload());
	}

	protected abstract void WritePayload(PacketBuffer buffer);
}
