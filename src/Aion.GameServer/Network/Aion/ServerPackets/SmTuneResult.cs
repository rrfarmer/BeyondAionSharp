using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmTuneResult : GameServerPacket
{
	public const int PacketOpCode = 288;

	private readonly PendingTuneResult _result;
	private readonly InventoryItem _targetItem;
	private readonly ItemTemplateSummary? _targetTemplate;
	private readonly int _tuningScrollItemId;

	public SmTuneResult(InventoryItem targetItem, ItemTemplateSummary? targetTemplate, int tuningScrollItemId, PendingTuneResult result)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_TUNE_RESULT(Item, int, PendingTuneResult).
		_targetItem = targetItem;
		_targetTemplate = targetTemplate;
		_tuningScrollItemId = tuningScrollItemId;
		_result = result;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_TUNE_RESULT.writeImpl.
		buffer.WriteD(_targetItem.ObjectId);
		buffer.WriteD(_tuningScrollItemId);
		buffer.WriteC(_result.StatBonusId);
		SmInventoryInfo.WriteEnchantInfo(buffer, _targetItem, _result.OptionalSockets, _result.EnchantBonus, _targetTemplate);
		buffer.WriteC(_result.IsAttributeOnly ? 1 : 0);
		buffer.WriteC(_result.IsAttributeOnly ? 1 : 0);
	}
}
