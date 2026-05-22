using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDeleteWarehouseItem : GameServerPacket
{
	public const int PacketOpCode = 170;

	private readonly int _warehouseType;
	private readonly int _itemObjectId;
	private readonly int _deleteType;

	public SmDeleteWarehouseItem(int warehouseType, int itemObjectId, int deleteType = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_DELETE_WAREHOUSE_ITEM.
		_warehouseType = warehouseType;
		_itemObjectId = itemObjectId;
		_deleteType = deleteType;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_warehouseType);
		buffer.WriteD(_itemObjectId);
		buffer.WriteC(_deleteType);
	}
}
