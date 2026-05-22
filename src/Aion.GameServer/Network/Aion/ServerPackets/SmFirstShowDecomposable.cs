using Aion.Commons.Network;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmFirstShowDecomposable : GameServerPacket
{
	public const int PacketOpCode = 284;

	private readonly int _objectId;
	private readonly IReadOnlyList<ResultedItemSummary> _items;

	public SmFirstShowDecomposable(int objectId, IReadOnlyList<ResultedItemSummary> items)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_FIRST_SHOW_DECOMPOSABLE.
		_objectId = objectId;
		_items = items;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_objectId);
		buffer.WriteD(0);
		buffer.WriteC(_items.Count);
		for (var index = 0; index < _items.Count; index++)
			WriteItem(buffer, index, _items[index]);
	}

	internal static void WriteItem(PacketBuffer buffer, int index, ResultedItemSummary item)
	{
		buffer.WriteC(index);
		buffer.WriteD(item.ItemId);
		buffer.WriteD(item.MinCount);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(1);
	}
}
