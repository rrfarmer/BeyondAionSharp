using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class IdentifyItemExecutionPlanServiceTests
{
	[Fact]
	public void CreateStartPlan_UsesJavaStartAnimationAndDelay()
	{
		var plan = IdentifyItemExecutionPlanService.CreateStartPlan(playerObjectId: 7001, targetItemObjectId: 1001, targetItemId: 110100001);

		Assert.Equal(IdentifyItemExecutionPlanService.UseDurationMilliseconds, plan.DelayMilliseconds);
		Assert.Contains("5000, 9, 0", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateAbortPlan_UsesJavaCancellationMessageAndAnimation()
	{
		var plan = IdentifyItemExecutionPlanService.CreateAbortPlan(
			playerObjectId: 7001,
			targetItemObjectId: 1001,
			targetItemId: 110100001,
			targetItemName: "Tac Officer's Sword");

		Assert.Equal(IdentifyItemExecutionPlanService.ItemUseTaskName, plan.CancelledTaskName);
		Assert.Equal(1401625, plan.CancelMessage.MessageId);
		Assert.True(plan.RemoveObserver);
		Assert.Contains("STR_MSG_ITEM_IDENTIFY_CANCELED", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateCompletionPlan_RollsMutationAndBuildsPackets()
	{
		var targetItem = CreateItem(tuneCount: -1, optionalSockets: 0, enchantBonus: 0, randomBonus: 0);
		var targetTemplate = CreateTemplate(optionSlotBonus: 6, maxEnchantBonus: 7, statBonusSetId: 11);

		var plan = IdentifyItemExecutionPlanService.CreateCompletionPlan(
			targetItem,
			targetTemplate,
			playerObjectId: 7001,
			CreateRandomBonuses(),
			targetItemName: "Tac Officer's Sword",
			randomInclusive: (_, max) => max,
			randomBonusRoll: () => 0.75d);

		Assert.Equal(6, plan.TargetItemUpdate.OptionalSocket);
		Assert.Equal(7, plan.TargetItemUpdate.EnchantBonus);
		Assert.Equal(2, plan.TargetItemUpdate.RandomBonus);
		Assert.Equal(0, plan.TargetItemUpdate.TuneCount);
		Assert.Equal(1401626, plan.SuccessMessage.MessageId);
		Assert.True(plan.InventoryPersistentUpdateRequired);
		Assert.True(plan.RemoveObserver);
		Assert.NotNull(plan.InventoryUpdatePacket);
		Assert.Contains("UPDATE_REQUIRED", plan.JavaSource, StringComparison.Ordinal);
	}

	private static InventoryItem CreateItem(int tuneCount, int optionalSockets, int enchantBonus, int randomBonus) =>
		new()
		{
			ObjectId = 1001,
			ItemId = 110100001,
			OwnerId = 9001,
			Location = 0,
			Slot = 1,
			Count = 1,
			TuneCount = tuneCount,
			OptionalSocket = optionalSockets,
			EnchantBonus = enchantBonus,
			RandomBonus = randomBonus,
		};

	private static ItemTemplateSummary CreateTemplate(int optionSlotBonus, int maxEnchantBonus, int statBonusSetId) =>
		new(
			110100001,
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
			MaxTuneCount: 6,
			StatBonusSetId: statBonusSetId,
			MaxEnchantBonus: maxEnchantBonus,
			OptionSlotBonus: optionSlotBonus);

	private static ItemRandomBonusTable CreateRandomBonuses() =>
		new(
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
