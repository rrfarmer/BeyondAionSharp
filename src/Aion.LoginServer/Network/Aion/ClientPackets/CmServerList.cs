using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ClientPackets;

public sealed class CmServerList : AionClientPacket
{
	public CmServerList(byte opCode)
		: base(opCode)
	{
	}

	public int AccountId { get; private set; }

	public int LoginOk { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		AccountId = buffer.ReadD();
		LoginOk = buffer.ReadD();
		buffer.ReadC();
		buffer.ReadB(6);
		buffer.ReadD();
		buffer.ReadD();
	}
}
