using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ClientPackets;

public sealed class CmUpdateSession : AionClientPacket
{
	public CmUpdateSession(byte opCode)
		: base(opCode)
	{
	}

	public int AccountId { get; private set; }

	public int LoginOk { get; private set; }

	public int ReconnectKey { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		AccountId = buffer.ReadD();
		LoginOk = buffer.ReadD();
		ReconnectKey = buffer.ReadD();
		buffer.ReadC();
		buffer.ReadB(6);
		buffer.ReadC();
		buffer.ReadC();
		buffer.ReadH();
	}
}
