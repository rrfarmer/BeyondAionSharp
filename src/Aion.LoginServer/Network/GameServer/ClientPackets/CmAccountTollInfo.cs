using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmAccountTollInfo : GsClientPacket
{
	public CmAccountTollInfo(byte opCode)
		: base(opCode)
	{
	}

	public int AccountId { get; private set; }

	public long Toll { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		AccountId = buffer.ReadD();
		Toll = buffer.ReadQ();
	}
}
