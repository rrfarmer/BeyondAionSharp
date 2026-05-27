using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmCubeUpdate : GameServerPacket
{
	public const int PacketOpCode = 130;
	private const int CubeStorageId = 0;
	private const int CubeStorageOrdinal = 0;
	private const int RegularWarehouseStorageId = 1;
	private const int RegularWarehouseStorageOrdinal = 1;
	private const int AccountWarehouseStorageOrdinal = 2;
	private const int LegionWarehouseStorageOrdinal = 3;
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

	public static SmCubeUpdate CubeSizeSnapshot(int itemsCount, int npcExpands, int questExpands, int itemExpands)
	{
		// Java parity: SM_CUBE_UPDATE.cubeSize(StorageType.CUBE, Player) after callers have captured
		// the post-mutation Storage.size()/expand fields at the exact fanout point.
		return new SmCubeUpdate(0, CubeStorageOrdinal, itemsCount, npcExpands, questExpands, itemExpands);
	}

	public static SmCubeUpdate RegularWarehouseSize(Player player)
	{
		// Java parity: SM_CUBE_UPDATE.cubeSize(StorageType.REGULAR_WAREHOUSE, Player).
		var itemsCount = player.WarehouseItems.Count(item => item.Location == RegularWarehouseStorageId && item.ItemId != KinahItemId);
		return new SmCubeUpdate(0, RegularWarehouseStorageOrdinal, itemsCount, player.WarehouseNpcExpands, player.WarehouseBonusExpands, 0);
	}

	public static SmCubeUpdate RegularWarehouseSizeSnapshot(int itemsCount, int npcExpands, int bonusExpands)
	{
		// Java parity: SM_CUBE_UPDATE.cubeSize(StorageType.REGULAR_WAREHOUSE, Player) after callers
		// have captured the post-mutation Storage.size()/warehouse expand fields at the fanout point.
		return new SmCubeUpdate(0, RegularWarehouseStorageOrdinal, itemsCount, npcExpands, bonusExpands, 0);
	}

	public static SmCubeUpdate AccountWarehouseSize()
	{
		// Java parity: SM_CUBE_UPDATE.cubeSize(StorageType.ACCOUNT_WAREHOUSE, Player) falls through to zero counts.
		return new SmCubeUpdate(0, AccountWarehouseStorageOrdinal, 0, 0, 0, 0);
	}

	public static SmCubeUpdate ZeroSizeForJavaStorageOrdinal(int storageTypeOrdinal)
	{
		// Java parity: SM_CUBE_UPDATE.cubeSize(StorageType, Player) writes StorageType.ordinal()
		// even for storage families whose count/expand fields fall through to zero.
		ArgumentOutOfRangeException.ThrowIfNegative(storageTypeOrdinal);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(storageTypeOrdinal, byte.MaxValue);
		return new SmCubeUpdate(0, storageTypeOrdinal, 0, 0, 0, 0);
	}

	public static bool TryGetJavaStorageOrdinal(int storageTypeId, out int storageTypeOrdinal)
	{
		// Java parity: StorageType enum declaration order in model/items/storage/StorageType.java.
		storageTypeOrdinal = storageTypeId switch
		{
			0 => 0,
			1 => 1,
			2 => 2,
			3 => 3,
			>= 32 and <= 43 => storageTypeId - 28,
			>= 60 and <= 79 => storageTypeId - 44,
			126 => 36,
			127 => 37,
			_ => -1,
		};
		return storageTypeOrdinal >= 0;
	}

	public static SmCubeUpdate ZeroSizeForJavaStorageId(int storageTypeId)
	{
		if (!TryGetJavaStorageOrdinal(storageTypeId, out var storageTypeOrdinal))
		{
			throw new ArgumentOutOfRangeException(nameof(storageTypeId), storageTypeId, "Java StorageType id is not modeled.");
		}

		return ZeroSizeForJavaStorageOrdinal(storageTypeOrdinal);
	}

	public static SmCubeUpdate LegionWarehouseSizeSnapshot(int itemsCount, int warehouseExpansions)
	{
		// Java parity: SM_CUBE_UPDATE.cubeSize(StorageType.LEGION_WAREHOUSE, Player) after callers
		// have captured LegionWarehouse.size()/Legion.getWarehouseExpansions() at the fanout point.
		return new SmCubeUpdate(0, LegionWarehouseStorageOrdinal, itemsCount, warehouseExpansions, 0, 0);
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
