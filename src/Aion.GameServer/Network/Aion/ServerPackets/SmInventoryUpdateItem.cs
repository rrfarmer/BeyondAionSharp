using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmInventoryUpdateItem : GameServerPacket
{
	public const int PacketOpCode = 29;
	public const int StatsChange = 0x00;
	// Java parity: ItemPacketService.ItemUpdateType.PLAYER_EXCHANGE_GET = 0x21 (player receives items from completed trade).
	public const int PlayerExchangeGet = 0x21;
	// Java parity: ItemPacketService.ItemUpdateType.INC_PLAYER_EXCHANGE_GET_BACK = 0x23 (player gets items back on exchange cancel).
	public const int PlayerExchangeGetBack = 0x23;
	// Java parity: ItemPacketService.ItemUpdateType.PUT_TO_EXCHANGE = 0x25 (partial-stack item put into exchange window, update).
	public const int PutToExchange = 0x25;
	public const int IncreaseItemCollect = 0x19;
	public const int IncreaseKinahCollect = 0x1A;
	public const int IncreaseKinahQuest = 0x32;
	public const int IncreaseItemRepurchase = 0x51;
	// Java parity: ItemPacketService.ItemUpdateType.INC_ITEM_BUY = 0x1C (buying from NPC).
	public const int IncreaseItemBuy = 0x1C;
	// Java parity: ItemPacketService.ItemUpdateType.DEC_ITEM_SPLIT = 0x06.
	public const int DecreaseItemSplit = 0x06;
	// Java parity: ItemPacketService.ItemUpdateType.DEC_ITEM_SPLIT_MOVE = 0x0A (cross-storage source).
	public const int DecreaseItemSplitMove = 0x0A;
	// Java parity: ItemPacketService.ItemUpdateType.INC_ITEM_MERGE = 0x01.
	public const int IncreaseItemMerge = 0x01;
	// Java parity: ItemPacketService.ItemUpdateType.INC_KINAH_MERGE = 0x05.
	public const int IncreaseKinahMerge = 0x05;
	public const int DecreaseItemUse = 0x16;
	public const int DecreaseStigmaUse = 0x17;
	public const int DecreaseKinahBuy = 0x1D;
	public const int DecreaseKinahLearn = 0x49;
	public const int DecreaseKinahFly = 0x4B;
	public const int DecreaseKinahCube = 0x5A;
	public const int EquipUnequip = -1;
	public const int Charge = -2;
	public const int PolishCharge = -3;

	private readonly InventoryItem _item;
	private readonly ItemTemplateSummary _template;
	private readonly int _updateType;
	private readonly int _generalInfoWarehouseRestrictionFlag;

	public SmInventoryUpdateItem(
		InventoryItem item,
		ItemTemplateSummary template,
		int updateType,
		int generalInfoWarehouseRestrictionFlag = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_INVENTORY_UPDATE_ITEM(Player, Item, ItemUpdateType).
		_item = item;
		_template = template;
		_updateType = updateType;
		_generalInfoWarehouseRestrictionFlag = generalInfoWarehouseRestrictionFlag;
	}

	public int UpdateType => _updateType;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_item.ObjectId);
		buffer.WriteS(_template.GetClientName());
		if (_updateType == Charge)
		{
			// Java parity: ItemUpdateType.CHARGE writes only ConditioningInfoBlob and omits the trailing update type.
			using var chargeBlob = new PacketBuffer();
			SmInventoryInfo.WriteConditioningInfoBlob(chargeBlob, _item);
			var chargeBlobBytes = chargeBlob.ToArray();
			buffer.WriteH(chargeBlobBytes.Length);
			buffer.WriteB(chargeBlobBytes);
			return;
		}

		if (_updateType == PolishCharge)
		{
			// Java parity: ItemUpdateType.POLISH_CHARGE writes only PolishInfoBlob and omits the trailing update type.
			using var polishBlob = new PacketBuffer();
			SmInventoryInfo.WritePolishInfoBlob(polishBlob, _item);
			var polishBlobBytes = polishBlob.ToArray();
			buffer.WriteH(polishBlobBytes.Length);
			buffer.WriteB(polishBlobBytes);
			return;
		}

		// Java parity: SM_INVENTORY_UPDATE_ITEM.writeImpl default full blob path.
		SmInventoryInfo.WriteItemInfoBlob(buffer, _item, _template, _generalInfoWarehouseRestrictionFlag);
		buffer.WriteH(_updateType);
	}
}
