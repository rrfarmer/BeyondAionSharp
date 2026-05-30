using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmExchangeAddItem : GameServerPacket
{
	public const int PacketOpCode = 75;
	public const int ActionSelf = 0;
	public const int ActionOther = 1;

	private readonly int _action;
	private readonly InventoryItem _item;
	private readonly ItemTemplateSummary _template;

	public SmExchangeAddItem(int action, InventoryItem item, ItemTemplateSummary template)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_EXCHANGE_ADD_ITEM.writeImpl.
		_action = action;
		_item = item;
		_template = template;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_action);                            // action: 0=self, 1=other
		buffer.WriteD(_template.TemplateId);               // itemTemplate.getTemplateId()
		buffer.WriteD(_item.ObjectId);                     // item.getObjectId()
		buffer.WriteS(_template.GetClientName() ?? _template.Name); // itemTemplate.getL10n()
		SmInventoryInfo.WriteItemInfoBlob(buffer, _item, _template); // ItemInfoBlob.getFullBlob(player, item)
	}
}
