using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TradeBuyTransactionPlanServiceTests
{
	[Fact]
	public void CreatePlan_PlansJavaCostSubtractionAndItemAddsWithoutLiveMutation()
	{
		var plan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems:
				[
					new TradeBuyTransactionItemRequest(1001, 2, UnitBuyPrice: 500, RequiredApPerItem: 1_000, AcquisitionType: "AP", RequiredItemId: 186000001, RequiredItemCountPerItem: 3),
					new TradeBuyTransactionItemRequest(1002, 1, UnitBuyPrice: 900, RequiredApPerItem: 500, AcquisitionType: "ABYSS", RequiredItemId: 186000001, RequiredItemCountPerItem: 1),
				],
				TradeTemplate: NormalTemplate,
				UseKinah: true,
				PlayerCanTrade: true,
				AvailableKinah: 10_000,
				CurrentAbyssPoints: 5_000,
				FreeSlots: 2,
				AvailableRequiredItems: new Dictionary<int, long> { [186000001] = 7 },
				VendorBuyModifier: 100));

		Assert.Equal(TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal(1_900, plan.RequiredKinah);
		Assert.Equal(2_500, plan.RequiredAbyssPoints);
		Assert.Equal(new TradeBuyTransactionRequiredItem(186000001, 7), Assert.Single(plan.RequiredItems));
		Assert.Contains(TradeBuyTransactionStep.PlanCostSubtraction, plan.Steps);

		var mutation = Assert.IsType<TradeBuyTransactionMutationDescriptor>(plan.Mutation);
		Assert.False(mutation.IsLive);
		Assert.Equal([1001, 1002], mutation.AddedItems.Select(item => item.ItemId).ToArray());
	}

	[Fact]
	public void CreateDisabledPersistenceAdapter_RecordsSuccessfulBuyTransactionWritesWithoutWriting()
	{
		var plan = CreateSuccessfulMutationPlan();

		var adapter = TradeBuyTransactionPersistenceAdapterPlanService.CreateDisabledPlan(plan);

		Assert.Equal(TradeBuyTransactionPersistenceAdapterStatus.DisabledNoWrites, adapter.Status);
		Assert.Same(plan, adapter.TransactionPlan);
		Assert.True(adapter.WouldWriteRepository);
		Assert.False(adapter.DidWriteRepository);
		Assert.False(adapter.ShouldDispatchLiveSideEffects);
		Assert.False(adapter.IsLive);
		Assert.Collection(
			adapter.Operations.Select(operation => operation.Kind),
			kind => Assert.Equal(TradeBuyTransactionPersistenceOperationKind.SaveAbyssPoints, kind),
			kind => Assert.Equal(TradeBuyTransactionPersistenceOperationKind.SaveKinah, kind),
			kind => Assert.Equal(TradeBuyTransactionPersistenceOperationKind.DeleteRequiredItem, kind),
			kind => Assert.Equal(TradeBuyTransactionPersistenceOperationKind.SaveAddedItem, kind),
			kind => Assert.Equal(TradeBuyTransactionPersistenceOperationKind.SaveAddedItem, kind),
			kind => Assert.Equal(TradeBuyTransactionPersistenceOperationKind.UpdateLimitedItemCounter, kind),
			kind => Assert.Equal(TradeBuyTransactionPersistenceOperationKind.UpdateLimitedItemCounter, kind));
		Assert.All(adapter.Operations, operation =>
		{
			Assert.True(operation.WouldWrite);
			Assert.False(operation.DidWrite);
		});
	}

	[Fact]
	public void CreateDisabledSendAdapter_RecordsSuccessfulBuyTransactionPacketIntentsWithoutSending()
	{
		var plan = CreateSuccessfulMutationPlan();

		var adapter = TradeBuyTransactionSendAdapterPlanService.CreateDisabledPlan(plan);

		Assert.Equal(TradeBuyTransactionSendAdapterStatus.DisabledNoPackets, adapter.Status);
		Assert.Same(plan, adapter.TransactionPlan);
		Assert.True(adapter.WouldSendPackets);
		Assert.False(adapter.DidSendPackets);
		Assert.False(adapter.WouldWriteAuditLog);
		Assert.False(adapter.DidWriteAuditLog);
		Assert.False(adapter.ShouldDispatchLiveSideEffects);
		Assert.False(adapter.IsLive);
		Assert.Collection(
			adapter.Intents.Select(intent => intent.Kind),
			kind => Assert.Equal(TradeBuyTransactionSendIntentKind.SendAbyssPointsUpdate, kind),
			kind => Assert.Equal(TradeBuyTransactionSendIntentKind.SendKinahUpdate, kind),
			kind => Assert.Equal(TradeBuyTransactionSendIntentKind.SendRequiredItemDelete, kind),
			kind => Assert.Equal(TradeBuyTransactionSendIntentKind.SendBoughtItemAdd, kind),
			kind => Assert.Equal(TradeBuyTransactionSendIntentKind.SendBoughtItemAdd, kind));
		Assert.All(adapter.Intents, intent =>
		{
			Assert.True(intent.WouldSend);
			Assert.False(intent.DidSend);
		});
	}

	[Fact]
	public void CreateDisabledOutcomePlan_GroupsSuccessfulBuyTransactionWithoutCommitting()
	{
		var plan = CreateSuccessfulMutationPlan();

		var outcome = TradeBuyTransactionOutcomePlanService.CreateDisabledPlan(plan);

		Assert.Equal(TradeBuyTransactionOutcomePlanStatus.DisabledNoTransaction, outcome.Status);
		Assert.Same(plan, outcome.TransactionPlan);
		Assert.NotNull(outcome.PersistenceAdapterPlan);
		Assert.NotNull(outcome.SendAdapterPlan);
		Assert.True(outcome.WouldWritePersistence);
		Assert.False(outcome.DidWritePersistence);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.DidSendPackets);
		Assert.False(outcome.WouldWriteAuditLog);
		Assert.False(outcome.DidWriteAuditLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.DidCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
		Assert.Collection(
			outcome.Steps.Select(step => step.Kind),
			kind => Assert.Equal(TradeBuyTransactionOutcomeStepKind.PersistRepositoryWrites, kind),
			kind => Assert.Equal(TradeBuyTransactionOutcomeStepKind.DispatchPacketAndAuditIntents, kind),
			kind => Assert.Equal(TradeBuyTransactionOutcomeStepKind.CommitTransactionBoundary, kind));
		Assert.All(outcome.Steps, step =>
		{
			Assert.True(step.WouldRun);
			Assert.False(step.DidRun);
		});
	}

	[Fact]
	public void CreateDisabledSendAdapter_RecordsBlockedBuyTransactionMessagesAndAuditWithoutPersistence()
	{
		var invalidPlan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1001, 0, UnitBuyPrice: 1_000)],
				TradeTemplate: NormalTemplate,
				UseKinah: true,
				PlayerCanTrade: true,
				AvailableKinah: 10_000,
				CurrentAbyssPoints: 0,
				FreeSlots: 1));
		var auditPlan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1002, 1, UnitBuyPrice: 0, RequiredApPerItem: -100, AcquisitionType: "AP")],
				TradeTemplate: NormalTemplate,
				UseKinah: false,
				PlayerCanTrade: true,
				AvailableKinah: 0,
				CurrentAbyssPoints: 0,
				FreeSlots: 1));

		var invalidPersistence = TradeBuyTransactionPersistenceAdapterPlanService.CreateDisabledPlan(invalidPlan);
		var invalidSend = TradeBuyTransactionSendAdapterPlanService.CreateDisabledPlan(invalidPlan);
		var auditOutcome = TradeBuyTransactionOutcomePlanService.CreateDisabledPlan(auditPlan);

		Assert.Equal(TradeBuyTransactionPersistenceAdapterStatus.TransactionPlanNotReady, invalidPersistence.Status);
		Assert.False(invalidPersistence.WouldWriteRepository);
		Assert.Equal(TradeBuyTransactionSendIntentKind.SendInvalidBuyItemMessage, Assert.Single(invalidSend.Intents).Kind);
		Assert.True(invalidSend.WouldSendPackets);
		Assert.False(invalidSend.WouldWriteAuditLog);
		Assert.True(auditOutcome.WouldSendPackets);
		Assert.True(auditOutcome.WouldWriteAuditLog);
		Assert.False(auditOutcome.WouldWritePersistence);
		Assert.True(auditOutcome.WouldCommitTransactionBoundary);
	}

	[Fact]
	public void CreateDisabledOutcomePlan_MissingTransactionPlanStopsBeforeAdapters()
	{
		var outcome = TradeBuyTransactionOutcomePlanService.CreateDisabledPlan(null);

		Assert.Equal(TradeBuyTransactionOutcomePlanStatus.MissingTransactionPlan, outcome.Status);
		Assert.Null(outcome.TransactionPlan);
		Assert.Null(outcome.PersistenceAdapterPlan);
		Assert.Null(outcome.SendAdapterPlan);
		Assert.Empty(outcome.Steps);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldWriteAuditLog);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreatePlan_UsesAbyssKinahSecondaryRatesLikeJavaCaller()
	{
		var plan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1001, 1, UnitBuyPrice: 1_000, RequiredApPerItem: 10_000, AcquisitionType: "AP")],
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "ABYSS_KINAH", SellPriceRate2: 75, ApSellPriceRate2: 80),
				UseKinah: true,
				PlayerCanTrade: true,
				AvailableKinah: 1_000,
				CurrentAbyssPoints: 10_000,
				FreeSlots: 1,
				VendorBuyModifier: 100));

		Assert.Equal(TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction, plan.Status);
		Assert.Equal(750, plan.RequiredKinah);
		Assert.Equal(8_000, plan.RequiredAbyssPoints);
	}

	[Fact]
	public void CreatePlan_SkipsKinahCalculationWhenJavaUseKinahIsFalse()
	{
		var plan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1001, 1, UnitBuyPrice: 1_000)],
				TradeTemplate: NormalTemplate,
				UseKinah: false,
				PlayerCanTrade: true,
				AvailableKinah: 0,
				CurrentAbyssPoints: 0,
				FreeSlots: 1));

		Assert.Equal(TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction, plan.Status);
		Assert.Equal(0, plan.RequiredKinah);
		Assert.DoesNotContain(TradeBuyTransactionStep.CalculateKinahPrice, plan.Steps);
	}

	[Fact]
	public void CreatePlan_BlocksCannotTradeBeforeValidation()
	{
		var plan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1001, 0, UnitBuyPrice: 1_000, IsAllowedByNpcGoodsList: false)],
				TradeTemplate: NormalTemplate,
				UseKinah: true,
				PlayerCanTrade: false,
				AvailableKinah: 0,
				CurrentAbyssPoints: 0,
				FreeSlots: 0));

		Assert.Equal(TradeBuyTransactionPlanStatus.BlockedCannotTrade, plan.Status);
		Assert.DoesNotContain(TradeBuyTransactionStep.ValidateBuyItems, plan.Steps);
		Assert.Null(plan.RejectedItem);
	}

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, false)]
	public void CreatePlan_BlocksInvalidBuyItemsBeforeInventorySnapshot(long count, bool isAllowed)
	{
		var plan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1001, count, UnitBuyPrice: 1_000, IsAllowedByNpcGoodsList: isAllowed)],
				TradeTemplate: NormalTemplate,
				UseKinah: true,
				PlayerCanTrade: true,
				AvailableKinah: 10_000,
				CurrentAbyssPoints: 0,
				FreeSlots: 1));

		Assert.Equal(TradeBuyTransactionPlanStatus.BlockedInvalidBuyItem, plan.Status);
		Assert.Equal(1001, plan.RejectedItem!.ItemId);
		Assert.DoesNotContain(TradeBuyTransactionStep.SnapshotInventoryFreeSlots, plan.Steps);
	}

	[Fact]
	public void CreatePlan_BlocksNotEnoughKinahBeforeAbyssRewardCalculation()
	{
		var plan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1001, 2, UnitBuyPrice: 500, RequiredApPerItem: 1_000, AcquisitionType: "AP")],
				TradeTemplate: NormalTemplate,
				UseKinah: true,
				PlayerCanTrade: true,
				AvailableKinah: 999,
				CurrentAbyssPoints: 10_000,
				FreeSlots: 1));

		Assert.Equal(TradeBuyTransactionPlanStatus.BlockedNotEnoughKinah, plan.Status);
		Assert.Equal(1_000, plan.RequiredKinah);
		Assert.DoesNotContain(TradeBuyTransactionStep.CalculateAbyssRewardRequirements, plan.Steps);
	}

	[Fact]
	public void CreatePlan_BlocksNotEnoughApOrRequiredItemsDuringAbyssRewardCalculation()
	{
		var apPlan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1001, 1, UnitBuyPrice: 0, RequiredApPerItem: 1_000, AcquisitionType: "AP")],
				TradeTemplate: NormalTemplate,
				UseKinah: false,
				PlayerCanTrade: true,
				AvailableKinah: 0,
				CurrentAbyssPoints: 999,
				FreeSlots: 1));
		var itemPlan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1002, 1, UnitBuyPrice: 0, RequiredItemId: 186000001, RequiredItemCountPerItem: 2)],
				TradeTemplate: NormalTemplate,
				UseKinah: false,
				PlayerCanTrade: true,
				AvailableKinah: 0,
				CurrentAbyssPoints: 0,
				FreeSlots: 1,
				AvailableRequiredItems: new Dictionary<int, long> { [186000001] = 1 }));

		Assert.Equal(TradeBuyTransactionPlanStatus.BlockedNotEnoughAbyssPoints, apPlan.Status);
		Assert.Equal(TradeBuyTransactionPlanStatus.BlockedNotEnoughRequiredItems, itemPlan.Status);
		Assert.Equal(new TradeBuyTransactionRequiredItem(186000001, 2), itemPlan.MissingRequiredItem);
	}

	[Fact]
	public void CreatePlan_AuditsNegativeRequiredApAfterAbyssRewardCalculation()
	{
		var plan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1001, 1, UnitBuyPrice: 0, RequiredApPerItem: -100, AcquisitionType: "AP")],
				TradeTemplate: NormalTemplate,
				UseKinah: false,
				PlayerCanTrade: true,
				AvailableKinah: 0,
				CurrentAbyssPoints: 0,
				FreeSlots: 1));

		Assert.Equal(TradeBuyTransactionPlanStatus.AuditNegativeRequiredAp, plan.Status);
		Assert.Equal(-100, plan.RequiredAbyssPoints);
		Assert.Equal("possibly used packet hack: tradeList.getRequiredAp() < 0", plan.AuditReason);
		Assert.Contains(TradeBuyTransactionStep.CheckRequiredApExploit, plan.Steps);
	}

	[Fact]
	public void CreatePlan_ChecksFreeSlotsBeforeLimitedItems()
	{
		var fullPlan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems:
				[
					new TradeBuyTransactionItemRequest(1001, 1, UnitBuyPrice: 0, LimitedItemCanBuy: false),
					new TradeBuyTransactionItemRequest(1002, 1, UnitBuyPrice: 0),
				],
				TradeTemplate: NormalTemplate,
				UseKinah: false,
				PlayerCanTrade: true,
				AvailableKinah: 0,
				CurrentAbyssPoints: 0,
				FreeSlots: 1));
		var limitedPlan = TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems: [new TradeBuyTransactionItemRequest(1001, 1, UnitBuyPrice: 0, LimitedItemCanBuy: false)],
				TradeTemplate: NormalTemplate,
				UseKinah: false,
				PlayerCanTrade: true,
				AvailableKinah: 0,
				CurrentAbyssPoints: 0,
				FreeSlots: 1));

		Assert.Equal(TradeBuyTransactionPlanStatus.BlockedInventoryFull, fullPlan.Status);
		Assert.DoesNotContain(TradeBuyTransactionStep.CheckLimitedItems, fullPlan.Steps);
		Assert.Equal(TradeBuyTransactionPlanStatus.BlockedLimitedItem, limitedPlan.Status);
		Assert.Equal(1001, limitedPlan.RejectedItem!.ItemId);
	}

	private static readonly TradeListTemplateSummary NormalTemplate =
		new(203060, [129], NpcType: "NORMAL", SellPriceRate: 100);

	private static TradeBuyTransactionPlan CreateSuccessfulMutationPlan()
	{
		return TradeBuyTransactionPlanService.CreatePlan(
			new TradeBuyTransactionInput(
				TradeItems:
				[
					new TradeBuyTransactionItemRequest(1001, 2, UnitBuyPrice: 500, RequiredApPerItem: 1_000, AcquisitionType: "AP", RequiredItemId: 186000001, RequiredItemCountPerItem: 3),
					new TradeBuyTransactionItemRequest(1002, 1, UnitBuyPrice: 900, RequiredApPerItem: 500, AcquisitionType: "ABYSS", RequiredItemId: 186000001, RequiredItemCountPerItem: 1),
				],
				TradeTemplate: NormalTemplate,
				UseKinah: true,
				PlayerCanTrade: true,
				AvailableKinah: 10_000,
				CurrentAbyssPoints: 5_000,
				FreeSlots: 2,
				AvailableRequiredItems: new Dictionary<int, long> { [186000001] = 7 },
				VendorBuyModifier: 100));
	}
}
