using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class IdianPolishServiceTests
{
	[Fact]
	public void CreatePolishPlan_AppliesSelectedBonusAndConsumesOneIdian()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, level: 20, itemGroup: "SWORD", mask: 1 << 17, idianInfo: new ItemIdianInfo(29, 12)),
			CreateTemplate(600, level: 10, polishSetId: 12),
		]);
		var randomBonuses = new ItemRandomBonusTable(
		[
			new ItemRandomBonusSummary(
				"POLISH",
				12,
				[
					[new ItemStatModifier("add", "MAXHP", 13, Bonus: true)],
					[new ItemStatModifier("add", "MAXMP", 8, Bonus: true)],
				],
				[25d, 75d]),
		]);

		var plan = IdianPolishService.CreatePolishPlan(
			CreateItem(600, objectId: 10, count: 2),
			CreateItem(100, objectId: 20),
			itemTemplates,
			randomBonuses,
			() => 0.75);

		Assert.Equal(IdianPolishResult.Success, plan.Result);
		Assert.False(plan.DeleteSourceItem);
		Assert.Equal(1, plan.SourceItemUpdate?.Count);
		Assert.Equal(600, plan.TargetItemUpdate?.IdianStone?.ItemId);
		Assert.Equal(2, plan.TargetItemUpdate?.IdianStone?.PolishNumber);
		Assert.Equal(IdianPolishService.FullPolishCharge, plan.TargetItemUpdate?.IdianStone?.PolishCharge);
	}

	[Fact]
	public void CreatePolishPlan_RejectsIdianAboveTargetLevel()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, level: 10, itemGroup: "SWORD", mask: 1 << 17),
			CreateTemplate(600, level: 20, polishSetId: 12),
		]);
		var randomBonuses = new ItemRandomBonusTable([]);

		var plan = IdianPolishService.CreatePolishPlan(
			CreateItem(600, objectId: 10),
			CreateItem(100, objectId: 20),
			itemTemplates,
			randomBonuses);

		Assert.Equal(IdianPolishResult.WrongLevel, plan.Result);
		Assert.Null(plan.SourceItemUpdate);
		Assert.False(plan.DeleteSourceItem);
		Assert.Null(plan.TargetItemUpdate);
	}

	[Fact]
	public void DecreasePolishCharge_MatchesJavaThresholdAndExhaustionUpdates()
	{
		var template = CreateTemplate(100, level: 20, itemGroup: "SWORD", mask: 1 << 17, idianInfo: new ItemIdianInfo(60_000, 12));
		var chargedItem = CreateItem(100, objectId: 20, polishCharge: 350_000);

		var lowCharge = IdianPolishService.DecreasePolishCharge(chargedItem, template);

		Assert.NotNull(lowCharge);
		Assert.Equal(IdianPolishBurnUpdateKind.LowCharge, lowCharge.UpdateKind);
		Assert.Equal(290_000, lowCharge.ItemUpdate.IdianStone?.PolishCharge);

		var exhausted = IdianPolishService.DecreasePolishCharge(lowCharge.ItemUpdate, template, skillValue: 500_000);

		Assert.NotNull(exhausted);
		Assert.Equal(IdianPolishBurnUpdateKind.Exhausted, exhausted.UpdateKind);
		Assert.Null(exhausted.ItemUpdate.IdianStone);
	}

	private static ItemTemplateSummary CreateTemplate(
		int templateId,
		int level = 1,
		string itemGroup = "NONE",
		int mask = 0,
		int polishSetId = 0,
		ItemIdianInfo? idianInfo = null)
	{
		return new ItemTemplateSummary(
			templateId,
			$"item_{templateId}",
			0,
			mask,
			level,
			itemGroup,
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			itemGroup == "NONE" ? 0 : 3,
			PolishSetId: polishSetId,
			IdianInfo: idianInfo);
	}

	private static InventoryItem CreateItem(int itemId, int objectId, long count = 1, int polishCharge = 0)
	{
		var item = new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			Location = 0,
		};
		if (polishCharge > 0)
			item.IdianStone = new PlayerIdianStone(600, 1, polishCharge);
		return item;
	}
}
