using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmViewPlayerDetails : GameServerPacket
{
	public const int PacketOpCode = 65;

	private readonly int _targetObjectId;
	private readonly IReadOnlyList<(InventoryItem Item, ItemTemplateSummary Template)> _equippedItems;

	public SmViewPlayerDetails(int targetObjectId, IReadOnlyList<(InventoryItem, ItemTemplateSummary)> equippedItems)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_VIEW_PLAYER_DETAILS(List<Item>, Player).
		_targetObjectId = targetObjectId;
		_equippedItems = equippedItems;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_VIEW_PLAYER_DETAILS.writeImpl.
		buffer.WriteD(_targetObjectId);
		buffer.WriteC(11); // EQUIPMENT storage type constant
		buffer.WriteH(_equippedItems.Count);
		foreach (var (item, template) in _equippedItems)
			WriteEquippedItemInfo(buffer, item, template);
	}

	private static void WriteEquippedItemInfo(PacketBuffer buffer, InventoryItem item, ItemTemplateSummary template)
	{
		// Java parity: network/aion/serverpackets/SM_VIEW_PLAYER_DETAILS.writeItemInfo.
		buffer.WriteD(0);
		buffer.WriteD(template.TemplateId);
		buffer.WriteS(template.GetClientName());
		SmInventoryInfo.WriteItemInfoBlob(buffer, item, template);
	}
}
