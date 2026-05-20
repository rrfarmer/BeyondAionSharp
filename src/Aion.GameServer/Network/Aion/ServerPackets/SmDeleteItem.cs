using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDeleteItem : GameServerPacket
{
	public const int PacketOpCode = 28;
	public const int UseDeleteType = 0x17;

	private readonly int _itemObjectId;
	private readonly int _deleteType;

	public SmDeleteItem(int itemObjectId, int deleteType = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_DELETE_ITEM(int, ItemDeleteType).
		_itemObjectId = itemObjectId;
		_deleteType = deleteType;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_DELETE_ITEM.writeImpl.
		buffer.WriteD(_itemObjectId);
		buffer.WriteC(_deleteType);
	}
}
