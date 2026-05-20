using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBuyBrokerItem : GameClientPacket
{
	public CmBuyBrokerItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int BrokerObjectId { get; private set; }

	public int BrokerItemObjectId { get; private set; }

	public long ItemCount { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BUY_BROKER_ITEM.readImpl.
		BrokerObjectId = buffer.ReadD();
		BrokerItemObjectId = buffer.ReadD();
		ItemCount = buffer.ReadQ();
	}
}
