namespace Aion.GameServer.Services;

public enum CmBuyItemSideEffectOutcomePlanStatus
{
	MissingHandlerPlan,
	HandlerNotOutcomeEligible,
	PrivateStoreOutcomeCreated,
	PetMerchantSellOutcomeCreated,
}

public sealed record CmBuyItemSideEffectOutcomePlan(
	CmBuyItemSideEffectOutcomePlanStatus Status,
	CmBuyItemHandlerCompositionPlan? HandlerPlan,
	PrivateStoreLiveExecutorFacadePlan? PrivateStoreFacadePlan,
	PrivateStorePurchaseOutcomePlan? PrivateStoreOutcomePlan,
	PetMerchantSellLiveExecutorFacadePlan? PetMerchantSellFacadePlan,
	PetMerchantSellOutcomePlan? PetMerchantSellOutcomePlan,
	bool WouldWritePersistence,
	bool WouldMutateSellerInventory,
	bool WouldMutateBuyerInventory,
	bool WouldMutateKinah,
	bool WouldAddRepurchaseItems,
	bool WouldSendPackets,
	bool WouldWriteExchangeLog,
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
			_ => CreateTerminalPlan(
				CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible,
				handlerPlan,
				"CM_BUY_ITEM side-effect outcome composition only covers Player action 0 and Pet MERCHANT action 17"),
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
			WouldWritePersistence: outcomePlan.WouldWritePersistence,
			WouldMutateSellerInventory: facadePlan.WouldMutateSellerInventory,
			WouldMutateBuyerInventory: facadePlan.WouldMutateBuyerInventory,
			WouldMutateKinah: outcomePlan.WouldWritePersistence && (facadePlan.WouldMutateBuyerKinah || facadePlan.WouldMutateSellerKinah),
			WouldAddRepurchaseItems: false,
			WouldSendPackets: outcomePlan.WouldSendPackets,
			WouldWriteExchangeLog: outcomePlan.WouldWriteExchangeLog,
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
			facadePlan,
			outcomePlan,
			WouldWritePersistence: outcomePlan.WouldWritePersistence,
			WouldMutateSellerInventory: outcomePlan.WouldMutateSellerInventory,
			WouldMutateBuyerInventory: false,
			WouldMutateKinah: outcomePlan.WouldMutateKinah,
			WouldAddRepurchaseItems: outcomePlan.WouldAddRepurchaseItems,
			WouldSendPackets: outcomePlan.WouldSendPackets,
			WouldWriteExchangeLog: false,
			WouldCommitTransactionBoundary: outcomePlan.WouldCommitTransactionBoundary,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			"CM_BUY_ITEM Pet MERCHANT action 17 disabled final outcome is composed from handler plan without dispatch",
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
			WouldWritePersistence: false,
			WouldMutateSellerInventory: false,
			WouldMutateBuyerInventory: false,
			WouldMutateKinah: false,
			WouldAddRepurchaseItems: false,
			WouldSendPackets: false,
			WouldWriteExchangeLog: false,
			WouldCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);
}
