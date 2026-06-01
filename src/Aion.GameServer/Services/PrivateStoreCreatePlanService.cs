using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PrivateStoreCreatePlanStatus
{
	ClosePlanCreated,
	OpenGuardBlocked,
	ItemValidationBlocked,
	DisabledNoSideEffects,
}

public enum PrivateStoreCreateStepKind
{
	ClosePrivateStore,
	CheckOpenGuard,
	CreatePrivateStore,
	ValidateItem,
	AddItemToStore,
	SetStore,
	SetPrivateShopState,
	BroadcastOpenPrivateShopEmotion,
}

public sealed record PrivateStoreCreatePlayerContext(
	int PlayerObjectId,
	int CreatureState,
	bool StoreIsOpen,
	bool IsFlying,
	bool IsInMove,
	bool IsInCombatMode,
	bool IsTrading,
	bool IsInRideOrRobotMode,
	bool IsHidden,
	bool IsDead,
	bool IsInChairState);

public sealed record PrivateStoreCreateItemContext(
	int ItemObjectId,
	int ItemId,
	long AvailableCount,
	bool ItemExistsAndIdMatches,
	bool ItemIsPackCountAboveZeroOrTradeable,
	bool ItemIsEquipped);

public sealed record PrivateStoreCreateStepPlan(
	PrivateStoreCreateStepKind Kind,
	string JavaSource)
{
	public bool IsLive => false;
}

public sealed record PrivateStoreCreateStoredItemPlan(
	int ItemObjectId,
	int ItemId,
	long Count,
	long Price,
	string JavaSource)
{
	public bool IsLive => false;
}

public sealed record PrivateStoreCreateItemValidationStepPlan(
	CmPrivateStoreEntry RequestedItem,
	PrivateStoreCreateItemContext? ItemContext,
	PrivateStoreItemValidationPlan ValidationPlan,
	string JavaSource)
{
	public bool IsLive => false;
}

public sealed record PrivateStoreCreatePlan(
	PrivateStoreCreatePlanStatus Status,
	CmPrivateStore Packet,
	PrivateStoreCreatePlayerContext Context,
	PrivateStoreClosePlan? ClosePlan,
	PrivateStoreOpenGuardPlan? OpenGuardPlan,
	IReadOnlyList<PrivateStoreCreateItemValidationStepPlan> ItemValidationSteps,
	IReadOnlyList<PrivateStoreCreateStoredItemPlan> StoredItemIntents,
	IReadOnlyList<PrivateStoreCreateStepPlan> Steps,
	SmEmotion? OpenPrivateShopEmotionPacket,
	bool WouldSetStore,
	bool WouldSetPrivateShopState,
	bool WouldBroadcastOpenEmotion,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class PrivateStoreCreatePlanService
{
	public static PrivateStoreCreatePlan CreateDisabledPlan(
		CmPrivateStore packet,
		PrivateStoreCreatePlayerContext context,
		IReadOnlyDictionary<int, PrivateStoreCreateItemContext> itemContextsByObjectId)
	{
		// Java parity: network/aion/clientpackets/CM_PRIVATE_STORE.runImpl.
		// Zero listed items call PrivateStoreService.closePrivateStore(player) before canOpenPrivateStore.
		if (packet.Items.Count <= 0)
		{
			var closePlan = PrivateStoreClosePlanService.CreatePlan(
				context.PlayerObjectId,
				context.CreatureState,
				context.StoreIsOpen);

			return new PrivateStoreCreatePlan(
				PrivateStoreCreatePlanStatus.ClosePlanCreated,
				packet,
				context,
				closePlan,
				OpenGuardPlan: null,
				ItemValidationSteps: Array.Empty<PrivateStoreCreateItemValidationStepPlan>(),
				StoredItemIntents: Array.Empty<PrivateStoreCreateStoredItemPlan>(),
				Steps: [Disabled(PrivateStoreCreateStepKind.ClosePrivateStore, "CM_PRIVATE_STORE.runImpl -> tradePSItems.length <= 0 -> PrivateStoreService.closePrivateStore(player)")],
				OpenPrivateShopEmotionPacket: null,
				WouldSetStore: false,
				WouldSetPrivateShopState: false,
				WouldBroadcastOpenEmotion: false,
				"CM_PRIVATE_STORE.runImpl -> tradePSItems.length <= 0 -> PrivateStoreService.closePrivateStore(player); close-store branch is disabled and recorded without dispatch");
		}

		var openGuard = PrivateStoreOpenGuardPlanService.CreatePlan(
			context.IsFlying,
			context.IsInMove,
			context.IsInCombatMode,
			context.IsTrading,
			context.IsInRideOrRobotMode,
			context.IsHidden,
			context.IsDead,
			context.IsInChairState,
			context.StoreIsOpen);

		if (!openGuard.CanOpen)
		{
			return new PrivateStoreCreatePlan(
				PrivateStoreCreatePlanStatus.OpenGuardBlocked,
				packet,
				context,
				ClosePlan: null,
				openGuard,
				ItemValidationSteps: Array.Empty<PrivateStoreCreateItemValidationStepPlan>(),
				StoredItemIntents: Array.Empty<PrivateStoreCreateStoredItemPlan>(),
				Steps: [Disabled(PrivateStoreCreateStepKind.CheckOpenGuard, "PrivateStoreService.createStoreWithItems -> if (!canOpenPrivateStore(player)) return")],
				OpenPrivateShopEmotionPacket: null,
				WouldSetStore: false,
				WouldSetPrivateShopState: false,
				WouldBroadcastOpenEmotion: false,
				"PrivateStoreService.createStoreWithItems blocked by canOpenPrivateStore; live denial packet dispatch remains deferred");
		}

		var validationSteps = new List<PrivateStoreCreateItemValidationStepPlan>();
		var storedItems = new List<PrivateStoreCreateStoredItemPlan>();
		var registeredObjectIds = new HashSet<int>();

		foreach (var requestedItem in packet.Items)
		{
			itemContextsByObjectId.TryGetValue(requestedItem.ItemObjectId, out var itemContext);
			var itemExistsAndIdMatches = itemContext?.ItemExistsAndIdMatches == true && itemContext.ItemId == requestedItem.ItemId;
			var itemAlreadyRegistered = registeredObjectIds.Contains(requestedItem.ItemObjectId);
			var validation = PrivateStoreItemValidationPlanService.CreatePlan(
				itemExistsAndIdMatches,
				requestedItem.Count,
				itemContext?.AvailableCount ?? 0,
				requestedItem.Price,
				storedItems.Count,
				itemContext?.ItemIsPackCountAboveZeroOrTradeable == true,
				itemContext?.ItemIsEquipped == true,
				itemAlreadyRegistered);

			validationSteps.Add(new PrivateStoreCreateItemValidationStepPlan(
				requestedItem,
				itemContext,
				validation,
				"PrivateStoreService.createStoreWithItems -> validateItem(store, item, tradePSItem)"));

			if (!validation.IsValid)
			{
				return new PrivateStoreCreatePlan(
					PrivateStoreCreatePlanStatus.ItemValidationBlocked,
					packet,
					context,
					ClosePlan: null,
					openGuard,
					validationSteps,
					storedItems,
					Steps:
					[
						Disabled(PrivateStoreCreateStepKind.CheckOpenGuard, "PrivateStoreService.createStoreWithItems -> canOpenPrivateStore passed"),
						Disabled(PrivateStoreCreateStepKind.CreatePrivateStore, "PrivateStoreService.createStoreWithItems -> new PrivateStore(player)"),
						Disabled(PrivateStoreCreateStepKind.ValidateItem, "PrivateStoreService.createStoreWithItems -> invalid item returns before setStore/state/broadcast")
					],
					OpenPrivateShopEmotionPacket: null,
					WouldSetStore: false,
					WouldSetPrivateShopState: false,
					WouldBroadcastOpenEmotion: false,
					"PrivateStoreService.createStoreWithItems item validation blocked; Java returns before store assignment and OPEN_PRIVATESHOP broadcast");
			}

			registeredObjectIds.Add(requestedItem.ItemObjectId);
			storedItems.Add(new PrivateStoreCreateStoredItemPlan(
				requestedItem.ItemObjectId,
				requestedItem.ItemId,
				requestedItem.Count,
				requestedItem.Price,
				"PrivateStoreService.createStoreWithItems -> store.addItemToSell(tradePSItem.getItemObjId(), tradePSItem)"));
		}

		var openEmotion = new SmEmotion(
			context.PlayerObjectId,
			EmotionType.OpenPrivateShop,
			context.CreatureState,
			movementSpeed: 0,
			baseAttackSpeed: 0,
			currentAttackSpeed: 0);

		return new PrivateStoreCreatePlan(
			PrivateStoreCreatePlanStatus.DisabledNoSideEffects,
			packet,
			context,
			ClosePlan: null,
			openGuard,
			validationSteps,
			storedItems,
			Steps:
			[
				Disabled(PrivateStoreCreateStepKind.CheckOpenGuard, "PrivateStoreService.createStoreWithItems -> canOpenPrivateStore passed"),
				Disabled(PrivateStoreCreateStepKind.CreatePrivateStore, "PrivateStoreService.createStoreWithItems -> new PrivateStore(player)"),
				Disabled(PrivateStoreCreateStepKind.ValidateItem, "PrivateStoreService.createStoreWithItems -> validate every requested item in read order"),
				Disabled(PrivateStoreCreateStepKind.AddItemToStore, "PrivateStoreService.createStoreWithItems -> store.addItemToSell for every valid item"),
				Disabled(PrivateStoreCreateStepKind.SetStore, "PrivateStoreService.createStoreWithItems -> player.setStore(store)"),
				Disabled(PrivateStoreCreateStepKind.SetPrivateShopState, "PrivateStoreService.createStoreWithItems -> player.setState(PRIVATE_SHOP, true)"),
				Disabled(PrivateStoreCreateStepKind.BroadcastOpenPrivateShopEmotion, "PrivateStoreService.createStoreWithItems -> broadcast SM_EMOTION(OPEN_PRIVATESHOP, true)")
			],
			openEmotion,
			WouldSetStore: true,
			WouldSetPrivateShopState: true,
			WouldBroadcastOpenEmotion: true,
			"PrivateStoreService.createStoreWithItems create-store boundary is disabled; store assignment, state mutation, and broadcast intents are recorded without dispatch");
	}

	private static PrivateStoreCreateStepPlan Disabled(PrivateStoreCreateStepKind kind, string javaSource) =>
		new(kind, javaSource);
}
