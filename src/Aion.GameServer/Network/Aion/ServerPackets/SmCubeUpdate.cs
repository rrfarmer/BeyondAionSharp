using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmCubeUpdate : GameServerPacket
{
	public const int PacketOpCode = 130;
	private const int CubeStorageId = 0;
	private const int CubeStorageOrdinal = 0;
	private const int KinahItemId = 182400001;

	private readonly int _action;
	private readonly int _actionValue;
	private readonly int _itemsCount;
	private readonly int _npcExpands;
	private readonly int _questExpands;
	private readonly int _itemExpands;

	private SmCubeUpdate(int action, int actionValue, int itemsCount, int npcExpands, int questExpands, int itemExpands)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_CUBE_UPDATE(int action, int actionValue, ...).
		_action = action;
		_actionValue = actionValue;
		_itemsCount = itemsCount;
		_npcExpands = npcExpands;
		_questExpands = questExpands;
		_itemExpands = itemExpands;
	}

	public static SmCubeUpdate CubeSize(Player player)
	{
		// Java parity: SM_CUBE_UPDATE.cubeSize(StorageType.CUBE, Player).
		var itemsCount = player.InventoryItems.Count(item => item.Location == CubeStorageId && item.ItemId != KinahItemId);
		return new SmCubeUpdate(0, CubeStorageOrdinal, itemsCount, player.NpcExpands, player.QuestExpands, player.ItemExpands);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_CUBE_UPDATE.writeImpl.
		buffer.WriteC(_action);
		buffer.WriteC(_actionValue);
		if (_action == 0)
		{
			buffer.WriteD(_itemsCount);
			buffer.WriteC(_npcExpands);
			buffer.WriteC(_questExpands);
			buffer.WriteC(_itemExpands);
		}
	}
}
