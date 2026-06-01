using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class RepurchasePlanServiceTests
{
	[Fact]
	public void CreatePlan_RepurchasesNonStackableSourceItemWithCloneAndKinahDecrease()
	{
		var player = new Player { ObjectId = 1001 };
		var kinah = Item(10, KinahItemId, 5_000, ownerId: player.ObjectId);
		var repurchaseItem = new InventoryItem
		{
			ObjectId = 200,
			ItemId = SwordItemId,
			Count = 1,
			OwnerId = player.ObjectId,
			Location = 0,
			Slot = 65535,
			Color = 123,
			Creator = "maker",
			Enchant = 7,
			OptionalSocket = 2,
			RandomBonus = 9,
			IsSoulBound = true,
		};
		repurchaseItem.ManaStones = [new ItemStoneSocket(167000001, 0)];
		repurchaseItem.Godstone = new PlayerGodstone(168000001, ProcCount: 3);

		var plan = CreatePlan(
			player,
			inventoryItems: [kinah],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 1_200)]);

		Assert.Equal(RepurchasePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Equal([repurchaseItem.ObjectId], plan.RepurchasedItemObjectIds);
		Assert.Equal([repurchaseItem.ObjectId], plan.RemovedRepurchaseItemObjectIds);
		Assert.Empty(plan.MissingRepurchaseItemObjectIds);
		Assert.Empty(plan.InsufficientKinahItemObjectIds);
		Assert.Equal(3_800, plan.KinahUpdate!.Count);

		var addedItem = Assert.Single(plan.AddedItems);
		Assert.Equal((100, SwordItemId, 1L, player.ObjectId, 0, 65535L), (addedItem.ObjectId, addedItem.ItemId, addedItem.Count, addedItem.OwnerId, addedItem.Location, addedItem.Slot));
		Assert.Equal(repurchaseItem.Color, addedItem.Color);
		Assert.Equal(repurchaseItem.Creator, addedItem.Creator);
		Assert.Equal(repurchaseItem.Enchant, addedItem.Enchant);
		Assert.Equal(repurchaseItem.OptionalSocket, addedItem.OptionalSocket);
		Assert.Equal(repurchaseItem.RandomBonus, addedItem.RandomBonus);
		Assert.True(addedItem.IsSoulBound);
		Assert.Equal(repurchaseItem.ManaStones, addedItem.ManaStones);
		Assert.Equal(repurchaseItem.Godstone, addedItem.Godstone);
	}

	[Fact]
	public void CreatePlan_AllowsOverflowAfterJavaPrecheckPasses()
	{
		var player = new Player { ObjectId = 1001 };
		var fillerItems = Enumerable.Range(0, 26)
			.Select(index => Item(index + 1, 3_000 + index, 1, ownerId: player.ObjectId))
			.ToArray();
		var repurchaseItem = Item(200, SwordItemId, 2, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 10_000, ownerId: player.ObjectId), .. fillerItems],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100)]);

		Assert.Equal(RepurchasePlanStatus.PlanCreated, plan.Status);
		Assert.Equal(2, plan.AddedItems.Count);
		Assert.Equal([100, 101], plan.AddedItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(9_900, plan.KinahUpdate!.Count);
	}

	[Fact]
	public void CreatePlan_InventoryFullBlocksBeforeStackableMerge()
	{
		var player = new Player { ObjectId = 1001 };
		var fillerItems = Enumerable.Range(0, 26)
			.Select(index => Item(index + 1, 3_000 + index, 1, ownerId: player.ObjectId))
			.ToArray();
		var existingStack = Item(50, StackableItemId, 1, ownerId: player.ObjectId);
		var repurchaseItem = Item(200, StackableItemId, 2, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 10_000, ownerId: player.ObjectId), .. fillerItems, existingStack],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100)]);

		Assert.Equal(RepurchasePlanStatus.BlockedInventoryFull, plan.Status);
		Assert.Equal(1390182, Assert.Single(plan.Messages).MessageId);
		Assert.Empty(plan.AddedItems);
		Assert.Empty(plan.UpdatedItems);
		Assert.Equal(10_000, plan.KinahUpdate!.Count);
	}

	[Fact]
	public void CreatePlan_MissingRepurchaseItemIsSkipped()
	{
		var player = new Player { ObjectId = 1001 };

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 10_000, ownerId: player.ObjectId)],
			requestedItemObjectIds: [999],
			repurchaseItems: []);

		Assert.Equal(RepurchasePlanStatus.PlanCreated, plan.Status);
		Assert.Equal([999], plan.MissingRepurchaseItemObjectIds);
		Assert.Empty(plan.RepurchasedItemObjectIds);
		Assert.Equal(10_000, plan.KinahUpdate!.Count);
	}

	[Fact]
	public void CreatePlan_InsufficientKinahSkipsItemAndContinues()
	{
		var player = new Player { ObjectId = 1001 };
		var expensiveItem = Item(200, SwordItemId, 1, ownerId: player.ObjectId);
		var affordableItem = Item(201, StackableItemId, 3, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 100, ownerId: player.ObjectId)],
			requestedItemObjectIds: [expensiveItem.ObjectId, affordableItem.ObjectId],
			repurchaseItems:
			[
				new RepurchaseSourceItem(expensiveItem, RepurchasePrice: 200),
				new RepurchaseSourceItem(affordableItem, RepurchasePrice: 50),
			]);

		Assert.Equal(RepurchasePlanStatus.PlanCreated, plan.Status);
		Assert.Equal([expensiveItem.ObjectId], plan.InsufficientKinahItemObjectIds);
		Assert.Equal([affordableItem.ObjectId], plan.RepurchasedItemObjectIds);
		Assert.Equal(
			[$"tried to repurchase item {expensiveItem.ItemId}, count: {expensiveItem.Count} without kinah"],
			plan.AuditMessages);
		Assert.Equal(50, plan.KinahUpdate!.Count);
		var addedItem = Assert.Single(plan.AddedItems);
		Assert.Equal((StackableItemId, 3L), (addedItem.ItemId, addedItem.Count));
	}

	[Fact]
	public void CreatePlan_RemovesSuccessfulRepurchaseItemBeforeRepeatedRequestCanMatchAgain()
	{
		var player = new Player { ObjectId = 1001 };
		var repurchaseItem = Item(200, SwordItemId, 1, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 10_000, ownerId: player.ObjectId)],
			requestedItemObjectIds: [repurchaseItem.ObjectId, repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100)]);

		Assert.Equal(RepurchasePlanStatus.PlanCreated, plan.Status);
		Assert.Equal([repurchaseItem.ObjectId], plan.RepurchasedItemObjectIds);
		Assert.Equal([repurchaseItem.ObjectId], plan.RemovedRepurchaseItemObjectIds);
		Assert.Equal([repurchaseItem.ObjectId], plan.MissingRepurchaseItemObjectIds);
		Assert.Single(plan.AddedItems);
		Assert.Equal(9_900, plan.KinahUpdate!.Count);
	}

	[Fact]
	public void CreatePlan_CannotTradeBlocksBeforeMutations()
	{
		var player = new Player { ObjectId = 1001 };
		var repurchaseItem = Item(200, SwordItemId, 1, ownerId: player.ObjectId);

		var plan = RepurchasePlanService.CreatePlan(
			canTrade: false,
			player,
			inventoryItems: [Item(99, KinahItemId, 10_000, ownerId: player.ObjectId)],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100)],
			CreateTemplates(),
			nextObjectId: () => 100);

		Assert.Equal(RepurchasePlanStatus.BlockedCannotTrade, plan.Status);
		Assert.Null(plan.KinahUpdate);
		Assert.Empty(plan.AddedItems);
		Assert.Empty(plan.RemovedRepurchaseItemObjectIds);
	}

	[Fact]
	public void CreatePlan_MissingTemplateBlocksConservatively()
	{
		var player = new Player { ObjectId = 1001 };
		var repurchaseItem = Item(200, MissingTemplateItemId, 1, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 10_000, ownerId: player.ObjectId)],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100)]);

		Assert.Equal(RepurchasePlanStatus.BlockedMissingTemplate, plan.Status);
		Assert.Equal(10_000, plan.KinahUpdate!.Count);
		Assert.Empty(plan.AddedItems);
		Assert.Empty(plan.RemovedRepurchaseItemObjectIds);
	}

	[Fact]
	public void CreatePlan_AddFailureBlocksWithoutKinahDecrease()
	{
		var player = new Player { ObjectId = 1001 };
		var repurchaseItem = Item(200, SwordItemId, 1, ownerId: player.ObjectId);

		var plan = RepurchasePlanService.CreatePlan(
			canTrade: true,
			player,
			inventoryItems: [Item(99, KinahItemId, 10_000, ownerId: player.ObjectId)],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100)],
			CreateTemplates(),
			nextObjectId: () => 0);

		Assert.Equal(RepurchasePlanStatus.BlockedAddFailed, plan.Status);
		Assert.Equal(10_000, plan.KinahUpdate!.Count);
		Assert.Empty(plan.AddedItems);
		Assert.Empty(plan.RemovedRepurchaseItemObjectIds);
	}

	[Fact]
	public void CreateDisabledOutcome_CarriesPostRepurchaseStateRemovalPlanWhenSnapshotContextIsSupplied()
	{
		var player = new Player { ObjectId = 1001 };
		var repurchaseItem = Item(200, SwordItemId, 1, ownerId: player.ObjectId);
		var remainingItem = Item(201, StackableItemId, 3, ownerId: player.ObjectId);
		var repurchaseItems =
			new[]
			{
				new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100),
				new RepurchaseSourceItem(remainingItem, RepurchasePrice: 50),
			};
		var repurchasePlan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 10_000, ownerId: player.ObjectId)],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems);
		var currentSnapshot = new RepurchaseStateSnapshot(
			player.ObjectId,
			repurchaseItems,
			"current supplied repurchase set");

		var outcome = RepurchaseOutcomePlanService.CreateDisabledPlan(
			repurchasePlan,
			player.ObjectId,
			[currentSnapshot]);

		Assert.Equal(RepurchaseOutcomePlanStatus.DisabledNoTransaction, outcome.Status);
		Assert.True(outcome.WouldRemoveRepurchaseItems);
		Assert.Equal(
			[
				RepurchaseSuccessOperationKind.DecreaseKinah,
				RepurchaseSuccessOperationKind.AddItem,
				RepurchaseSuccessOperationKind.RemoveRepurchaseItem,
			],
			outcome.SuccessOperations.Select(operation => operation.Kind));
		Assert.All(outcome.SuccessOperations, operation =>
		{
			Assert.Equal(repurchaseItem.ObjectId, operation.ItemObjectId);
			Assert.True(operation.WouldRun);
			Assert.False(operation.DidRun);
			Assert.Contains("RepurchaseService.repurchaseFromShop", operation.JavaSource, StringComparison.Ordinal);
		});
		Assert.Collection(
			outcome.PacketIntents,
			intent =>
			{
				Assert.Equal(RepurchasePacketIntentKind.SendKinahUpdate, intent.Kind);
				Assert.Equal(99, intent.ItemObjectId);
				Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, intent.PacketMask);
				Assert.True(intent.WouldSend);
				Assert.False(intent.DidSend);
				Assert.Contains("DEC_KINAH_BUY", intent.JavaSource, StringComparison.Ordinal);
			},
			intent =>
			{
				Assert.Equal(RepurchasePacketIntentKind.SendRepurchasedItemAdd, intent.Kind);
				Assert.NotNull(intent.ItemObjectId);
				Assert.Equal(SmInventoryAddItem.ItemCollect, intent.PacketMask);
				Assert.Contains("ITEM_COLLECT", intent.JavaSource, StringComparison.Ordinal);
			},
			intent =>
			{
				Assert.Equal(RepurchasePacketIntentKind.SendCubeSizeUpdate, intent.Kind);
				Assert.NotNull(intent.ItemObjectId);
				Assert.Equal(SmCubeUpdate.PacketOpCode, intent.PacketMask);
				Assert.Contains("SM_CUBE_UPDATE.cubeSize", intent.JavaSource, StringComparison.Ordinal);
			});
		var stateRemoval = Assert.IsType<RepurchaseStateItemRemovalPlan>(outcome.StateItemRemovalPlan);
		Assert.Equal(RepurchaseStateItemRemovalPlanStatus.SnapshotUpdated, stateRemoval.Status);
		Assert.Equal([repurchaseItem.ObjectId], stateRemoval.RemovedItemObjectIds);
		Assert.Empty(stateRemoval.MissingItemObjectIds);
		Assert.Equal([remainingItem.ObjectId], stateRemoval.UpdatedSnapshot!.RepurchaseItems.Select(item => item.Item.ObjectId));
		Assert.False(stateRemoval.DidRemoveItems);
		Assert.False(stateRemoval.IsLive);
	}

	[Fact]
	public void CreateDisabledOutcome_LeavesStateRemovalPlanNullWithoutSnapshotContext()
	{
		var player = new Player { ObjectId = 1001 };
		var repurchaseItem = Item(200, SwordItemId, 1, ownerId: player.ObjectId);
		var repurchasePlan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 10_000, ownerId: player.ObjectId)],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100)]);

		var outcome = RepurchaseOutcomePlanService.CreateDisabledPlan(repurchasePlan);

		Assert.Equal(RepurchaseOutcomePlanStatus.DisabledNoTransaction, outcome.Status);
		Assert.True(outcome.WouldRemoveRepurchaseItems);
		Assert.Equal(3, outcome.SuccessOperations.Count);
		Assert.Equal(3, outcome.PacketIntents.Count);
		Assert.Null(outcome.StateItemRemovalPlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreateDisabledOutcome_RecordsStackMergeAndInventoryFullPacketIntents()
	{
		var player = new Player { ObjectId = 1001 };
		var kinah = Item(99, KinahItemId, 10_000, ownerId: player.ObjectId);
		var existingStack = Item(50, StackableItemId, 1, ownerId: player.ObjectId);
		var repurchaseItem = Item(200, StackableItemId, 2, ownerId: player.ObjectId);
		var mergePlan = CreatePlan(
			player,
			inventoryItems: [kinah, existingStack],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100)]);
		var fillerItems = Enumerable.Range(0, 26)
			.Select(index => Item(index + 1, 3_000 + index, 1, ownerId: player.ObjectId))
			.ToArray();
		var fullPlan = CreatePlan(
			player,
			inventoryItems: [kinah, .. fillerItems, existingStack],
			requestedItemObjectIds: [repurchaseItem.ObjectId],
			repurchaseItems: [new RepurchaseSourceItem(repurchaseItem, RepurchasePrice: 100)]);

		var mergeOutcome = RepurchaseOutcomePlanService.CreateDisabledPlan(mergePlan);
		var fullOutcome = RepurchaseOutcomePlanService.CreateDisabledPlan(fullPlan);

		Assert.Equal(RepurchaseOutcomePlanStatus.DisabledNoTransaction, mergeOutcome.Status);
		Assert.Contains(mergeOutcome.PacketIntents, intent =>
			intent.Kind == RepurchasePacketIntentKind.SendRepurchasedItemUpdate
			&& intent.ItemObjectId == existingStack.ObjectId
			&& intent.PacketMask == SmInventoryUpdateItem.IncreaseItemCollect);
		Assert.DoesNotContain(mergeOutcome.PacketIntents, intent => intent.Kind == RepurchasePacketIntentKind.SendCubeSizeUpdate);
		Assert.Equal(RepurchaseOutcomePlanStatus.DisabledNoTransaction, fullOutcome.Status);
		var fullIntent = Assert.Single(fullOutcome.PacketIntents);
		Assert.Equal(RepurchasePacketIntentKind.SendInventoryFullMessage, fullIntent.Kind);
		Assert.Null(fullIntent.ItemObjectId);
		Assert.Null(fullIntent.PacketMask);
		Assert.Contains("STR_MSG_DICE_INVEN_ERROR", fullIntent.JavaSource, StringComparison.Ordinal);
	}

	private static RepurchasePlan CreatePlan(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<int> requestedItemObjectIds,
		IReadOnlyList<RepurchaseSourceItem> repurchaseItems)
	{
		var nextObjectId = 99;
		player.InventoryItems = inventoryItems;
		return RepurchasePlanService.CreatePlan(
			canTrade: true,
			player,
			inventoryItems,
			requestedItemObjectIds,
			repurchaseItems,
			CreateTemplates(),
			() => ++nextObjectId);
	}

	private static InventoryItem Item(int objectId, int itemId, long count, int ownerId)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			OwnerId = ownerId,
			Location = 0,
			Slot = 65535,
		};
	}

	private static ItemTemplateTable CreateTemplates()
	{
		var fillerTemplates = Enumerable.Range(3_000, 27)
			.Select(itemId => Template(itemId, maxStackCount: 1))
			.ToArray();
		return new ItemTemplateTable(
		[
			Template(KinahItemId, maxStackCount: 1),
			Template(SwordItemId, maxStackCount: 1),
			Template(StackableItemId, maxStackCount: 10),
			.. fillerTemplates,
		]);
	}

	private static ItemTemplateSummary Template(int itemId, int maxStackCount)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			DescriptionId: 1,
			Mask: 0,
			Level: 1,
			ItemGroup: "NORMAL",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: maxStackCount,
			Price: 0,
			ValidEquipmentSlots: 0);
	}

	private const int KinahItemId = 182400001;
	private const int SwordItemId = 100000001;
	private const int StackableItemId = 182003001;
	private const int MissingTemplateItemId = 100000099;
}
