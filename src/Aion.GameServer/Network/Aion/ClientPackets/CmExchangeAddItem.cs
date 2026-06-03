using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmExchangeAddItem : GameClientPacket
{
	public CmExchangeAddItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_EXCHANGE_ADD_ITEM.readImpl: itemObjId + itemCount.
	public int ItemObjectId { get; private set; }
	public int ItemCount { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_EXCHANGE_ADD_ITEM.readImpl.
		ItemObjectId = buffer.ReadD();
		ItemCount = buffer.ReadD();
	}
}
