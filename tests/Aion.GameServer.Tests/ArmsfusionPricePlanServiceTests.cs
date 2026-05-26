using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ArmsfusionPricePlanServiceTests
{
	[Theory]
	[InlineData("JUNK", 200)]
	[InlineData("COMMON", 200)]
	[InlineData("RARE", 250)]
	[InlineData("LEGEND", 300)]
	[InlineData("UNIQUE", 400)]
	[InlineData("EPIC", 500)]
	[InlineData("MYTHIC", 600)]
	[InlineData("ANCIENT", 600)]
	public void GetBasePricePerLevelSquared_UsesJavaQualityMapping(string quality, long expectedBasePrice)
	{
		Assert.Equal(expectedBasePrice, ArmsfusionPricePlanService.GetBasePricePerLevelSquared(quality));
	}

	[Fact]
	public void CreatePlan_UsesJavaPriceFormulaForMainWeaponLevelAndQuality()
	{
		var template = CreateWeaponTemplate(level: 10, quality: "UNIQUE");

		var plan = ArmsfusionPricePlanService.CreatePlan(
			template,
			"ELYOS",
			new GameServerPriceOptions(),
			new PriceInfluenceRates(Elyos: 0.3f, Asmodians: 0.5f));

		Assert.False(plan.IsLive);
		Assert.Equal(400, plan.BasePricePerLevelSquared);
		Assert.Equal(10, plan.MainWeaponLevel);
		Assert.Equal(40_000, plan.BasePrice);
		Assert.Equal(46_200, plan.FusionPrice);
		Assert.Contains("ArmsfusionService.fusionWeapons", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateFusionPlan_SucceedsWithJavaValidationOrderAndNonLiveMutations()
	{
		var player = CreatePlayer(kinah: 50_000);
		var mainWeapon = new InventoryItem
		{
			ObjectId = 1001,
			ItemId = MainWeaponId,
			Count = 1,
			Location = 0,
			OptionalFusionSocket = 5,
			FusionRandomBonus = 3,
			Charge = 120_000,
			TuneCount = 1,
		};
		var fuseWeapon = new InventoryItem
		{
			ObjectId = 1002,
			ItemId = FuseWeaponId,
			Count = 1,
			Location = 0,
			OptionalSocket = 2,
			RandomBonus = 7,
			ManaStones = [new ItemStoneSocket(167000002, 2), new ItemStoneSocket(167000001, 0)],
		};
		player.InventoryItems = [mainWeapon, fuseWeapon, player.InventoryItems.Single()];

		var plan = ArmsfusionPricePlanService.CreateFusionPlan(
			player,
			mainWeaponObjectId: 1001,
			fuseWeaponObjectId: 1002,
			CreateItemTemplates(),
			influenceRates: new PriceInfluenceRates(Elyos: 0.3f, Asmodians: 0.5f));

		Assert.True(plan.Succeeded);
		Assert.False(plan.IsLive);
		Assert.Equal(ArmsfusionFailure.None, plan.Failure);
		Assert.Equal(46_200, plan.FusionPrice);
		var mainWeaponUpdate = Assert.IsType<InventoryItem>(plan.MainWeaponUpdate);
		Assert.Equal(FuseWeaponId, mainWeaponUpdate.FusionedItem);
		Assert.Equal(2, mainWeaponUpdate.OptionalFusionSocket);
		Assert.Equal(7, mainWeaponUpdate.FusionRandomBonus);
		Assert.Equal(0, mainWeaponUpdate.Charge);
		Assert.Equal(3, mainWeaponUpdate.TuneCount);
		Assert.Equal([0, 2], mainWeaponUpdate.FusionStones.Select(stone => stone.Slot).ToArray());
		Assert.Equal([167000001, 167000002], mainWeaponUpdate.FusionStones.Select(stone => stone.ItemId).ToArray());
		Assert.Null(plan.FuseWeaponUpdate);
		Assert.Equal(1002, plan.DeletedFuseWeaponObjectId);
		Assert.Equal(3_800, plan.KinahItemUpdate?.Count);
	}

	[Theory]
	[InlineData(ArmsfusionFailure.EquippedItem, false, true, true, 50_000, 0, 1, 0, 1, 0)]
	[InlineData(ArmsfusionFailure.ItemNoTarget, false, true, true, 50_000, 0, 1, 0, 1, 0)]
	[InlineData(ArmsfusionFailure.NotAvailable, true, false, true, 50_000, 0, 1, 0, 1, 0)]
	[InlineData(ArmsfusionFailure.NotEnoughKinah, true, true, true, 1, 0, 1, 0, 1, 0)]
	[InlineData(ArmsfusionFailure.TemporaryExchangeItem, true, true, true, 50_000, 1001, 1, 0, 1, 0)]
	[InlineData(ArmsfusionFailure.NotAvailable, true, true, true, 50_000, 0, 1, FuseWeaponId, 1, 0)]
	[InlineData(ArmsfusionFailure.DifferentType, true, true, true, 50_000, 0, 1, 0, 1, OtherWeaponId)]
	[InlineData(ArmsfusionFailure.MainRequireHigherLevel, true, true, true, 50_000, 0, 99, 0, 1, 0)]
	[InlineData(ArmsfusionFailure.NotComparableItem, true, true, true, 50_000, 0, 1, 0, 2, 0)]
	public void CreateFusionPlan_FollowsJavaFailureOrder(
		ArmsfusionFailure expectedFailure,
		bool includeMainInBag,
		bool mainCanFuse,
		bool includeFuse,
		long kinah,
		int temporaryExchangeObjectId,
		int fuseLevel,
		int fuseFusionedItem,
		int fuseChargeWay,
		int fuseItemIdOverride)
	{
		var player = CreatePlayer(kinah);
		var mainWeapon = new InventoryItem { ObjectId = 1001, ItemId = MainWeaponId, Count = 1, Location = 0, IsEquipped = !includeMainInBag };
		var fuseWeapon = new InventoryItem
		{
			ObjectId = 1002,
			ItemId = fuseItemIdOverride == 0 ? FuseWeaponId : fuseItemIdOverride,
			Count = 1,
			Location = 0,
			FusionedItem = fuseFusionedItem,
		};
		var items = new List<InventoryItem> { player.InventoryItems.Single() };
		if (includeMainInBag || expectedFailure == ArmsfusionFailure.EquippedItem)
			items.Add(mainWeapon);
		if (includeFuse)
			items.Add(fuseWeapon);
		player.InventoryItems = items;
		var temporaryExchangeItems = temporaryExchangeObjectId == 0
			? null
			: new HashSet<int> { temporaryExchangeObjectId };

		var plan = ArmsfusionPricePlanService.CreateFusionPlan(
			player,
			mainWeaponObjectId: 1001,
			fuseWeaponObjectId: 1002,
			CreateItemTemplates(mainCanFuse, fuseLevel, fuseChargeWay),
			temporaryExchangeItemObjectIds: temporaryExchangeItems);

		Assert.False(plan.Succeeded);
		Assert.Equal(expectedFailure, plan.Failure);
	}

	private static Player CreatePlayer(long kinah)
	{
		return new Player
		{
			ObjectId = 7001,
			Race = "ELYOS",
			InventoryItems =
			[
				new InventoryItem { ObjectId = 9001, ItemId = KinahItemId, Count = kinah, Location = 0 },
			],
		};
	}

	private static ItemTemplateTable CreateItemTemplates(bool mainCanFuse = true, int fuseLevel = 10, int fuseChargeWay = 1)
	{
		return new ItemTemplateTable(
		[
			CreateWeaponTemplate(level: 10, quality: "UNIQUE", itemId: MainWeaponId, canFuse: mainCanFuse, chargeWay: 1),
			CreateWeaponTemplate(level: fuseLevel, quality: "RARE", itemId: FuseWeaponId, canFuse: true, chargeWay: fuseChargeWay),
			CreateWeaponTemplate(level: 10, quality: "RARE", itemId: OtherWeaponId, canFuse: true, itemGroup: "BOW", chargeWay: 1),
		]);
	}

	private static ItemTemplateSummary CreateWeaponTemplate(
		int level,
		string quality,
		int itemId = MainWeaponId,
		bool canFuse = true,
		string itemGroup = "SWORD",
		int chargeWay = 1)
	{
		return new ItemTemplateSummary(
			TemplateId: itemId,
			Name: "Fusion Sword",
			DescriptionId: 0,
			Mask: canFuse ? 1 << 11 : 0,
			Level: level,
			ItemGroup: itemGroup,
			ItemType: "NORMAL",
			Quality: quality,
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 1,
			Improvement: new ItemImprovement(chargeWay, Level: 1, BurnAttack: 0, BurnDefend: 0, Price1: 0, Price2: 0),
			MaxTuneCount: 3);
	}

	private const int MainWeaponId = 1001;
	private const int FuseWeaponId = 1002;
	private const int OtherWeaponId = 1003;
	private const int KinahItemId = 182400001;
}
