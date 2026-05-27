using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmWarehouseAddItem : GameServerPacket
{
	public const int PacketOpCode = 169;
	public const int AllSlot = SmInventoryAddItem.AllSlot;
	public const int RegularWarehouse = 1;
	public const int AccountWarehouse = 2;
	public const int LegionWarehouse = 3;

	private readonly int _warehouseType;
	private readonly int _addType;
	private readonly IReadOnlyList<WarehousePacketItem> _items;

	public SmWarehouseAddItem(int warehouseType, IReadOnlyList<WarehousePacketItem> items, int addType)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_WAREHOUSE_ADD_ITEM(Item, int, Player, ItemAddType).
		_warehouseType = warehouseType;
		_items = items;
		_addType = addType;
	}

	public static SmWarehouseAddItem CreateAllSlot(int warehouseType, InventoryItem item, ItemTemplateSummary template)
	{
		// Java parity: ItemPacketService.sendItemUnlockPacket -> ItemAddType.ALL_SLOT for non-cube storage.
		return new SmWarehouseAddItem(warehouseType, [new WarehousePacketItem(item, template)], AllSlot);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_WAREHOUSE_ADD_ITEM.writeImpl.
		buffer.WriteC(_warehouseType);
		buffer.WriteH(_addType);
		buffer.WriteH(_items.Count);
		foreach (var item in _items)
			WriteItemInfo(buffer, item);
	}

	private static void WriteItemInfo(PacketBuffer buffer, WarehousePacketItem packetItem)
	{
		// Java parity: SM_WAREHOUSE_ADD_ITEM.writeItemInfo.
		var item = packetItem.Item;
		var template = packetItem.Template;
		buffer.WriteD(item.ObjectId);
		buffer.WriteD(template.TemplateId);
		buffer.WriteC(0);
		buffer.WriteS(template.GetClientName());
		SmInventoryInfo.WriteItemInfoBlob(buffer, item, template);
		buffer.WriteH((int)(item.Slot & 0xffff));
	}

	public sealed record WarehousePacketItem(InventoryItem Item, ItemTemplateSummary Template);
}
