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
	public void CreatePolishPlan_RejectsUnidentifiedTarget()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, level: 20, itemGroup: "SWORD", mask: 1 << 17),
			CreateTemplate(600, level: 10, polishSetId: 12),
		]);
		var randomBonuses = new ItemRandomBonusTable([]);

		var plan = IdianPolishService.CreatePolishPlan(
			CreateItem(600, objectId: 10),
			CreateItem(100, objectId: 20, tuneCount: -1),
			itemTemplates,
			randomBonuses);

		Assert.Equal(IdianPolishResult.NeedIdentify, plan.Result);
		Assert.Null(plan.SourceItemUpdate);
		Assert.False(plan.DeleteSourceItem);
		Assert.Null(plan.TargetItemUpdate);
	}

	[Fact]
	public void InventoryItem_IsIdentifiedFollowsJavaTuneCount()
	{
		Assert.False(CreateItem(100, objectId: 20, tuneCount: -1).IsIdentified);
		Assert.True(CreateItem(100, objectId: 20, tuneCount: 0).IsIdentified);
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

	[Fact]
	public void BurnEquippedWeaponPolishCharge_BurnsMainWeaponIdiansAndSkipsOffHands()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, level: 20, itemGroup: "SWORD", mask: 1 << 17, idianInfo: new ItemIdianInfo(60_000, 12)),
			CreateTemplate(101, level: 20, itemGroup: "DAGGER", mask: 1 << 17, idianInfo: new ItemIdianInfo(60_000, 12)),
			CreateTemplate(102, level: 20, itemGroup: "CL_TORSO", mask: 1 << 17, idianInfo: new ItemIdianInfo(60_000, 12)),
		]);
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(100, objectId: 20, polishCharge: 500_000, isEquipped: true, slot: 1),
				CreateItem(101, objectId: 21, polishCharge: 500_000, isEquipped: true, slot: 2),
				CreateItem(100, objectId: 22, polishCharge: 500_000, isEquipped: true, slot: 1L << 17),
				CreateItem(102, objectId: 23, polishCharge: 500_000, isEquipped: true, slot: 8),
				CreateItem(100, objectId: 24, polishCharge: 500_000, isEquipped: false, slot: 65535),
			],
		};

		var plan = IdianPolishService.BurnEquippedWeaponPolishCharge(player, itemTemplates, skillValue: 250_000);

		Assert.True(plan.Changed);
		Assert.Equal([20, 21], plan.Burns.Select(burn => burn.ItemUpdate.ObjectId));
		Assert.All(plan.Burns, burn => Assert.Equal(IdianPolishBurnUpdateKind.LowCharge, burn.UpdateKind));
		Assert.Equal(250_000, plan.InventoryItems.First(item => item.ObjectId == 20).IdianStone?.PolishCharge);
		Assert.Equal(250_000, plan.InventoryItems.First(item => item.ObjectId == 21).IdianStone?.PolishCharge);
		Assert.Equal(500_000, plan.InventoryItems.First(item => item.ObjectId == 22).IdianStone?.PolishCharge);
		Assert.Equal(500_000, plan.InventoryItems.First(item => item.ObjectId == 23).IdianStone?.PolishCharge);
		Assert.Equal(500_000, plan.InventoryItems.First(item => item.ObjectId == 24).IdianStone?.PolishCharge);
	}

	[Fact]
	public void BurnEquippedWeaponPolishCharge_RemovesExhaustedIdian()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, level: 20, itemGroup: "SWORD", mask: 1 << 17, idianInfo: new ItemIdianInfo(60_000, 12)),
		]);
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(100, objectId: 20, polishCharge: 200_000, isEquipped: true, slot: 1),
			],
		};

		var plan = IdianPolishService.BurnEquippedWeaponPolishCharge(player, itemTemplates, skillValue: 250_000);

		Assert.True(plan.Changed);
		var burn = Assert.Single(plan.Burns);
		Assert.Equal(IdianPolishBurnUpdateKind.Exhausted, burn.UpdateKind);
		Assert.Null(burn.ItemUpdate.IdianStone);
		Assert.Null(Assert.Single(plan.InventoryItems).IdianStone);
	}

	[Fact]
	public void BurnEquippedWeaponPolishChargeForObserverEvent_BurnsOnlyMainHandObserverIdian()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, level: 20, itemGroup: "SWORD", mask: 1 << 17, idianInfo: new ItemIdianInfo(60_000, 100_000)),
			CreateTemplate(101, level: 20, itemGroup: "DAGGER", mask: 1 << 17, idianInfo: new ItemIdianInfo(60_000, 100_000)),
			CreateTemplate(102, level: 20, itemGroup: "CL_TORSO", mask: 1 << 17, idianInfo: new ItemIdianInfo(60_000, 100_000)),
		]);
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(100, objectId: 20, polishCharge: 500_000, isEquipped: true, slot: 1),
				CreateItem(101, objectId: 21, polishCharge: 500_000, isEquipped: true, slot: 2),
				CreateItem(100, objectId: 22, polishCharge: 500_000, isEquipped: true, slot: 1L << 17),
				CreateItem(102, objectId: 23, polishCharge: 500_000, isEquipped: true, slot: 1),
				CreateItem(100, objectId: 24, polishCharge: 500_000, isEquipped: false, slot: 1),
			],
		};

		var attackPlan = IdianPolishService.BurnEquippedWeaponPolishChargeForObserverEvent(
			player,
			itemTemplates,
			IdianPolishObserverEvent.Attack,
			skillId: 0);
		var attackedPlan = IdianPolishService.BurnEquippedWeaponPolishChargeForObserverEvent(
			player,
			itemTemplates,
			IdianPolishObserverEvent.Attacked,
			skillId: 2001);

		Assert.True(attackPlan.Changed);
		var attackBurn = Assert.Single(attackPlan.Burns);
		Assert.Equal(20, attackBurn.ItemUpdate.ObjectId);
		Assert.Equal(440_000, attackBurn.ItemUpdate.IdianStone?.PolishCharge);
		Assert.Equal(500_000, attackPlan.InventoryItems.First(item => item.ObjectId == 21).IdianStone?.PolishCharge);
		Assert.Equal(500_000, attackPlan.InventoryItems.First(item => item.ObjectId == 22).IdianStone?.PolishCharge);
		Assert.Equal(500_000, attackPlan.InventoryItems.First(item => item.ObjectId == 23).IdianStone?.PolishCharge);
		Assert.Equal(500_000, attackPlan.InventoryItems.First(item => item.ObjectId == 24).IdianStone?.PolishCharge);

		Assert.True(attackedPlan.Changed);
		var attackedBurn = Assert.Single(attackedPlan.Burns);
		Assert.Equal(20, attackedBurn.ItemUpdate.ObjectId);
		Assert.Equal(400_000, attackedBurn.ItemUpdate.IdianStone?.PolishCharge);
	}

	[Fact]
	public void BurnEquippedWeaponPolishChargeForObserverEvent_SkipsSkillAttackButAllowsDotAttacked()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, level: 20, itemGroup: "SWORD", mask: 1 << 17, idianInfo: new ItemIdianInfo(60_000, 100_000)),
		]);
		var player = new Player
		{
			InventoryItems =
			[
				CreateItem(100, objectId: 20, polishCharge: 500_000, isEquipped: true, slot: 1),
			],
		};

		var skillAttackPlan = IdianPolishService.BurnEquippedWeaponPolishChargeForObserverEvent(
			player,
			itemTemplates,
			IdianPolishObserverEvent.Attack,
			skillId: 2001);
		var dotPlan = IdianPolishService.BurnEquippedWeaponPolishChargeForObserverEvent(
			player,
			itemTemplates,
			IdianPolishObserverEvent.DotAttacked,
			skillId: 2001);

		Assert.False(skillAttackPlan.Changed);
		Assert.Empty(skillAttackPlan.Burns);
		Assert.True(dotPlan.Changed);
		Assert.Equal(400_000, Assert.Single(dotPlan.Burns).ItemUpdate.IdianStone?.PolishCharge);
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

	private static InventoryItem CreateItem(
		int itemId,
		int objectId,
		long count = 1,
		int polishCharge = 0,
		int tuneCount = 0,
		bool isEquipped = false,
		long slot = 0)
	{
		var item = new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			Location = 0,
			TuneCount = tuneCount,
			IsEquipped = isEquipped,
			Slot = slot,
		};
		if (polishCharge > 0)
			item.IdianStone = new PlayerIdianStone(600, 1, polishCharge);
		return item;
	}
}
