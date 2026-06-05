using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmInventoryAddItem : GameServerPacket
{
	public const int PacketOpCode = 27;
	public const int AllSlot = 0x13;
	public const int ItemCollect = 0x19;
	// Java parity: ItemPacketService.ItemAddType.BUY = 0x1C (buying from NPC).
	public const int Buy = 0x1C;
	// Java parity: ItemPacketService.ItemAddType.PLAYER_EXCHANGE_GET = 0x21 (item received from a completed trade).
	public const int PlayerExchangeGet = 0x21;
	public const int CraftedItem = 0x2D;
	public const int BrokerBuy = 0x2E;
	public const int BrokerReturn = 0x2F;
	public const int Decomposable = 0x50;
	public const int Repurchase = 0x51;

	private readonly int _addType;
	private readonly IReadOnlyList<InventoryPacketItem> _items;

	public SmInventoryAddItem(IReadOnlyList<InventoryPacketItem> items, int addType)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_INVENTORY_ADD_ITEM(List<Item>, Player, ItemAddType).
		_items = items;
		_addType = addType;
	}

	public static SmInventoryAddItem CreateBrokerReturn(
		InventoryItem item,
		ItemTemplateSummary template,
		int generalInfoWarehouseRestrictionFlag = 0)
	{
		// Java parity: services/BrokerService.cancelRegisteredItem uses ItemPacketService.ItemAddType.BROKER_RETURN.
		return new SmInventoryAddItem([new InventoryPacketItem(item, template, generalInfoWarehouseRestrictionFlag)], BrokerReturn);
	}

	public static SmInventoryAddItem CreateBrokerBuy(
		InventoryItem item,
		ItemTemplateSummary template,
		int generalInfoWarehouseRestrictionFlag = 0)
	{
		// Java parity: services/BrokerService.buyBrokerItem uses ItemPacketService.ItemAddType.BROKER_BUY.
		return new SmInventoryAddItem([new InventoryPacketItem(item, template, generalInfoWarehouseRestrictionFlag)], BrokerBuy);
	}

	public static SmInventoryAddItem CreateItemCollect(
		InventoryItem item,
		ItemTemplateSummary template,
		int generalInfoWarehouseRestrictionFlag = 0)
	{
		// Java parity: ItemPacketService.ItemAddType.ITEM_COLLECT default add type.
		return new SmInventoryAddItem([new InventoryPacketItem(item, template, generalInfoWarehouseRestrictionFlag)], ItemCollect);
	}

	public static SmInventoryAddItem CreateBuy(
		InventoryItem item,
		ItemTemplateSummary template,
		int generalInfoWarehouseRestrictionFlag = 0)
	{
		// Java parity: ItemService.addItem(..., ItemAddType.BUY, ItemUpdateType.INC_ITEM_BUY).
		return new SmInventoryAddItem([new InventoryPacketItem(item, template, generalInfoWarehouseRestrictionFlag)], Buy);
	}

	public static SmInventoryAddItem CreatePlayerExchangeGet(
		InventoryItem item,
		ItemTemplateSummary template,
		int generalInfoWarehouseRestrictionFlag = 0)
	{
		// Java parity: ExchangeService.putItemToInventory uses ItemPacketService.ItemAddType.PLAYER_EXCHANGE_GET.
		return new SmInventoryAddItem([new InventoryPacketItem(item, template, generalInfoWarehouseRestrictionFlag)], PlayerExchangeGet);
	}

	public static SmInventoryAddItem CreateAllSlot(
		InventoryItem item,
		ItemTemplateSummary template,
		int generalInfoWarehouseRestrictionFlag = 0)
	{
		// Java parity: ItemPacketService.sendItemUnlockPacket -> ItemAddType.ALL_SLOT for normal cube storage.
		return new SmInventoryAddItem([new InventoryPacketItem(item, template, generalInfoWarehouseRestrictionFlag)], AllSlot);
	}

	public static SmInventoryAddItem CreateCraftedItem(
		InventoryItem item,
		ItemTemplateSummary template,
		int generalInfoWarehouseRestrictionFlag = 0)
	{
		// Java parity: services/craft/CraftService.finishCrafting -> ItemService.addItem(..., ItemAddType.CRAFTED_ITEM, ItemUpdateType.INC_ITEM_COLLECT).
		return new SmInventoryAddItem([new InventoryPacketItem(item, template, generalInfoWarehouseRestrictionFlag)], CraftedItem);
	}

	public static SmInventoryAddItem CreateDecomposable(IReadOnlyList<InventoryPacketItem> items)
	{
		// Java parity: ItemService.addItem with ItemPacketService.ItemAddType.DECOMPOSABLE.
		return new SmInventoryAddItem(items, Decomposable);
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
		SmInventoryInfo.WriteItemInfoBlob(buffer, item, template, packetItem.GeneralInfoWarehouseRestrictionFlag);
		buffer.WriteH((int)(item.Slot & 0xffff));
		buffer.WriteC(template.IsCloth ? 1 : 0);
	}

	public sealed record InventoryPacketItem(InventoryItem Item, ItemTemplateSummary Template, int GeneralInfoWarehouseRestrictionFlag = 0);
}
