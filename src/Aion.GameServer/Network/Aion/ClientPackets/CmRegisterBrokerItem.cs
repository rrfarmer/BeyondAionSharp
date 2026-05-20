using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmRegisterBrokerItem : GameClientPacket
{
	public CmRegisterBrokerItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int BrokerObjectId { get; private set; }

	public int ItemObjectId { get; private set; }

	public long Price { get; private set; }

	public long ItemCount { get; private set; }

	public bool SplittingAvailable { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_REGISTER_BROKER_ITEM.readImpl.
		BrokerObjectId = buffer.ReadD();
		ItemObjectId = buffer.ReadD();
		Price = buffer.ReadQ();
		ItemCount = buffer.ReadQ();
		SplittingAvailable = buffer.ReadC() == 1;
	}
}
