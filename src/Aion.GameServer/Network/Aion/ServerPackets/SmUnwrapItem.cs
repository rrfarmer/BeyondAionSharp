using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmUnwrapItem : GameServerPacket
{
	public const int PacketOpCode = 289;

	private readonly int _itemObjectId;
	private readonly int _packCount;

	public SmUnwrapItem(int itemObjectId, int packCount)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_UNWRAP_ITEM(int, int).
		_itemObjectId = itemObjectId;
		_packCount = packCount;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_UNWRAP_ITEM.writeImpl.
		buffer.WriteD(_itemObjectId);
		buffer.WriteC(_packCount);
	}
}
