using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ClientPackets;

public sealed class CmAccountReconnectKey : GsClientPacket
{
	public CmAccountReconnectKey(byte opCode)
		: base(opCode)
	{
	}

	public int AccountId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		AccountId = buffer.ReadD();
	}
}
