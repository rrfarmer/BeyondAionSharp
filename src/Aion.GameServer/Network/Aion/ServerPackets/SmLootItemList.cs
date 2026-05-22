using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLootItemList : GameServerPacket
{
	public const int PacketOpCode = 206;

	private readonly int _targetObjectId;
	private readonly int _playerObjectId;
	private readonly bool _teamMembersNearby;
	private readonly IReadOnlyList<WorldNpcDropItem> _dropItems;

	public SmLootItemList(int targetObjectId, IEnumerable<WorldNpcDropItem> dropItems, Player player, bool teamMembersNearby = false)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_LOOT_ITEMLIST.
		_targetObjectId = targetObjectId;
		_playerObjectId = player.ObjectId;
		_teamMembersNearby = teamMembersNearby;
		_dropItems = dropItems.Where(item => item.CanViewDropItem(player.ObjectId)).ToArray();
	}

	public int TargetObjectId => _targetObjectId;

	public IReadOnlyList<WorldNpcDropItem> DropItems => _dropItems;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_targetObjectId);
		buffer.WriteC(_dropItems.Count);

		foreach (var dropItem in _dropItems)
		{
			buffer.WriteC(dropItem.Index);
			buffer.WriteD(dropItem.ItemId);
			buffer.WriteD((int)dropItem.Count);
			buffer.WriteC(dropItem.OptionalSocket);
			buffer.WriteC(0);
			buffer.WriteC(0);
			buffer.WriteC(ShouldShowLootConfirmation(dropItem) ? 1 : 0);
		}
	}

	private bool ShouldShowLootConfirmation(WorldNpcDropItem dropItem)
	{
		// Java parity: SM_LOOT_ITEMLIST suppresses confirmation for the only possible looter or when no team member is nearby.
		return _teamMembersNearby && !dropItem.IsOnlyPossibleLooter(_playerObjectId);
	}
}
