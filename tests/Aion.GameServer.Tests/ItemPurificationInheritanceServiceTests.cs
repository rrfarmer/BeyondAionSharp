using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationInheritanceServiceTests
{
	[Fact]
	public void CreateTargetItemPlan_CopiesJavaUpgradeStateAndClampsTargetFields()
	{
		var source = CreateSourceItem(
			enchant: 25,
			tuneCount: 3,
			randomBonus: 7,
			isAmplified: true,
			buffSkill: 13006);
		var sourceTemplate = CreateTemplate(100000001, statBonusSetId: 1, maxTuneCount: 5, maxEnchantLevel: 15);
		var targetTemplate = CreateTemplate(100000002, statBonusSetId: 1, maxTuneCount: 1, maxEnchantLevel: 20);

		var plan = ItemPurificationInheritanceService.CreateTargetItemPlan(
			source,
			sourceTemplate,
			targetTemplate,
			targetObjectId: 9001);

		Assert.True(plan.Succeeded);
		Assert.False(plan.RandomBonusWasRerolled);
		Assert.NotNull(plan.TargetItem);
		var target = plan.TargetItem;
		Assert.Equal(9001, target.ObjectId);
		Assert.Equal(100000002, target.ItemId);
		Assert.Equal(1, target.Count);
		Assert.Equal(20, target.Enchant);
		Assert.Equal(4, target.EnchantBonus);
		Assert.Equal(1, target.TuneCount);
		Assert.True(target.IsAmplified);
		Assert.Equal(13006, target.BuffSkill);
		Assert.Equal(7, target.RandomBonus);
		Assert.Equal(2, target.FusionRandomBonus);
		Assert.Equal(500001, target.FusionedItem);
		Assert.Equal(12, target.OptionalSocket);
		Assert.Equal(5, target.OptionalFusionSocket);
		Assert.Equal(3, target.Tempering);
		Assert.True(target.IsSoulBound);
		Assert.Equal("Artisan", target.Creator);
		Assert.Equal(0x123456, target.Color);
		Assert.Equal(source.ManaStones, target.ManaStones);
		Assert.Equal(source.FusionStones, target.FusionStones);
		Assert.Equal(source.Godstone, target.Godstone);
	}

	[Fact]
	public void CreateTargetItemPlan_DropsAmplifiedAndBuffSkillWhenTargetEnchantFallsBelowLimits()
	{
		var source = CreateSourceItem(
			enchant: 20,
			tuneCount: -1,
			randomBonus: 0,
			isAmplified: true,
			buffSkill: 13006);
		var sourceTemplate = CreateTemplate(100000001, statBonusSetId: 1, maxTuneCount: 5, maxEnchantLevel: 15);
		var targetTemplate = CreateTemplate(100000002, statBonusSetId: 1, maxTuneCount: 2, maxEnchantLevel: 20);

		var plan = ItemPurificationInheritanceService.CreateTargetItemPlan(
			source,
			sourceTemplate,
			targetTemplate,
			targetObjectId: 9001);

		Assert.NotNull(plan.TargetItem);
		var target = plan.TargetItem;
		Assert.Equal(15, target.Enchant);
		Assert.Equal(0, target.TuneCount);
		Assert.False(target.IsAmplified);
		Assert.Equal(0, target.BuffSkill);
	}

	[Fact]
	public void CreateTargetItemPlan_RerollsRandomBonusWhenInventoryBonusSetsDiffer()
	{
		var source = CreateSourceItem(enchant: 25, tuneCount: 1, randomBonus: 7);
		var sourceTemplate = CreateTemplate(100000001, statBonusSetId: 1);
		var targetTemplate = CreateTemplate(100000002, statBonusSetId: 2);

		var plan = ItemPurificationInheritanceService.CreateTargetItemPlan(
			source,
			sourceTemplate,
			targetTemplate,
			targetObjectId: 9001,
			rerolledRandomBonusId: 11);

		Assert.True(plan.RandomBonusWasRerolled);
		Assert.Equal(11, plan.TargetItem?.RandomBonus);
	}

	[Fact]
	public void CreateTargetItemPlan_PreservesRandomBonusWhenDifferentInventorySetsHaveEqualGroupCounts()
	{
		var source = CreateSourceItem(enchant: 25, tuneCount: 1, randomBonus: 7);
		var sourceTemplate = CreateTemplate(100000001, statBonusSetId: 1);
		var targetTemplate = CreateTemplate(100000002, statBonusSetId: 2);
		var randomBonuses = CreateRandomBonuses(set1GroupCount: 2, set2GroupCount: 2);

		var plan = ItemPurificationInheritanceService.CreateTargetItemPlan(
			source,
			sourceTemplate,
			targetTemplate,
			targetObjectId: 9001,
			itemRandomBonuses: randomBonuses);

		Assert.True(plan.Succeeded);
		Assert.False(plan.RandomBonusWasRerolled);
		Assert.Equal(7, plan.TargetItem?.RandomBonus);
	}

	[Fact]
	public void CreateTargetItemPlan_SelectsRandomBonusWhenInventoryBonusSetsDifferAndTableIsAvailable()
	{
		var source = CreateSourceItem(enchant: 25, tuneCount: 1, randomBonus: 7);
		var sourceTemplate = CreateTemplate(100000001, statBonusSetId: 1);
		var targetTemplate = CreateTemplate(100000002, statBonusSetId: 2);
		var randomBonuses = CreateRandomBonuses(set1GroupCount: 1, set2GroupCount: 2);

		var plan = ItemPurificationInheritanceService.CreateTargetItemPlan(
			source,
			sourceTemplate,
			targetTemplate,
			targetObjectId: 9001,
			itemRandomBonuses: randomBonuses,
			randomBonusRoll: () => 0.75d);

		Assert.True(plan.Succeeded);
		Assert.True(plan.RandomBonusWasRerolled);
		Assert.Equal(2, plan.TargetItem?.RandomBonus);
	}

	[Fact]
	public void CreateTargetItemPlan_ReportsMissingInputs()
	{
		var source = CreateSourceItem(enchant: 15, tuneCount: 0, randomBonus: 0);
		var template = CreateTemplate(100000001);

		Assert.Equal(
			ItemPurificationInheritanceStatus.MissingSourceItem,
			ItemPurificationInheritanceService.CreateTargetItemPlan(null, template, template, 1).Status);
		Assert.Equal(
			ItemPurificationInheritanceStatus.MissingSourceTemplate,
			ItemPurificationInheritanceService.CreateTargetItemPlan(source, null, template, 1).Status);
		Assert.Equal(
			ItemPurificationInheritanceStatus.MissingTargetTemplate,
			ItemPurificationInheritanceService.CreateTargetItemPlan(source, template, null, 1).Status);
	}

	private static InventoryItem CreateSourceItem(
		int enchant,
		int tuneCount,
		int randomBonus,
		bool isAmplified = false,
		int buffSkill = 0)
	{
		var item = new InventoryItem
		{
			ObjectId = 1001,
			ItemId = 100000001,
			Count = 1,
			Color = 0x123456,
			Creator = "Artisan",
			OwnerId = 700,
			Location = 0,
			Enchant = enchant,
			EnchantBonus = 4,
			FusionedItem = 500001,
			OptionalSocket = 12,
			OptionalFusionSocket = 5,
			TuneCount = tuneCount,
			RandomBonus = randomBonus,
			FusionRandomBonus = 2,
			Tempering = 3,
			IsSoulBound = true,
			IsAmplified = isAmplified,
			BuffSkill = buffSkill,
		};
		item.ManaStones = [new ItemStoneSocket(167000001, 0)];
		item.FusionStones = [new ItemStoneSocket(167000002, 1)];
		item.Godstone = new PlayerGodstone(168000001, 4);
		return item;
	}

	private static ItemTemplateSummary CreateTemplate(
		int templateId,
		int statBonusSetId = 0,
		int maxTuneCount = 0,
		int maxEnchantLevel = 15)
	{
		return new ItemTemplateSummary(
			TemplateId: templateId,
			Name: $"item-{templateId}",
			DescriptionId: 0,
			Mask: 0,
			Level: 65,
			ItemGroup: "SWORD",
			ItemType: "normal",
			Quality: "MYTHIC",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 0,
			StatBonusSetId: statBonusSetId,
			MaxTuneCount: maxTuneCount,
			MaxEnchantLevel: maxEnchantLevel);
	}

	private static ItemRandomBonusTable CreateRandomBonuses(int set1GroupCount, int set2GroupCount)
	{
		return new ItemRandomBonusTable(
		[
			new ItemRandomBonusSummary("INVENTORY", 1, CreateModifierGroups(set1GroupCount), Enumerable.Repeat(1d, set1GroupCount).ToArray()),
			new ItemRandomBonusSummary("INVENTORY", 2, CreateModifierGroups(set2GroupCount), Enumerable.Repeat(1d, set2GroupCount).ToArray()),
		]);
	}

	private static IReadOnlyList<IReadOnlyList<ItemStatModifier>> CreateModifierGroups(int count)
	{
		return Enumerable.Range(1, count)
			.Select(index => (IReadOnlyList<ItemStatModifier>)[new ItemStatModifier("add", $"STAT{index}", index, Bonus: true)])
			.ToArray();
	}
}
