using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmInventoryUpdateItem : GameServerPacket
{
	public const int PacketOpCode = 29;
	public const int IncreaseKinahCollect = 0x1A;
	public const int DecreaseItemUse = 0x16;
	public const int DecreaseKinahBuy = 0x1D;

	private readonly InventoryItem _item;
	private readonly ItemTemplateSummary _template;
	private readonly int _updateType;

	public SmInventoryUpdateItem(InventoryItem item, ItemTemplateSummary template, int updateType)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_INVENTORY_UPDATE_ITEM(Player, Item, ItemUpdateType).
		_item = item;
		_template = template;
		_updateType = updateType;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_INVENTORY_UPDATE_ITEM.writeImpl default full blob path.
		buffer.WriteD(_item.ObjectId);
		buffer.WriteS(_template.GetClientName());
		SmInventoryInfo.WriteItemInfoBlob(buffer, _item, _template);
		buffer.WriteH(_updateType);
	}
}
