using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum RepurchasePlanStatus
{
	PlanCreated,
	BlockedCannotTrade,
	BlockedInventoryFull,
	BlockedMissingTemplate,
	BlockedAddFailed,
}

public sealed record RepurchaseSourceItem(
	InventoryItem Item,
	long RepurchasePrice);

public sealed record RepurchasePlan(
	RepurchasePlanStatus Status,
	IReadOnlyList<int> RequestedItemObjectIds,
	IReadOnlyList<int> RepurchasedItemObjectIds,
	IReadOnlyList<int> MissingRepurchaseItemObjectIds,
	IReadOnlyList<int> InsufficientKinahItemObjectIds,
	IReadOnlyList<InventoryItem> AddedItems,
	IReadOnlyList<InventoryItem> UpdatedItems,
	InventoryItem? KinahUpdate,
	IReadOnlyList<int> RemovedRepurchaseItemObjectIds,
	IReadOnlyList<SmSystemMessage> Messages,
	IReadOnlyList<string> AuditMessages,
	string JavaSource)
{
	public bool IsLive => false;
}

public enum RepurchaseOutcomePlanStatus
{
	MissingRepurchasePlan,
	RepurchasePlanNotReady,
	DisabledNoTransaction,
}

public enum RepurchaseOutcomeStepKind
{
	PersistInventoryWrites,
	RemoveRepurchaseItems,
	DispatchPacketIntents,
	WriteAuditLog,
	CommitSideEffectBoundary,
}

public sealed record RepurchaseOutcomeStepPlan(
	RepurchaseOutcomeStepKind Kind,
	bool WouldRun,
	bool DidRun,
	string JavaSource);

public sealed record RepurchaseOutcomePlan(
	RepurchaseOutcomePlanStatus Status,
	RepurchasePlan? RepurchasePlan,
	IReadOnlyList<RepurchaseOutcomeStepPlan> Steps,
	bool WouldWritePersistence,
	bool DidWritePersistence,
	bool WouldMutatePlayerInventory,
	bool DidMutatePlayerInventory,
	bool WouldRemoveRepurchaseItems,
	bool DidRemoveRepurchaseItems,
	bool WouldMutateKinah,
	bool DidMutateKinah,
	bool WouldSendPackets,
	bool DidSendPackets,
	bool WouldWriteAuditLog,
	bool DidWriteAuditLog,
	bool WouldCommitTransactionBoundary,
	bool DidCommitTransactionBoundary,
	bool ShouldCommitTransactionBoundary,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public static class RepurchasePlanService
{
	private const int CubeStorageId = 0;

	public static RepurchasePlan CreatePlan(
		bool canTrade,
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<int> requestedItemObjectIds,
		IReadOnlyList<RepurchaseSourceItem> repurchaseItems,
		ItemTemplateTable itemTemplates,
		Func<int> nextObjectId)
	{
		// Java parity: services/RepurchaseService.repurchaseFromShop.
		if (!canTrade)
			return Block(
				RepurchasePlanStatus.BlockedCannotTrade,
				requestedItemObjectIds,
				"RepurchaseService.repurchaseFromShop -> !PlayerRestrictions.canTrade(player) -> return");

		var workingItems = inventoryItems.ToList();
		var addedItems = new List<InventoryItem>();
		var updatedItems = new List<InventoryItem>();
		var repurchasedIds = new List<int>();
		var missingIds = new List<int>();
		var insufficientKinahIds = new List<int>();
		var removedRepurchaseIds = new List<int>();
		var auditMessages = new List<string>();
		var workingRepurchaseItems = repurchaseItems.ToList();
		var kinahItem = workingItems.FirstOrDefault(item => item.ItemId == InventoryItemFactory.KinahItemId && item.Location == CubeStorageId);
		var kinahCount = kinahItem?.Count ?? 0;

		foreach (var itemObjectId in requestedItemObjectIds)
		{
			if (InventoryCapacity.GetFreeCubeSlots(player, workingItems) == 0)
			{
				return CreatePlan(
					RepurchasePlanStatus.BlockedInventoryFull,
					requestedItemObjectIds,
					repurchasedIds,
					missingIds,
					insufficientKinahIds,
					addedItems,
					updatedItems,
					kinahItem,
					kinahCount,
					removedRepurchaseIds,
					messages: [SmSystemMessage.DiceInventoryError()],
					auditMessages,
					"RepurchaseService.repurchaseFromShop -> player.getInventory().isFull() -> STR_MSG_DICE_INVEN_ERROR and break");
			}

			var repurchaseItem = workingRepurchaseItems.FirstOrDefault(item => item.Item.ObjectId == itemObjectId);
			if (repurchaseItem == null)
			{
				missingIds.Add(itemObjectId);
				continue;
			}

			if (kinahCount < repurchaseItem.RepurchasePrice)
			{
				insufficientKinahIds.Add(itemObjectId);
				auditMessages.Add($"tried to repurchase item {repurchaseItem.Item.ItemId}, count: {repurchaseItem.Item.Count} without kinah");
				continue;
			}

			var template = itemTemplates.GetItemTemplate(repurchaseItem.Item.ItemId);
			if (template == null)
				return CreatePlan(
					RepurchasePlanStatus.BlockedMissingTemplate,
					requestedItemObjectIds,
					repurchasedIds,
					missingIds,
					insufficientKinahIds,
					addedItems,
					updatedItems,
					kinahItem,
					kinahCount,
					removedRepurchaseIds,
					messages: Array.Empty<SmSystemMessage>(),
					auditMessages,
					"ItemService.addItem(player, repurchaseItem) -> DataManager.ITEM_DATA missing template would fail before inventory mutation");

			kinahCount -= repurchaseItem.RepurchasePrice;
			var addPlan = InventoryAddService.CreateAddItemPlan(
				player,
				workingItems,
				template,
				repurchaseItem.Item.Count,
				nextObjectId,
				allowInventoryOverflow: true,
				itemTemplates: itemTemplates,
				sourceItem: repurchaseItem.Item);

			if (!addPlan.Succeeded)
				return CreatePlan(
					RepurchasePlanStatus.BlockedAddFailed,
					requestedItemObjectIds,
					repurchasedIds,
					missingIds,
					insufficientKinahIds,
					addedItems,
					updatedItems,
					kinahItem,
					kinahCount + repurchaseItem.RepurchasePrice,
					removedRepurchaseIds,
					messages: addPlan.InventoryFull ? [SmSystemMessage.DiceInventoryError()] : Array.Empty<SmSystemMessage>(),
					auditMessages,
					"ItemService.addItem(player, repurchaseItem) returned remaining count; live exception/partial-add behavior is not wired in this non-live planner");

			addedItems.AddRange(addPlan.AddedItems);
			updatedItems.AddRange(addPlan.UpdatedItems);
			ApplyAddPlan(workingItems, addPlan);
			workingRepurchaseItems.Remove(repurchaseItem);
			repurchasedIds.Add(itemObjectId);
			removedRepurchaseIds.Add(itemObjectId);
		}

		return CreatePlan(
			RepurchasePlanStatus.PlanCreated,
			requestedItemObjectIds,
			repurchasedIds,
			missingIds,
			insufficientKinahIds,
			addedItems,
			updatedItems,
			kinahItem,
			kinahCount,
			removedRepurchaseIds,
			messages: Array.Empty<SmSystemMessage>(),
			auditMessages,
			"RepurchaseService.repurchaseFromShop -> for each requested object id, precheck inventory full -> find repurchase item -> tryDecreaseKinah(price) -> ItemService.addItem(player, repurchaseItem) -> remove from repurchase set");
	}

	private static RepurchasePlan Block(RepurchasePlanStatus status, IReadOnlyList<int> requestedItemObjectIds, string javaSource)
	{
		return new RepurchasePlan(
			status,
			requestedItemObjectIds,
			RepurchasedItemObjectIds: Array.Empty<int>(),
			MissingRepurchaseItemObjectIds: Array.Empty<int>(),
			InsufficientKinahItemObjectIds: Array.Empty<int>(),
			AddedItems: Array.Empty<InventoryItem>(),
			UpdatedItems: Array.Empty<InventoryItem>(),
			KinahUpdate: null,
			RemovedRepurchaseItemObjectIds: Array.Empty<int>(),
			Messages: Array.Empty<SmSystemMessage>(),
			AuditMessages: Array.Empty<string>(),
			javaSource);
	}

	private static RepurchasePlan CreatePlan(
		RepurchasePlanStatus status,
		IReadOnlyList<int> requestedItemObjectIds,
		IReadOnlyList<int> repurchasedIds,
		IReadOnlyList<int> missingIds,
		IReadOnlyList<int> insufficientKinahIds,
		IReadOnlyList<InventoryItem> addedItems,
		IReadOnlyList<InventoryItem> updatedItems,
		InventoryItem? kinahItem,
		long kinahCount,
		IReadOnlyList<int> removedRepurchaseIds,
		IReadOnlyList<SmSystemMessage> messages,
		IReadOnlyList<string> auditMessages,
		string javaSource)
	{
		return new RepurchasePlan(
			status,
			requestedItemObjectIds,
			repurchasedIds.ToArray(),
			missingIds.ToArray(),
			insufficientKinahIds.ToArray(),
			addedItems.ToArray(),
			updatedItems.ToArray(),
			kinahItem == null ? null : CopyInventoryItem(kinahItem, kinahCount),
			removedRepurchaseIds.ToArray(),
			messages,
			auditMessages.ToArray(),
			javaSource);
	}

	private static void ApplyAddPlan(List<InventoryItem> workingItems, InventoryAddPlan addPlan)
	{
		foreach (var updatedItem in addPlan.UpdatedItems)
		{
			var index = workingItems.FindIndex(item => item.ObjectId == updatedItem.ObjectId);
			if (index >= 0)
				workingItems[index] = updatedItem;
		}
		workingItems.AddRange(addPlan.AddedItems);
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			PersistentState = item.PersistentState,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}
}

public static class RepurchaseOutcomePlanService
{
	public static RepurchaseOutcomePlan CreateDisabledPlan(RepurchasePlan? repurchasePlan)
	{
		if (repurchasePlan == null)
			return CreateTerminalPlan(
				RepurchaseOutcomePlanStatus.MissingRepurchasePlan,
				repurchasePlan,
				"RepurchaseService.repurchaseFromShop final outcome requires a repurchase execution plan");

		var wouldMutatePlayerInventory = repurchasePlan.AddedItems.Count > 0 || repurchasePlan.UpdatedItems.Count > 0;
		var wouldRemoveRepurchaseItems = repurchasePlan.RemovedRepurchaseItemObjectIds.Count > 0;
		var wouldMutateKinah = repurchasePlan.RepurchasedItemObjectIds.Count > 0 && repurchasePlan.KinahUpdate != null;
		var wouldWritePersistence = wouldMutatePlayerInventory || wouldRemoveRepurchaseItems || wouldMutateKinah;
		var wouldSendPackets = wouldMutatePlayerInventory || wouldMutateKinah || repurchasePlan.Messages.Count > 0;
		var wouldWriteAuditLog = repurchasePlan.AuditMessages.Count > 0;
		var wouldCommitBoundary = wouldWritePersistence || wouldSendPackets || wouldWriteAuditLog;

		if (!wouldCommitBoundary)
			return CreateTerminalPlan(
				RepurchaseOutcomePlanStatus.RepurchasePlanNotReady,
				repurchasePlan,
				"RepurchaseService.repurchaseFromShop final outcome has no recorded mutation, packet, or audit side effect");

		var steps = new List<RepurchaseOutcomeStepPlan>();
		if (wouldWritePersistence)
			steps.Add(Disabled(
				RepurchaseOutcomeStepKind.PersistInventoryWrites,
				"RepurchaseService.repurchaseFromShop -> tryDecreaseKinah and ItemService.addItem persist player inventory/Kinah state"));
		if (wouldRemoveRepurchaseItems)
			steps.Add(Disabled(
				RepurchaseOutcomeStepKind.RemoveRepurchaseItems,
				"RepurchaseService.repurchaseFromShop -> items.remove(repurchaseItem) removes the item from the singleton repurchase set"));
		if (wouldSendPackets)
			steps.Add(Disabled(
				RepurchaseOutcomeStepKind.DispatchPacketIntents,
				"RepurchaseService.repurchaseFromShop -> inventory mutations and inventory-full guards emit packet intents"));
		if (wouldWriteAuditLog)
			steps.Add(Disabled(
				RepurchaseOutcomeStepKind.WriteAuditLog,
				"RepurchaseService.repurchaseFromShop -> insufficient Kinah branch writes AuditLogger output"));
		if (wouldCommitBoundary)
			steps.Add(Disabled(
				RepurchaseOutcomeStepKind.CommitSideEffectBoundary,
				"RepurchaseService.repurchaseFromShop side-effect boundary is recorded only; Java runtime ordering is not yet verified"));

		return new RepurchaseOutcomePlan(
			RepurchaseOutcomePlanStatus.DisabledNoTransaction,
			repurchasePlan,
			steps,
			wouldWritePersistence,
			DidWritePersistence: false,
			wouldMutatePlayerInventory,
			DidMutatePlayerInventory: false,
			wouldRemoveRepurchaseItems,
			DidRemoveRepurchaseItems: false,
			wouldMutateKinah,
			DidMutateKinah: false,
			wouldSendPackets,
			DidSendPackets: false,
			wouldWriteAuditLog,
			DidWriteAuditLog: false,
			wouldCommitBoundary,
			DidCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			"RepurchaseService.repurchaseFromShop final outcome is disabled; inventory/Kinah/repurchase-set writes, packets, and audits are recorded without dispatch",
			IsLive: false);
	}

	private static RepurchaseOutcomePlan CreateTerminalPlan(
		RepurchaseOutcomePlanStatus status,
		RepurchasePlan? repurchasePlan,
		string javaSource) =>
		new(
			status,
			repurchasePlan,
			Steps: Array.Empty<RepurchaseOutcomeStepPlan>(),
			WouldWritePersistence: false,
			DidWritePersistence: false,
			WouldMutatePlayerInventory: false,
			DidMutatePlayerInventory: false,
			WouldRemoveRepurchaseItems: false,
			DidRemoveRepurchaseItems: false,
			WouldMutateKinah: false,
			DidMutateKinah: false,
			WouldSendPackets: false,
			DidSendPackets: false,
			WouldWriteAuditLog: false,
			DidWriteAuditLog: false,
			WouldCommitTransactionBoundary: false,
			DidCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);

	private static RepurchaseOutcomeStepPlan Disabled(
		RepurchaseOutcomeStepKind kind,
		string javaSource) =>
		new(kind, WouldRun: true, DidRun: false, javaSource);
}
