using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBrokerSearch : GameClientPacket
{
	public CmBrokerSearch(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int BrokerObjectId { get; private set; }

	public byte SortType { get; private set; }

	public int Page { get; private set; }

	public int Mask { get; private set; }

	public IReadOnlyList<int> ItemIds { get; private set; } = Array.Empty<int>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BROKER_SEARCH.readImpl.
		BrokerObjectId = buffer.ReadD();
		SortType = buffer.ReadC();
		Page = buffer.ReadH();
		Mask = buffer.ReadH();
		var itemCount = buffer.ReadH();
		var itemIds = new int[itemCount];
		for (var i = 0; i < itemIds.Length; i++)
			itemIds[i] = buffer.ReadD();
		ItemIds = itemIds;
	}
}
