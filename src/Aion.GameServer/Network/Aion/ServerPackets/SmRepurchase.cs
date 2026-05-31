using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmRepurchase : GameServerPacket
{
	public const int PacketOpCode = 167;

	private readonly IReadOnlyList<RepurchasePacketItem> _items;
	private readonly int _targetObjectId;

	public SmRepurchase(int targetObjectId, IReadOnlyList<RepurchasePacketItem> items)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_REPURCHASE(Player, npcId).
		_targetObjectId = targetObjectId;
		_items = items;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_targetObjectId);
		buffer.WriteD(1);
		buffer.WriteH(_items.Count);

		foreach (var packetItem in _items)
		{
			var item = packetItem.Item;
			var template = packetItem.Template;
			buffer.WriteD(item.ObjectId);
			buffer.WriteD(template.TemplateId);
			buffer.WriteS(template.GetClientName());
			SmInventoryInfo.WriteItemInfoBlob(buffer, item, template);
			buffer.WriteQ(packetItem.RepurchasePrice);
		}
	}
}

public sealed record RepurchasePacketItem(
	InventoryItem Item,
	ItemTemplateSummary Template,
	long RepurchasePrice);
