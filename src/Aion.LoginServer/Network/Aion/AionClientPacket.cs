using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion;

public abstract class AionClientPacket
{
	protected AionClientPacket(byte opCode)
	{
		OpCode = opCode;
	}

	public byte OpCode { get; }

	public void Read(PacketBuffer buffer)
	{
		ReadPayload(buffer);
	}

	protected abstract void ReadPayload(PacketBuffer buffer);
}
