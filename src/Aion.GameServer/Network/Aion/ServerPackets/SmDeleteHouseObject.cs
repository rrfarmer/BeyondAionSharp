using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDeleteHouseObject : GameServerPacket
{
	public const int PacketOpCode = 269;

	private readonly int _itemObjectId;

	public SmDeleteHouseObject(int itemObjectId)
		: base(PacketOpCode)
	{
		_itemObjectId = itemObjectId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_DELETE_HOUSE_OBJECT.writeImpl.
		buffer.WriteD(_itemObjectId);
	}
}
