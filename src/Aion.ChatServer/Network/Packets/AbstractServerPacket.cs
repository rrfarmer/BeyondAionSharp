using Aion.Commons.Network;
using Aion.ChatServer.Network;

namespace Aion.ChatServer.Network.Packets;

public abstract class AbstractServerPacket
{
	protected AbstractServerPacket(byte opCode)
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

	public byte[] SerializeFrame()
	{
		return ChatPacketFrameCodec.CreateFrame(SerializePayload());
	}

	protected abstract void WritePayload(PacketBuffer buffer);
}
