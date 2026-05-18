using Aion.Commons.Network;
using Aion.LoginServer.Network;

namespace Aion.LoginServer.Network.Aion;

public abstract class AionServerPacket
{
	protected AionServerPacket(byte opCode)
	{
		OpCode = opCode;
	}

	public byte OpCode { get; }

	public byte[] SerializePayload()
	{
		using var buffer = new PacketBuffer();
		buffer.WriteC(OpCode);
		WritePayload(buffer);
		return buffer.ToArray();
	}

	public byte[] SerializeUnencryptedFrame()
	{
		return PacketFrameCodec.CreateFrame(SerializePayload());
	}

	protected abstract void WritePayload(PacketBuffer buffer);
}
