using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmWarehouseUpdateItem : GameServerPacket
{
	public const int PacketOpCode = 171;

	private readonly InventoryItem _item;
	private readonly ItemTemplateSummary _template;
	private readonly int _warehouseType;
	private readonly int _updateType;

	public SmWarehouseUpdateItem(InventoryItem item, ItemTemplateSummary template, int warehouseType, int updateType)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_WAREHOUSE_UPDATE_ITEM(Player, Item, int, ItemUpdateType).
		_item = item;
		_template = template;
		_warehouseType = warehouseType;
		_updateType = updateType;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_WAREHOUSE_UPDATE_ITEM.writeImpl — only GENERAL_INFO blob; sendable update type appended.
		buffer.WriteD(_item.ObjectId);
		buffer.WriteC(_warehouseType);
		buffer.WriteS(_template.GetClientName());

		// Java parity: itemInfoBlob.addBlobEntry(ItemBlobType.GENERAL_INFO) — only the general info entry, not the full blob.
		using var blob = new PacketBuffer();
		SmInventoryInfo.WriteGeneralInfoBlobForWarehouse(blob, _item, _template);
		var blobBytes = blob.ToArray();
		buffer.WriteH(blobBytes.Length);
		buffer.WriteB(blobBytes);

		// Java parity: if updateType.isSendable() writeH(updateType.getMask()).
		// All update types used with this packet (DEC_ITEM_SPLIT, INC_ITEM_MERGE) are sendable.
		buffer.WriteH(_updateType);
	}
}
