using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests;

public sealed class PlayerEnterWorldRepositoryItemStonePersistenceTests
{
	[Fact]
	public void GetPlayerItemStoneSnapshotItems_ReturnsModeledCurrentPlayerItems()
	{
		var cubeItem = new InventoryItem { ObjectId = 9001, ItemId = 100000001 };
		var equippedDuplicate = new InventoryItem { ObjectId = 9001, ItemId = 100000001, IsEquipped = true };
		var warehouseItem = new InventoryItem { ObjectId = 9002, ItemId = 100000002 };
		var accountWarehouseItem = new InventoryItem { ObjectId = 9003, ItemId = 100000003 };
		var deletedItem = new InventoryItem
		{
			ObjectId = 9004,
			ItemId = 100000004,
			PersistentState = InventoryItemPersistentState.Deleted,
		};
		var player = new Player
		{
			InventoryItems = [cubeItem, equippedDuplicate, deletedItem],
			WarehouseItems = [warehouseItem],
			AccountWarehouseItems = [accountWarehouseItem],
		};

		var items = MySqlPlayerEnterWorldRepository.GetPlayerItemStoneSnapshotItems(player);

		Assert.Equal([9001, 9002, 9003], items.Select(item => item.ObjectId).OrderBy(id => id));
	}

	[Fact]
	public void ResolveInventoryStoreOwnerId_UsesLegionOwnerForLegionWarehouseLikeJava()
	{
		// Java source breadcrumbs: InventoryDAO.store(player) -> getItemOwnerId.
		var player = new Player { ObjectId = 1001, AccountId = 77, LegionId = 501 };

		Assert.Equal(1001, MySqlPlayerEnterWorldRepository.ResolveInventoryStoreOwnerId(
			player,
			new InventoryItem { ObjectId = 9001, Location = 0, OwnerId = 9999 }));
		Assert.Equal(77, MySqlPlayerEnterWorldRepository.ResolveInventoryStoreOwnerId(
			player,
			new InventoryItem { ObjectId = 9002, Location = 2, OwnerId = 9999 }));
		Assert.Equal(501, MySqlPlayerEnterWorldRepository.ResolveInventoryStoreOwnerId(
			player,
			new InventoryItem { ObjectId = 9003, Location = 3, OwnerId = 9999 }));
		Assert.Equal(1001, MySqlPlayerEnterWorldRepository.ResolveInventoryStoreOwnerId(
			new Player { ObjectId = 1001, AccountId = 77 },
			new InventoryItem { ObjectId = 9004, Location = 3, OwnerId = 9999 }));
	}

	[Fact]
	public void BuildItemStonePersistenceRows_MapsAllInheritedTargetStoneCategories()
	{
		var item = new InventoryItem
		{
			ObjectId = 9001,
			ItemId = 100000002,
			ManaStones =
			[
				new ItemStoneSocket(167000001, 1),
				new ItemStoneSocket(167000002, 3),
			],
			FusionStones =
			[
				new ItemStoneSocket(167100001, 2),
			],
			Godstone = new PlayerGodstone(168000001, ProcCount: 44),
			IdianStone = new PlayerIdianStone(169000001, PolishNumber: 7, PolishCharge: 250),
		};

		var rows = MySqlPlayerEnterWorldRepository.BuildItemStonePersistenceRows(item);

		Assert.Equal(5, rows.Count);
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 167000001, Slot: 1, Category: 0, PolishNumber: 0, PolishCharge: 0, ProcCount: 0));
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 167000002, Slot: 3, Category: 0, PolishNumber: 0, PolishCharge: 0, ProcCount: 0));
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 168000001, Slot: 0, Category: 1, PolishNumber: 0, PolishCharge: 0, ProcCount: 44));
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 167100001, Slot: 2, Category: 2, PolishNumber: 0, PolishCharge: 0, ProcCount: 0));
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 169000001, Slot: 0, Category: 3, PolishNumber: 7, PolishCharge: 250, ProcCount: 0));
	}

	[Fact]
	public void BuildItemStonePersistenceRows_ReturnsEmptyRowsForPlainItem()
	{
		var item = new InventoryItem { ObjectId = 9002, ItemId = 186000001 };

		var rows = MySqlPlayerEnterWorldRepository.BuildItemStonePersistenceRows(item);

		Assert.Empty(rows);
	}
}
