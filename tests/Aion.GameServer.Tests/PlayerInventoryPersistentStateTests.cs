using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Tests;

public sealed class PlayerInventoryPersistentStateTests
{
	[Fact]
	public void GetDirtyItemsToUpdate_ReturnsDirtyItemsAcrossModeledStorages()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(1001, location: 0, InventoryItemPersistentState.Updated),
				CreateItem(1002, location: 0, InventoryItemPersistentState.UpdateRequired),
			],
			WarehouseItems =
			[
				CreateItem(2001, location: 1, InventoryItemPersistentState.New),
			],
			AccountWarehouseItems =
			[
				CreateItem(3001, location: 2, InventoryItemPersistentState.Deleted),
				CreateItem(3002, location: 2, InventoryItemPersistentState.NoAction),
			],
		};

		var dirtyItems = player.GetDirtyItemsToUpdate();

		Assert.Equal([1002, 2001, 3001], dirtyItems.Select(item => item.ObjectId).ToArray());
	}

	[Fact]
	public void MarkDirtyItemsPersisted_NormalizesDirtyItemsToUpdated()
	{
		var pendingTuneResult = new PendingTuneResult(OptionalSockets: 1, EnchantBonus: 2, StatBonusId: 3, IsAttributeOnly: false);
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(1001, location: 0, InventoryItemPersistentState.UpdateRequired, pendingTuneResult),
			],
			WarehouseItems =
			[
				CreateItem(2001, location: 1, InventoryItemPersistentState.New),
			],
			AccountWarehouseItems =
			[
				CreateItem(3001, location: 2, InventoryItemPersistentState.Deleted),
			],
		};

		player.MarkDirtyItemsPersisted();

		Assert.All(player.GetDirtyItemsToUpdate(), item => Assert.True(false, $"Expected no remaining dirty items, found {item.ObjectId}"));
		Assert.Equal(InventoryItemPersistentState.Updated, player.InventoryItems.Single().PersistentState);
		Assert.Equal(pendingTuneResult, player.InventoryItems.Single().PendingTuneResult);
		Assert.Equal(InventoryItemPersistentState.Updated, player.WarehouseItems.Single().PersistentState);
		Assert.Equal(InventoryItemPersistentState.Updated, player.AccountWarehouseItems.Single().PersistentState);
	}

	private static InventoryItem CreateItem(
		int objectId,
		int location,
		InventoryItemPersistentState persistentState,
		PendingTuneResult? pendingTuneResult = null) =>
		new()
		{
			ObjectId = objectId,
			ItemId = 110100001,
			Count = 1,
			OwnerId = 1001,
			Location = location,
			TuneCount = 0,
			PendingTuneResult = pendingTuneResult,
			PersistentState = persistentState,
		};
}
