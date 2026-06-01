using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmInventoryUpdateItem : GameServerPacket
{
	public const int PacketOpCode = 29;
	public const int StatsChange = 0x00;
	public const int IncreaseItemCollect = 0x19;
	public const int IncreaseKinahCollect = 0x1A;
	public const int IncreaseKinahQuest = 0x32;
	public const int IncreaseItemRepurchase = 0x51;
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
