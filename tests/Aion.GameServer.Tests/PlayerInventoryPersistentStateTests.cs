using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Tests;

public sealed class PlayerInventoryPersistentStateTests
{
	[Theory]
	[InlineData(InventoryItemPersistentState.New, InventoryItemPersistentState.UpdateRequired, InventoryItemPersistentState.New)]
	[InlineData(InventoryItemPersistentState.New, InventoryItemPersistentState.Deleted, InventoryItemPersistentState.NoAction)]
	[InlineData(InventoryItemPersistentState.Updated, InventoryItemPersistentState.UpdateRequired, InventoryItemPersistentState.UpdateRequired)]
	[InlineData(InventoryItemPersistentState.UpdateRequired, InventoryItemPersistentState.Deleted, InventoryItemPersistentState.Deleted)]
	[InlineData(InventoryItemPersistentState.Updated, InventoryItemPersistentState.Updated, InventoryItemPersistentState.Updated)]
	public void TransitionPersistentState_MirrorsJavaItemStateRules(
		InventoryItemPersistentState currentState,
		InventoryItemPersistentState requestedState,
		InventoryItemPersistentState expectedState)
	{
		var actualState = InventoryItem.TransitionPersistentState(currentState, requestedState);

		Assert.Equal(expectedState, actualState);
	}

	[Fact]
	public void GetDirtyItemsToUpdate_ReturnsDirtyItemsAcrossModeledStorages()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(1001, location: 0, InventoryItemPersistentState.Updated),
				CreateItem(1002, location: 0, InventoryItemPersistentState.UpdateRequired),
				CreateItem(1003, location: 0, InventoryItemPersistentState.Updated, isEquipped: true),
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

		Assert.Equal([1001, 1002, 2001, 3001, 3002], dirtyItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.WarehouseStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.AccountWarehouseStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.EquipmentPersistentState);
	}

	[Fact]
	public void GetDirtyItemsToUpdate_IncludesTrackedDeletedItemsAcrossModeledStorages()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(1001, location: 0, InventoryItemPersistentState.Updated),
				CreateItem(4001, location: 3, InventoryItemPersistentState.Updated),
			],
			WarehouseItems = [CreateItem(2001, location: 1, InventoryItemPersistentState.Updated)],
			AccountWarehouseItems = [CreateItem(3001, location: 2, InventoryItemPersistentState.Updated)],
		};

		player.TrackDeletedItem(player.InventoryItems.Single(item => item.Location == 0));
		player.TrackDeletedItem(player.WarehouseItems.Single());
		player.TrackDeletedItem(player.AccountWarehouseItems.Single());
		player.TrackDeletedItem(player.InventoryItems.Single(item => item.Location == 3));

		var dirtyItems = player.GetDirtyItemsToUpdate();

		Assert.Equal(
			[
				(1001, InventoryItemPersistentState.Deleted),
				(2001, InventoryItemPersistentState.Deleted),
				(3001, InventoryItemPersistentState.Deleted),
				(4001, InventoryItemPersistentState.Deleted),
			],
			dirtyItems.Select(item => (item.ObjectId, item.PersistentState)).ToArray());
		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.WarehouseStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.AccountWarehouseStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.LegionWarehouseStoragePersistentState);
	}

	[Fact]
	public void GetDirtyItemsToUpdate_IncludesTrackedDeletedLegionWarehouseItemsLikeJavaStorageDelete()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(1001, location: 0, InventoryItemPersistentState.Updated),
				CreateItem(4001, location: 3, InventoryItemPersistentState.Updated),
			],
		};

		player.TrackDeletedItem(player.InventoryItems.Single(item => item.Location == 3));

		var dirtyItems = player.GetDirtyItemsToUpdate();
		var secondHarvest = player.GetDirtyItemsToUpdate();

		Assert.Equal(
			[
				(4001, InventoryItemPersistentState.Deleted),
			],
			dirtyItems.Select(item => (item.ObjectId, item.PersistentState)).ToArray());
		Assert.Empty(secondHarvest);
		Assert.Equal([1001], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Empty(player.DeletedInventoryItems);
		Assert.Equal([4001], player.DeletedLegionWarehouseItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.LegionWarehouseStoragePersistentState);
	}

	[Fact]
	public void TrackDeletedItem_DropsNewItemsAsNoAction()
	{
		var player = new Player
		{
			InventoryItems = [CreateItem(1001, location: 0, InventoryItemPersistentState.New)],
		};

		player.TrackDeletedItem(player.InventoryItems.Single());

		Assert.Empty(player.DeletedInventoryItems);
		Assert.Empty(player.InventoryItems);
		Assert.Empty(player.GetDirtyItemsToUpdate());
	}

	[Fact]
	public void GetDirtyItemsToUpdate_HarvestsDirtyEquippedItemsThroughEquipmentState()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(1001, location: 0, InventoryItemPersistentState.Updated),
				CreateItem(1002, location: 0, InventoryItemPersistentState.UpdateRequired, isEquipped: true),
				CreateItem(1003, location: 0, InventoryItemPersistentState.Updated, isEquipped: true),
			],
		};

		var dirtyItems = player.GetDirtyItemsToUpdate();

		Assert.Equal([1002, 1003], dirtyItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.EquipmentPersistentState);
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

		Assert.Empty(player.GetDirtyItemsToUpdate());
		Assert.Equal(InventoryItemPersistentState.Updated, player.InventoryItems.Single().PersistentState);
		Assert.Equal(pendingTuneResult, player.InventoryItems.Single().PendingTuneResult);
		Assert.Equal(InventoryItemPersistentState.Updated, player.WarehouseItems.Single().PersistentState);
		Assert.Equal(InventoryItemPersistentState.Updated, player.AccountWarehouseItems.Single().PersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.WarehouseStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.AccountWarehouseStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.EquipmentPersistentState);
	}

	[Fact]
	public void MarkDirtyItemsPersisted_ClearsTrackedDeletedItems()
	{
		var player = new Player
		{
			InventoryItems = [CreateItem(1001, location: 0, InventoryItemPersistentState.Updated)],
		};

		player.TrackDeletedItem(player.InventoryItems.Single());
		player.MarkDirtyItemsPersisted();

		Assert.Empty(player.DeletedInventoryItems);
		Assert.Empty(player.GetDirtyItemsToUpdate());
	}

	[Fact]
	public void MarkDirtyItemsPersisted_ClearsTrackedDeletedLegionWarehouseItems()
	{
		var player = new Player
		{
			InventoryItems = [CreateItem(4001, location: 3, InventoryItemPersistentState.Updated)],
		};

		player.TrackDeletedItem(player.InventoryItems.Single());
		player.MarkDirtyItemsPersisted();

		Assert.Empty(player.DeletedLegionWarehouseItems);
		Assert.Empty(player.GetDirtyItemsToUpdate());
	}

	[Fact]
	public void GetDirtyItemsToUpdate_ReturnsAllCurrentRowsWhenOneStorageItemIsDirty()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(1001, location: 0, InventoryItemPersistentState.UpdateRequired),
				CreateItem(1002, location: 0, InventoryItemPersistentState.Updated),
			],
		};

		var dirtyItems = player.GetDirtyItemsToUpdate();

		Assert.Equal([1001, 1002], dirtyItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
	}

	[Fact]
	public void MarkStorageDirty_HarvestsAllCurrentRowsEvenWhenTheyAreUpdated()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(1001, location: 0, InventoryItemPersistentState.Updated),
				CreateItem(1002, location: 0, InventoryItemPersistentState.Updated),
			],
		};

		player.MarkStorageDirty(location: 0);

		var firstHarvest = player.GetDirtyItemsToUpdate();
		var secondHarvest = player.GetDirtyItemsToUpdate();

		Assert.Equal([1001, 1002], firstHarvest.Select(item => item.ObjectId).ToArray());
		Assert.Empty(secondHarvest);
		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
	}

	[Fact]
	public void MarkEquipmentDirty_HarvestsEquippedRowsEvenWhenTheyAreUpdated()
	{
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(1001, location: 0, InventoryItemPersistentState.Updated),
				CreateItem(1002, location: 0, InventoryItemPersistentState.Updated, isEquipped: true),
				CreateItem(1003, location: 0, InventoryItemPersistentState.Updated, isEquipped: true),
			],
		};

		player.MarkEquipmentDirty();

		var firstHarvest = player.GetDirtyItemsToUpdate();
		var secondHarvest = player.GetDirtyItemsToUpdate();

		Assert.Equal([1002, 1003], firstHarvest.Select(item => item.ObjectId).ToArray());
		Assert.Empty(secondHarvest);
		Assert.Equal(StoragePersistentState.Updated, player.EquipmentPersistentState);
	}

	[Fact]
	public void AssigningDirtyRows_PromotesTheModeledStorageState()
	{
		var player = new Player();

		player.InventoryItems =
		[
			CreateItem(1001, location: 0, InventoryItemPersistentState.UpdateRequired),
			CreateItem(1002, location: 0, InventoryItemPersistentState.Updated),
		];
		player.WarehouseItems =
		[
			CreateItem(2001, location: 1, InventoryItemPersistentState.New),
		];
		player.AccountWarehouseItems =
		[
			CreateItem(3001, location: 2, InventoryItemPersistentState.Deleted),
		];

		Assert.Equal(StoragePersistentState.UpdateRequired, player.InventoryStoragePersistentState);
		Assert.Equal(StoragePersistentState.UpdateRequired, player.WarehouseStoragePersistentState);
		Assert.Equal(StoragePersistentState.UpdateRequired, player.AccountWarehouseStoragePersistentState);
		Assert.Equal(StoragePersistentState.Updated, player.EquipmentPersistentState);
	}

	[Fact]
	public void AssigningDirtyEquippedRows_PromotesEquipmentStateWithoutDirtyingInventoryStorage()
	{
		var player = new Player();

		player.InventoryItems =
		[
			CreateItem(1001, location: 0, InventoryItemPersistentState.UpdateRequired, isEquipped: true),
			CreateItem(1002, location: 0, InventoryItemPersistentState.Updated, isEquipped: false),
		];

		Assert.Equal(StoragePersistentState.Updated, player.InventoryStoragePersistentState);
		Assert.Equal(StoragePersistentState.UpdateRequired, player.EquipmentPersistentState);
	}

	private static InventoryItem CreateItem(
		int objectId,
		int location,
		InventoryItemPersistentState persistentState,
		PendingTuneResult? pendingTuneResult = null,
		bool isEquipped = false) =>
		new()
		{
			ObjectId = objectId,
			ItemId = 110100001,
			Count = 1,
			OwnerId = 1001,
			Location = location,
			IsEquipped = isEquipped,
			TuneCount = 0,
			PendingTuneResult = pendingTuneResult,
			PersistentState = persistentState,
		};
}
