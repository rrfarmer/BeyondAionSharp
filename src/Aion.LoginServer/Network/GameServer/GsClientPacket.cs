using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer;

public abstract class GsClientPacket
{
	protected GsClientPacket(byte opCode)
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
