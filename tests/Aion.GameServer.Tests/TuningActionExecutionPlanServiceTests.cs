using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TuningActionExecutionPlanServiceTests
{
	private const int TuningScrollItemId = 166030001;
	private const int TargetItemId = 100000001;

	[Fact]
	public void CreateStartPlan_UsesJavaStartAnimationAndDelay()
	{
		var plan = TuningActionExecutionPlanService.CreateStartPlan(playerObjectId: 7001, tuningScrollObjectId: 2001, tuningScrollItemId: TuningScrollItemId);

		Assert.Equal(TuningActionExecutionPlanService.UseDurationMilliseconds, plan.DelayMilliseconds);
		Assert.False(plan.IsLive);
		Assert.Contains("5000, 12, 0", plan.JavaSource);
	}

	[Fact]
	public void CreateAbortPlan_UsesJavaCancellationMessageAndAbortAnimation()
	{
		var plan = TuningActionExecutionPlanService.CreateAbortPlan(
			playerObjectId: 7001,
			tuningScrollObjectId: 2001,
			tuningScrollItemId: TuningScrollItemId,
			removedCooldownDelayId: 42,
			targetItemName: "Tac Officer's Sword");

		Assert.Equal(TuningActionExecutionPlanService.ItemUseTaskName, plan.CancelledTaskName);
		Assert.Equal(42, plan.RemovedCooldownDelayId);
		Assert.Equal(1401638, plan.CancelMessage.MessageId);
		Assert.True(plan.RemoveObserver);
		Assert.Contains("STR_MSG_ITEM_REIDENTIFY_CANCELED", plan.JavaSource);
	}

	[Fact]
	public void CreateCompletionPlan_ScrollConsumptionFailureStopsAfterCompletionAnimation()
	{
		var plan = TuningActionExecutionPlanService.CreateCompletionPlan(
			CreateItem(tuneCount: 2, optionalSockets: 3, enchantBonus: 4),
			CreateTargetTemplate(maxTuneCount: 5, optionSlotBonus: 6, maxEnchantBonus: 7, statBonusSetId: 11),
			maxOptionalSockets: 6,
			maxEnchantBonus: 7,
			playerObjectId: 7001,
			tuningScrollObjectId: 2001,
			tuningScrollItemId: TuningScrollItemId,
			shouldNotReduceTuneCount: false,
			scrollConsumptionSucceeded: false,
			CreateRandomBonuses(),
			targetItemName: "Tac Officer's Sword");

		Assert.Equal(TuningActionCompletionPlanStatus.ScrollConsumptionFailed, plan.Status);
		Assert.True(plan.RemoveObserver);
		Assert.True(plan.AttemptDecreaseScroll);
		Assert.Null(plan.TargetItemUpdate);
		Assert.Null(plan.PendingResult);
		Assert.Null(plan.ResultPacket);
		Assert.Null(plan.SuccessMessage);
		Assert.False(plan.InventoryPersistentUpdateRequired);
		Assert.Contains("decreaseByObjectId failed", plan.JavaSource);
	}

	[Fact]
	public void CreateCompletionPlan_AttributeOnlyReusesTuneStateAndSetsJavaFlags()
	{
		var plan = TuningActionExecutionPlanService.CreateCompletionPlan(
			CreateItem(tuneCount: 2, optionalSockets: 3, enchantBonus: 4),
			CreateTargetTemplate(maxTuneCount: 5, optionSlotBonus: 6, maxEnchantBonus: 7, statBonusSetId: 11),
			maxOptionalSockets: 6,
			maxEnchantBonus: 7,
			playerObjectId: 7001,
			tuningScrollObjectId: 2001,
			tuningScrollItemId: TuningScrollItemId,
			shouldNotReduceTuneCount: true,
			scrollConsumptionSucceeded: true,
			CreateRandomBonuses(),
			targetItemName: "Tac Officer's Sword",
			randomBonusRoll: () => 0.75d);

		Assert.Equal(TuningActionCompletionPlanStatus.Planned, plan.Status);
		Assert.Equal(2, plan.TargetItemUpdate?.TuneCount);
		Assert.Same(plan.PendingResult, plan.TargetItemUpdate?.PendingTuneResult);
		Assert.Equal(3, plan.PendingResult?.OptionalSockets);
		Assert.Equal(4, plan.PendingResult?.EnchantBonus);
		Assert.Equal(2, plan.PendingResult?.StatBonusId);
		Assert.True(plan.PendingResult?.IsAttributeOnly);
		Assert.False(plan.InventoryPersistentUpdateRequired);
		Assert.Equal(1401639, plan.SuccessMessage?.MessageId);
	}

	[Fact]
	public void CreateCompletionPlan_NormalTuneIncrementsCountAndMarksInventoryUpdateRequired()
	{
		var plan = TuningActionExecutionPlanService.CreateCompletionPlan(
			CreateItem(tuneCount: 2, optionalSockets: 3, enchantBonus: 4),
			CreateTargetTemplate(maxTuneCount: 5, optionSlotBonus: 6, maxEnchantBonus: 7, statBonusSetId: 11),
			maxOptionalSockets: 6,
			maxEnchantBonus: 7,
			playerObjectId: 7001,
			tuningScrollObjectId: 2001,
			tuningScrollItemId: TuningScrollItemId,
			shouldNotReduceTuneCount: false,
			scrollConsumptionSucceeded: true,
			CreateRandomBonuses(),
			targetItemName: "Tac Officer's Sword",
			randomInclusive: (_, max) => max,
			randomBonusRoll: () => 0d);

		Assert.Equal(TuningActionCompletionPlanStatus.Planned, plan.Status);
		Assert.Equal(3, plan.TargetItemUpdate?.TuneCount);
		Assert.Same(plan.PendingResult, plan.TargetItemUpdate?.PendingTuneResult);
		Assert.Equal(6, plan.PendingResult?.OptionalSockets);
		Assert.Equal(7, plan.PendingResult?.EnchantBonus);
		Assert.Equal(1, plan.PendingResult?.StatBonusId);
		Assert.False(plan.PendingResult?.IsAttributeOnly);
		Assert.True(plan.InventoryPersistentUpdateRequired);
		Assert.NotNull(plan.ResultPacket);
		Assert.Equal(1401639, plan.SuccessMessage?.MessageId);
		Assert.Contains("UPDATE_REQUIRED", plan.JavaSource);
	}

	private static InventoryItem CreateItem(int tuneCount, int optionalSockets, int enchantBonus)
	{
		return new InventoryItem
		{
			ObjectId = 1001,
			ItemId = TargetItemId,
			Location = 0,
			Slot = 1,
			Count = 1,
			TuneCount = tuneCount,
			OptionalSocket = optionalSockets,
			EnchantBonus = enchantBonus,
		};
	}

	private static ItemTemplateSummary CreateTargetTemplate(int maxTuneCount, int optionSlotBonus, int maxEnchantBonus, int statBonusSetId)
	{
		_ = optionSlotBonus;
		_ = maxEnchantBonus;

		return new ItemTemplateSummary(
			TargetItemId,
			"Tac Officer's Sword",
			0,
			1,
			55,
			"SWORD",
			"NORMAL",
			"UNIQUE",
			"PC_ALL",
			1,
			0,
			1,
			CanTune: true,
			MaxTuneCount: maxTuneCount,
			StatBonusSetId: statBonusSetId);
	}

	private static ItemRandomBonusTable CreateRandomBonuses()
	{
		return new ItemRandomBonusTable(
		[
			new ItemRandomBonusSummary(
				"INVENTORY",
				11,
				[
					[new ItemStatModifier("add", "MAXHP", 20, Bonus: true)],
					[new ItemStatModifier("add", "MAXMP", 15, Bonus: true)],
				],
				[1d, 1d]),
		]);
	}
}
