using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmInventoryAddItem : GameServerPacket
{
	public const int PacketOpCode = 27;
	public const int ItemCollect = 0x19;
	public const int BrokerBuy = 0x2E;
	public const int BrokerReturn = 0x2F;

	private readonly int _addType;
	private readonly IReadOnlyList<InventoryPacketItem> _items;

	public SmInventoryAddItem(IReadOnlyList<InventoryPacketItem> items, int addType)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_INVENTORY_ADD_ITEM(List<Item>, Player, ItemAddType).
		_items = items;
		_addType = addType;
	}

	public static SmInventoryAddItem CreateBrokerReturn(InventoryItem item, ItemTemplateSummary template)
	{
		// Java parity: services/BrokerService.cancelRegisteredItem uses ItemPacketService.ItemAddType.BROKER_RETURN.
		return new SmInventoryAddItem([new InventoryPacketItem(item, template)], BrokerReturn);
	}

	public static SmInventoryAddItem CreateBrokerBuy(InventoryItem item, ItemTemplateSummary template)
	{
		// Java parity: services/BrokerService.buyBrokerItem uses ItemPacketService.ItemAddType.BROKER_BUY.
		return new SmInventoryAddItem([new InventoryPacketItem(item, template)], BrokerBuy);
	}

	public static SmInventoryAddItem CreateItemCollect(InventoryItem item, ItemTemplateSummary template)
	{
		// Java parity: ItemPacketService.ItemAddType.ITEM_COLLECT default add type.
		return new SmInventoryAddItem([new InventoryPacketItem(item, template)], ItemCollect);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_INVENTORY_ADD_ITEM.writeImpl.
		buffer.WriteH(_addType);
		buffer.WriteH(_items.Count);
		foreach (var item in _items)
			WriteItemInfo(buffer, item);
	}

	private static void WriteItemInfo(PacketBuffer buffer, InventoryPacketItem packetItem)
	{
		// Java parity: SM_INVENTORY_ADD_ITEM.writeItemInfo.
		var item = packetItem.Item;
		var template = packetItem.Template;
		buffer.WriteD(item.ObjectId);
		buffer.WriteD(template.TemplateId);
		buffer.WriteS(template.GetClientName());
		SmInventoryInfo.WriteItemInfoBlob(buffer, item, template);
		buffer.WriteH((int)(item.Slot & 0xffff));
		buffer.WriteC(template.IsCloth ? 1 : 0);
	}

	public sealed record InventoryPacketItem(InventoryItem Item, ItemTemplateSummary Template);
}
