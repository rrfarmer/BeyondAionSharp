using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ClientPackets;

public sealed class CmAuthGameGuard : AionClientPacket
{
	public CmAuthGameGuard(byte opCode)
		: base(opCode)
	{
	}

	public int SessionId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		SessionId = buffer.ReadD();
		buffer.ReadD();
		buffer.ReadD();
		buffer.ReadD();
		buffer.ReadD();
		buffer.ReadB(0x0B);
	}
}
