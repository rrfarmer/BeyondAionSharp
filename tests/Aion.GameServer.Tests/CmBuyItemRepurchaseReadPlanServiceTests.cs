using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemRepurchaseReadPlanServiceTests
{
	[Fact]
	public void CreatePlan_FiltersRepurchasableItemsInFirstSeenOrderAndDeduplicates()
	{
		var plan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			sellerObjectId: 7001,
			tradeActionId: CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
			declaredAmount: 4,
			readItems:
			[
				new CmBuyItemReadItem(101, Count: 1),
				new CmBuyItemReadItem(999, Count: 1),
				new CmBuyItemReadItem(102, Count: 5),
				new CmBuyItemReadItem(101, Count: 7),
			],
			repurchasableItemObjectIds: new HashSet<int> { 102, 101 });

		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.IsRepurchaseAction);
		Assert.Equal(7001, plan.SellerObjectId);
		Assert.Equal([101, 102], plan.RepurchaseItemObjectIds);
		Assert.Equal([101, 999, 102, 101], plan.ProcessedItems.Select(item => item.ItemObjectId).ToArray());
		Assert.Null(plan.AuditItem);
	}

	[Fact]
	public void CreatePlan_DeclaredAmountLimitsProcessedItems()
	{
		var plan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			sellerObjectId: 7001,
			tradeActionId: CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
			declaredAmount: 1,
			readItems:
			[
				new CmBuyItemReadItem(101, Count: 1),
				new CmBuyItemReadItem(102, Count: 1),
			],
			repurchasableItemObjectIds: new HashSet<int> { 101, 102 });

		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.PlanCreated, plan.Status);
		Assert.Equal([101], plan.RepurchaseItemObjectIds);
		Assert.Equal([101], plan.ProcessedItems.Select(item => item.ItemObjectId).ToArray());
	}

	[Fact]
	public void CreatePlan_AmountAboveJavaMaximumAuditsBeforeItems()
	{
		var plan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			sellerObjectId: 7001,
			tradeActionId: CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
			declaredAmount: 37,
			readItems: [new CmBuyItemReadItem(101, Count: 1)],
			repurchasableItemObjectIds: new HashSet<int> { 101 });

		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.AuditAmountOutOfRange, plan.Status);
		Assert.Empty(plan.ProcessedItems);
		Assert.Empty(plan.RepurchaseItemObjectIds);
		Assert.Null(plan.AuditItem);
	}

	[Fact]
	public void CreatePlan_NegativeAmountAuditsConservatively()
	{
		var plan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			sellerObjectId: 7001,
			tradeActionId: CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
			declaredAmount: -1,
			readItems: [],
			repurchasableItemObjectIds: new HashSet<int>());

		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.AuditAmountOutOfRange, plan.Status);
		Assert.Empty(plan.ProcessedItems);
	}

	[Fact]
	public void CreatePlan_NegativeCountAuditsAndSuppressesRepurchaseList()
	{
		var plan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			sellerObjectId: 7001,
			tradeActionId: CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
			declaredAmount: 2,
			readItems:
			[
				new CmBuyItemReadItem(101, Count: 1),
				new CmBuyItemReadItem(102, Count: -1),
			],
			repurchasableItemObjectIds: new HashSet<int> { 101, 102 });

		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.AuditInvalidItem, plan.Status);
		Assert.Equal(new CmBuyItemReadItem(102, Count: -1), plan.AuditItem);
		Assert.Equal([101, 102], plan.ProcessedItems.Select(item => item.ItemObjectId).ToArray());
		Assert.Empty(plan.RepurchaseItemObjectIds);
	}

	[Fact]
	public void CreatePlan_NonPositiveItemObjectIdAuditsForRepurchaseAction()
	{
		var plan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			sellerObjectId: 7001,
			tradeActionId: CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
			declaredAmount: 1,
			readItems: [new CmBuyItemReadItem(0, Count: 1)],
			repurchasableItemObjectIds: new HashSet<int>());

		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.AuditInvalidItem, plan.Status);
		Assert.Equal(new CmBuyItemReadItem(0, Count: 1), plan.AuditItem);
		Assert.Empty(plan.RepurchaseItemObjectIds);
	}

	[Fact]
	public void CreatePlan_CountAboveJavaMaximumAudits()
	{
		var plan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			sellerObjectId: 7001,
			tradeActionId: CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
			declaredAmount: 1,
			readItems: [new CmBuyItemReadItem(101, Count: 20_001)],
			repurchasableItemObjectIds: new HashSet<int> { 101 });

		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.AuditInvalidItem, plan.Status);
		Assert.Equal(new CmBuyItemReadItem(101, Count: 20_001), plan.AuditItem);
		Assert.Empty(plan.RepurchaseItemObjectIds);
	}

	[Fact]
	public void CreatePlan_NonRepurchaseActionAppliesReadGuardsWithoutRepurchaseFiltering()
	{
		var plan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			sellerObjectId: 7001,
			tradeActionId: 1,
			declaredAmount: 1,
			readItems: [new CmBuyItemReadItem(101, Count: 1)],
			repurchasableItemObjectIds: new HashSet<int> { 101 });

		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsRepurchaseAction);
		Assert.Empty(plan.RepurchaseItemObjectIds);
		Assert.Equal([101], plan.ProcessedItems.Select(item => item.ItemObjectId).ToArray());
	}

	[Fact]
	public void CreatePlan_PrivateStoreActionAllowsNonPositiveItemIndexLikeJavaGuard()
	{
		var plan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			sellerObjectId: 7001,
			tradeActionId: 0,
			declaredAmount: 1,
			readItems: [new CmBuyItemReadItem(0, Count: 1)],
			repurchasableItemObjectIds: new HashSet<int>());

		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.PlanCreated, plan.Status);
		Assert.Equal([0], plan.ProcessedItems.Select(item => item.ItemObjectId).ToArray());
	}
}
