using Aion.Commons.Network;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSecondaryShowDecomposable : GameServerPacket
{
	public const int PacketOpCode = 286;

	private readonly int _objectId;
	private readonly IReadOnlyList<ResultedItemSummary> _items;

	public SmSecondaryShowDecomposable(int objectId, IReadOnlyList<ResultedItemSummary> items)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_SECONDARY_SHOW_DECOMPOSABLE.
		_objectId = objectId;
		_items = items;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_objectId);
		buffer.WriteD(0);
		buffer.WriteC(_items.Count);
		for (var index = 0; index < _items.Count; index++)
			SmFirstShowDecomposable.WriteItem(buffer, index, _items[index]);
	}
}
