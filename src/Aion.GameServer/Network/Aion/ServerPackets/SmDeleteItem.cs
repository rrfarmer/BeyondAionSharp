using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDeleteItem : GameServerPacket
{
	public const int PacketOpCode = 28;
	public const int UseDeleteType = 0x17;
	// Java parity: services/item/ItemPacketService.ItemDeleteType.SPLIT = 0x04
	public const int SplitDeleteType = 0x04;
	// Java parity: ItemPacketService.ItemDeleteType.QUEST_COMPLETE = 0x31.
	public const int QuestCompleteDeleteType = 0x31;
	// Java parity: ItemPacketService.ItemDeleteType.QUEST_START = 0x34.
	public const int QuestStartDeleteType = 0x34;
	// Java parity: services/item/ItemPacketService.ItemDeleteType.DISCARD = 0x15
	public const int DiscardDeleteType = 0x15;
	// Java parity: services/item/ItemPacketService.ItemDeleteType.MOVE = 0x14
	public const int MoveDeleteType = 0x14;
	// Java parity: services/item/ItemPacketService.ItemDeleteType.PUT_TO_EXCHANGE = 0x26 (full-stack item moved to exchange window).
	public const int PutToExchangeDeleteType = 0x26;

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
