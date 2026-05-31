namespace Aion.GameServer.Services;

public enum CmBuyItemSideEffectOutcomePlanStatus
{
	MissingHandlerPlan,
	HandlerNotOutcomeEligible,
	PrivateStoreOutcomeCreated,
	PetMerchantSellOutcomeCreated,
	BuyFromShopOutcomeCreated,
}

public sealed record CmBuyItemSideEffectOutcomePlan(
	CmBuyItemSideEffectOutcomePlanStatus Status,
	CmBuyItemHandlerCompositionPlan? HandlerPlan,
	PrivateStoreLiveExecutorFacadePlan? PrivateStoreFacadePlan,
	PrivateStorePurchaseOutcomePlan? PrivateStoreOutcomePlan,
	PetMerchantSellLiveExecutorFacadePlan? PetMerchantSellFacadePlan,
	PetMerchantSellOutcomePlan? PetMerchantSellOutcomePlan,
	TradeBuyTransactionOutcomePlan? BuyFromShopOutcomePlan,
	bool WouldWritePersistence,
	bool WouldMutateSellerInventory,
	bool WouldMutateBuyerInventory,
	bool WouldMutateKinah,
	bool WouldAddRepurchaseItems,
	bool WouldSendPackets,
	bool WouldWriteExchangeLog,
	bool WouldWriteAuditLog,
	bool WouldCommitTransactionBoundary,
	bool ShouldCommitTransactionBoundary,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public static class CmBuyItemSideEffectOutcomePlanService
{
	public static CmBuyItemSideEffectOutcomePlan CreateDisabledPlan(CmBuyItemHandlerCompositionPlan? handlerPlan)
	{
		if (handlerPlan == null)
			return CreateTerminalPlan(
				CmBuyItemSideEffectOutcomePlanStatus.MissingHandlerPlan,
				handlerPlan,
				"CM_BUY_ITEM side-effect outcome composition requires handler composition evidence");

		return handlerPlan.Status switch
		{
			CmBuyItemHandlerCompositionPlanStatus.SelectedPrivateStorePlanner => CreatePrivateStoreOutcomePlan(handlerPlan),
			CmBuyItemHandlerCompositionPlanStatus.SelectedPetSellToShopPlanner => CreatePetMerchantOutcomePlan(handlerPlan),
			CmBuyItemHandlerCompositionPlanStatus.SelectedBuyFromShopPlanner => CreateBuyFromShopOutcomePlan(handlerPlan),
			_ => CreateTerminalPlan(
				CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible,
				handlerPlan,
				"CM_BUY_ITEM side-effect outcome composition only covers Player action 0, Pet MERCHANT action 17, and Npc buy-from-shop actions 13-16"),
		};
	}

	private static CmBuyItemSideEffectOutcomePlan CreatePrivateStoreOutcomePlan(CmBuyItemHandlerCompositionPlan handlerPlan)
	{
		var facadePlan = PrivateStoreLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);
		var outcomePlan = PrivateStorePurchaseOutcomePlanService.CreateDisabledPlan(facadePlan);

		return new CmBuyItemSideEffectOutcomePlan(
			CmBuyItemSideEffectOutcomePlanStatus.PrivateStoreOutcomeCreated,
			handlerPlan,
			facadePlan,
			outcomePlan,
			PetMerchantSellFacadePlan: null,
			PetMerchantSellOutcomePlan: null,
			BuyFromShopOutcomePlan: null,
			WouldWritePersistence: outcomePlan.WouldWritePersistence,
			WouldMutateSellerInventory: facadePlan.WouldMutateSellerInventory,
			WouldMutateBuyerInventory: facadePlan.WouldMutateBuyerInventory,
			WouldMutateKinah: outcomePlan.WouldWritePersistence && (facadePlan.WouldMutateBuyerKinah || facadePlan.WouldMutateSellerKinah),
			WouldAddRepurchaseItems: false,
			WouldSendPackets: outcomePlan.WouldSendPackets,
			WouldWriteExchangeLog: outcomePlan.WouldWriteExchangeLog,
			WouldWriteAuditLog: false,
			WouldCommitTransactionBoundary: outcomePlan.WouldCommitTransactionBoundary,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			"CM_BUY_ITEM Player action 0 disabled final outcome is composed from handler plan without dispatch",
			IsLive: false);
	}

	private static CmBuyItemSideEffectOutcomePlan CreatePetMerchantOutcomePlan(CmBuyItemHandlerCompositionPlan handlerPlan)
	{
		var facadePlan = PetMerchantSellLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);
		var outcomePlan = PetMerchantSellOutcomePlanService.CreateDisabledPlan(facadePlan);

		return new CmBuyItemSideEffectOutcomePlan(
			CmBuyItemSideEffectOutcomePlanStatus.PetMerchantSellOutcomeCreated,
			handlerPlan,
			PrivateStoreFacadePlan: null,
			PrivateStoreOutcomePlan: null,
			PetMerchantSellFacadePlan: facadePlan,
			PetMerchantSellOutcomePlan: outcomePlan,
			BuyFromShopOutcomePlan: null,
			WouldWritePersistence: outcomePlan.WouldWritePersistence,
			WouldMutateSellerInventory: outcomePlan.WouldMutateSellerInventory,
			WouldMutateBuyerInventory: false,
			WouldMutateKinah: outcomePlan.WouldMutateKinah,
			WouldAddRepurchaseItems: outcomePlan.WouldAddRepurchaseItems,
			WouldSendPackets: outcomePlan.WouldSendPackets,
			WouldWriteExchangeLog: false,
			WouldWriteAuditLog: false,
			WouldCommitTransactionBoundary: outcomePlan.WouldCommitTransactionBoundary,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			"CM_BUY_ITEM Pet MERCHANT action 17 disabled final outcome is composed from handler plan without dispatch",
			IsLive: false);
	}

	private static CmBuyItemSideEffectOutcomePlan CreateBuyFromShopOutcomePlan(CmBuyItemHandlerCompositionPlan handlerPlan)
	{
		var transactionPlan = handlerPlan.BuyFromShopPlan?.Dispatch?.BuyTransactionPlan;
		var outcomePlan = TradeBuyTransactionOutcomePlanService.CreateDisabledPlan(transactionPlan);

		return new CmBuyItemSideEffectOutcomePlan(
			CmBuyItemSideEffectOutcomePlanStatus.BuyFromShopOutcomeCreated,
			handlerPlan,
			PrivateStoreFacadePlan: null,
			PrivateStoreOutcomePlan: null,
			PetMerchantSellFacadePlan: null,
			PetMerchantSellOutcomePlan: null,
			BuyFromShopOutcomePlan: outcomePlan,
			WouldWritePersistence: outcomePlan.WouldWritePersistence,
			WouldMutateSellerInventory: false,
			WouldMutateBuyerInventory: outcomePlan.WouldWritePersistence && transactionPlan?.Mutation?.AddedItems.Count > 0,
			WouldMutateKinah: outcomePlan.WouldWritePersistence && transactionPlan?.RequiredKinah > 0,
			WouldAddRepurchaseItems: false,
			WouldSendPackets: outcomePlan.WouldSendPackets,
			WouldWriteExchangeLog: false,
			WouldWriteAuditLog: outcomePlan.WouldWriteAuditLog,
			WouldCommitTransactionBoundary: outcomePlan.WouldCommitTransactionBoundary,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			"CM_BUY_ITEM Npc actions 13-16 disabled buy-from-shop final outcome is composed from handler plan without dispatch",
			IsLive: false);
	}

	private static CmBuyItemSideEffectOutcomePlan CreateTerminalPlan(
		CmBuyItemSideEffectOutcomePlanStatus status,
		CmBuyItemHandlerCompositionPlan? handlerPlan,
		string javaSource) =>
		new(
			status,
			handlerPlan,
			PrivateStoreFacadePlan: null,
			PrivateStoreOutcomePlan: null,
			PetMerchantSellFacadePlan: null,
			PetMerchantSellOutcomePlan: null,
			BuyFromShopOutcomePlan: null,
			WouldWritePersistence: false,
			WouldMutateSellerInventory: false,
			WouldMutateBuyerInventory: false,
			WouldMutateKinah: false,
			WouldAddRepurchaseItems: false,
			WouldSendPackets: false,
			WouldWriteExchangeLog: false,
			WouldWriteAuditLog: false,
			WouldCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);
}
