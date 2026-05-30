using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmGroupLoot : GameServerPacket
{
	public const int PacketOpCode = 135;

	private readonly int _groupId;
	private readonly int _index;
	private readonly int _itemCount;
	private readonly int _itemId;
	private readonly int _lootCorpseId;
	private readonly int _distributionId;
	private readonly int _playerId;
	private readonly int _luck;

	public SmGroupLoot(
		int groupId,
		int playerId,
		int itemId,
		int itemCount,
		int lootCorpseId,
		int distributionId,
		long luck,
		int index)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_LOOT.writeImpl.
		// luck is truncated to int via (int) cast like Java.
		_groupId = groupId;
		_index = index;
		_itemCount = itemCount;
		_itemId = itemId;
		_lootCorpseId = lootCorpseId;
		_distributionId = distributionId;
		_playerId = playerId;
		_luck = (int)luck;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_groupId);
		buffer.WriteD(_index);
		buffer.WriteD(_itemCount);
		buffer.WriteD(_itemId);
		buffer.WriteC(0);    // unk3
		buffer.WriteC(0);    // 3.0
		buffer.WriteC(0);    // 3.5
		buffer.WriteD(_lootCorpseId);
		buffer.WriteC(_distributionId);
		buffer.WriteD(_playerId);
		buffer.WriteD(_luck);
	}
}
