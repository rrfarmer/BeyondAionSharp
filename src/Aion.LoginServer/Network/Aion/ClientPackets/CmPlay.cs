using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ClientPackets;

public sealed class CmPlay : AionClientPacket
{
	public CmPlay(byte opCode)
		: base(opCode)
	{
	}

	public int AccountId { get; private set; }

	public int LoginOk { get; private set; }

	public byte ServerId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		AccountId = buffer.ReadD();
		LoginOk = buffer.ReadD();
		ServerId = buffer.ReadC();
		buffer.ReadB(6);
		buffer.ReadQ();
	}
}
