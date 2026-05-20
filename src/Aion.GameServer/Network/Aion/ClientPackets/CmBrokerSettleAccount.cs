using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBrokerSettleAccount : GameClientPacket
{
	public CmBrokerSettleAccount(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int BrokerObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BROKER_SETTLE_ACCOUNT.readImpl.
		BrokerObjectId = buffer.ReadD();
	}
}
