using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemChargeServiceTests
{
	[Fact]
	public void CreateChargePlan_UsesJavaPriceMathForKinahConditioning()
	{
		var improvement = new ItemImprovement(ChargeWay: 1, Level: 2, BurnAttack: 0, BurnDefend: 0, Price1: 1000, Price2: 2000);
		var itemTemplates = new ItemTemplateTable([CreateTemplate(100, improvement: improvement)]);
		var player = CreatePlayer(rank: 18);

		var levelOnePlan = ItemChargeService.CreateChargePlan(
			player,
			CreateItem(charge: 0),
			itemTemplates,
			maxLevel: 1,
			ignoreRankRequirement: false,
			requirePayment: true);

		Assert.NotNull(levelOnePlan);
		Assert.Equal(1, levelOnePlan.Level);
		Assert.Equal(ItemChargeService.Level1ChargePoints, levelOnePlan.TargetChargePoints);
		Assert.Equal(500, levelOnePlan.PaymentAmount);
		Assert.Equal(1, levelOnePlan.ChargeWay);

		var fullLevelTwoPlan = ItemChargeService.CreateChargePlan(
			player,
			CreateItem(charge: 0),
			itemTemplates,
			maxLevel: 2,
			ignoreRankRequirement: false,
			requirePayment: true);

		Assert.NotNull(fullLevelTwoPlan);
		Assert.Equal(2, fullLevelTwoPlan.Level);
		Assert.Equal(ItemChargeService.Level2ChargePoints, fullLevelTwoPlan.TargetChargePoints);
		Assert.Equal(1500, fullLevelTwoPlan.PaymentAmount);

		var updateLevelTwoPlan = ItemChargeService.CreateChargePlan(
			player,
			CreateItem(charge: ItemChargeService.Level1ChargePoints),
			itemTemplates,
			maxLevel: 2,
			ignoreRankRequirement: false,
			requirePayment: true);

		Assert.NotNull(updateLevelTwoPlan);
		Assert.Equal(2, updateLevelTwoPlan.Level);
		Assert.Equal(ItemChargeService.Level2ChargePoints, updateLevelTwoPlan.TargetChargePoints);
		Assert.Equal(1000, updateLevelTwoPlan.PaymentAmount);
	}

	[Fact]
	public void CreateChargePlan_LimitsLevelByRecommendedRankAndFusionTemplate()
	{
		var improvement = new ItemImprovement(ChargeWay: 2, Level: 2, BurnAttack: 0, BurnDefend: 0, Price1: 1000, Price2: 2000);
		var rankLimitedTemplates = new ItemTemplateTable([CreateTemplate(100, improvement: improvement, recommendRank: 15)]);
		var underRankedPlayer = CreatePlayer(rank: 14);

		var rankLimitedPlan = ItemChargeService.CreateChargePlan(
			underRankedPlayer,
			CreateItem(charge: 0),
			rankLimitedTemplates,
			maxLevel: 2,
			ignoreRankRequirement: false,
			requirePayment: true);

		Assert.NotNull(rankLimitedPlan);
		Assert.Equal(1, rankLimitedPlan.Level);
		Assert.Equal(ItemChargeService.Level1ChargePoints, rankLimitedPlan.TargetChargePoints);
		Assert.Equal(2, rankLimitedPlan.ChargeWay);

		var fusionTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, level: 50),
			CreateTemplate(200, level: 55, improvement: improvement, recommendRank: 15),
		]);
		var fusionPlan = ItemChargeService.CreateChargePlan(
			underRankedPlayer,
			CreateItem(charge: 0, fusionedItem: 200),
			fusionTemplates,
			maxLevel: 2,
			ignoreRankRequirement: false,
			requirePayment: true);

		Assert.NotNull(fusionPlan);
		Assert.Equal(1, fusionPlan.Level);
		Assert.Equal(ItemChargeService.Level1ChargePoints, fusionPlan.TargetChargePoints);
		Assert.Equal(100, fusionPlan.Template.TemplateId);
		Assert.Equal(2, fusionPlan.ChargeWay);
	}

	[Fact]
	public void CreateAbyssPointPaymentPlan_RejectsInsufficientApBeforeAbyssPointsClamp()
	{
		var player = CreatePlayer(rank: 18);
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 400, Rank = 1, MaxRank = 1 };

		var paymentPlan = ItemChargeService.CreateAbyssPointPaymentPlan(player, requiredAbyssPoints: 500);

		Assert.False(paymentPlan.Succeeded);
		Assert.Equal(ItemChargeAbyssPointPaymentStatus.InsufficientAbyssPoints, paymentPlan.Status);
		Assert.Equal(400, paymentPlan.CurrentAbyssPoints);
		Assert.Equal(500, paymentPlan.RequiredAbyssPoints);
		Assert.Null(paymentPlan.AbyssPointsPlan);
		Assert.Equal(400, player.AbyssRank.Ap);
	}

	[Fact]
	public void CreateAbyssPointPaymentPlan_CreatesNegativeAbyssPointsPlanWhenAffordable()
	{
		var player = CreatePlayer(rank: 18);
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 900, Rank = 1, MaxRank = 1 };

		var paymentPlan = ItemChargeService.CreateAbyssPointPaymentPlan(player, requiredAbyssPoints: 500);

		Assert.True(paymentPlan.Succeeded);
		Assert.Equal(ItemChargeAbyssPointPaymentStatus.Ready, paymentPlan.Status);
		Assert.NotNull(paymentPlan.AbyssPointsPlan);
		Assert.Equal(900, paymentPlan.CurrentAbyssPoints);
		Assert.Equal(500, paymentPlan.RequiredAbyssPoints);
		Assert.Equal(400, paymentPlan.AbyssPointsPlan.UpdatedRank?.Ap);
		Assert.Equal(-500, paymentPlan.AbyssPointsPlan.Added);
		Assert.Equal(900, player.AbyssRank.Ap);
	}

	[Fact]
	public void CreateAbyssPointPaymentPlan_RejectsPaymentsThatCannotMatchJavaIntSpend()
	{
		var player = CreatePlayer(rank: 18);
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = int.MaxValue, Rank = 1, MaxRank = 1 };

		var paymentPlan = ItemChargeService.CreateAbyssPointPaymentPlan(player, requiredAbyssPoints: (long)int.MaxValue + 1);

		Assert.False(paymentPlan.Succeeded);
		Assert.Equal(ItemChargeAbyssPointPaymentStatus.PaymentTooLarge, paymentPlan.Status);
		Assert.Null(paymentPlan.AbyssPointsPlan);
	}

	[Fact]
	public void CreateAbyssPointPaymentPlan_SkipsZeroPayment()
	{
		var paymentPlan = ItemChargeService.CreateAbyssPointPaymentPlan(CreatePlayer(rank: 18), requiredAbyssPoints: 0);

		Assert.True(paymentPlan.Succeeded);
		Assert.Equal(ItemChargeAbyssPointPaymentStatus.NoPaymentRequired, paymentPlan.Status);
		Assert.Null(paymentPlan.AbyssPointsPlan);
	}

	[Fact]
	public void DecreaseChargePoints_UsesJavaAttackAndDefendBurnAmounts()
	{
		var improvement = new ItemImprovement(ChargeWay: 1, Level: 2, BurnAttack: 200, BurnDefend: 100, Price1: 1000, Price2: 2000);
		var item = CreateItem(charge: 120_000);

		var attackBurn = ItemChargeService.DecreaseChargePoints(item, improvement, isAttacked: false);
		var defendBurn = ItemChargeService.DecreaseChargePoints(item, improvement, isAttacked: true);

		Assert.NotNull(attackBurn);
		Assert.Equal(119_800, attackBurn.ItemUpdate.Charge);
		Assert.Equal(-200, attackBurn.PointsDelta);
		Assert.False(attackBurn.ChargeBarChanged);
		Assert.Equal(120_000, item.Charge);

		Assert.NotNull(defendBurn);
		Assert.Equal(119_900, defendBurn.ItemUpdate.Charge);
		Assert.Equal(-100, defendBurn.PointsDelta);
	}

	[Fact]
	public void UpdateChargePoints_ClampsAndReportsJavaChargeBarStepChanges()
	{
		var dropsOneVisualStep = ItemChargeService.UpdateChargePoints(CreateItem(charge: 100_050), pointsToAdd: -100);
		var clampsToZero = ItemChargeService.UpdateChargePoints(CreateItem(charge: 50), pointsToAdd: -200);
		var alreadyFull = ItemChargeService.UpdateChargePoints(CreateItem(charge: ItemChargeService.Level2ChargePoints), pointsToAdd: 100);

		Assert.NotNull(dropsOneVisualStep);
		Assert.Equal(99_950, dropsOneVisualStep.ItemUpdate.Charge);
		Assert.True(dropsOneVisualStep.ChargeBarChanged);
		Assert.Equal(-100, dropsOneVisualStep.PointsDelta);

		Assert.NotNull(clampsToZero);
		Assert.Equal(0, clampsToZero.ItemUpdate.Charge);
		Assert.False(clampsToZero.ChargeBarChanged);
		Assert.Equal(-50, clampsToZero.PointsDelta);

		Assert.Null(alreadyFull);
	}

	[Fact]
	public void BurnEquippedChargePoints_BurnsAllEquippedConditionedItemsForObserverEvents()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, improvement: new ItemImprovement(ChargeWay: 1, Level: 2, BurnAttack: 200, BurnDefend: 100, Price1: 1000, Price2: 2000)),
			CreateTemplate(101, improvement: new ItemImprovement(ChargeWay: 1, Level: 2, BurnAttack: 300, BurnDefend: 150, Price1: 1000, Price2: 2000)),
			CreateTemplate(102),
			CreateTemplate(200, level: 55, improvement: new ItemImprovement(ChargeWay: 2, Level: 2, BurnAttack: 500, BurnDefend: 250, Price1: 1000, Price2: 2000)),
		]);
		var player = CreatePlayer(rank: 18);
		player.InventoryItems =
		[
			CreateItem(itemId: 100, objectId: 10, charge: 100_050, isEquipped: true),
			CreateItem(itemId: 101, objectId: 11, charge: 120_000, isEquipped: true),
			CreateItem(itemId: 102, objectId: 12, charge: 120_000, isEquipped: true),
			CreateItem(itemId: 100, objectId: 13, charge: 120_000, isEquipped: false),
			CreateItem(itemId: 100, objectId: 14, charge: 0, isEquipped: true),
			CreateItem(itemId: 102, objectId: 15, charge: 120_000, fusionedItem: 200, isEquipped: true),
		];

		var attackPlan = ItemChargeService.BurnEquippedChargePoints(
			player,
			itemTemplates,
			ItemChargeObserverEvent.Attack,
			skillId: 0);
		var attackedPlan = ItemChargeService.BurnEquippedChargePoints(
			player,
			itemTemplates,
			ItemChargeObserverEvent.Attacked,
			skillId: 0);

		Assert.True(attackPlan.Changed);
		Assert.Equal([10, 11, 15], attackPlan.Burns.Select(burn => burn.ItemUpdate.ObjectId));
		Assert.Equal(99_850, attackPlan.InventoryItems.First(item => item.ObjectId == 10).Charge);
		Assert.Equal(119_700, attackPlan.InventoryItems.First(item => item.ObjectId == 11).Charge);
		Assert.Equal(119_500, attackPlan.InventoryItems.First(item => item.ObjectId == 15).Charge);
		Assert.Equal(120_000, attackPlan.InventoryItems.First(item => item.ObjectId == 12).Charge);
		Assert.Equal(120_000, attackPlan.InventoryItems.First(item => item.ObjectId == 13).Charge);
		Assert.True(attackPlan.Burns.First(burn => burn.ItemUpdate.ObjectId == 10).ChargeBarChanged);

		Assert.True(attackedPlan.Changed);
		Assert.Equal(99_950, attackedPlan.InventoryItems.First(item => item.ObjectId == 10).Charge);
		Assert.Equal(119_850, attackedPlan.InventoryItems.First(item => item.ObjectId == 11).Charge);
		Assert.Equal(119_750, attackedPlan.InventoryItems.First(item => item.ObjectId == 15).Charge);
	}

	[Fact]
	public void BurnEquippedChargePoints_SkipsSkillAttackButAllowsDotAttacked()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100, improvement: new ItemImprovement(ChargeWay: 1, Level: 2, BurnAttack: 200, BurnDefend: 100, Price1: 1000, Price2: 2000)),
		]);
		var player = CreatePlayer(rank: 18);
		player.InventoryItems =
		[
			CreateItem(itemId: 100, objectId: 10, charge: 120_000, isEquipped: true),
		];

		var skillAttackPlan = ItemChargeService.BurnEquippedChargePoints(
			player,
			itemTemplates,
			ItemChargeObserverEvent.Attack,
			skillId: 2001);
		var dotPlan = ItemChargeService.BurnEquippedChargePoints(
			player,
			itemTemplates,
			ItemChargeObserverEvent.DotAttacked,
			skillId: 2001);

		Assert.False(skillAttackPlan.Changed);
		Assert.Empty(skillAttackPlan.Burns);
		Assert.True(dotPlan.Changed);
		Assert.Equal(119_900, Assert.Single(dotPlan.Burns).ItemUpdate.Charge);
	}

	[Fact]
	public void PlayerAbyssRank_AddApMatchesSoldierRankThresholds()
	{
		var rank = PlayerAbyssRank.Default() with { Ap = 200_000, Rank = 9, MaxRank = 9 };

		var updated = rank.AddAp(-60_000);

		Assert.Equal(140_000, updated.Ap);
		Assert.Equal(8, updated.Rank);
		Assert.Equal(9, updated.MaxRank);
	}

	[Fact]
	public void PlayerAbyssRank_AddApAppliesJavaCapAndAboveCapClamp()
	{
		var nearCap = PlayerAbyssRank.Default() with { Ap = 900, Rank = 1, MaxRank = 1 };
		var cappedGain = nearCap.AddAp(200, enableApCap: true, apCapValue: 1_000);

		Assert.Equal(1_000, cappedGain.Ap);
		Assert.Equal(200, cappedGain.DailyAp);
		Assert.Equal(200, cappedGain.WeeklyAp);

		var aboveCap = PlayerAbyssRank.Default() with { Ap = 1_500, DailyAp = 10, WeeklyAp = 20, Rank = 2, MaxRank = 2 };
		var clampedBackToCap = aboveCap.AddAp(50, enableApCap: true, apCapValue: 1_000);

		Assert.Equal(1_000, clampedBackToCap.Ap);
		Assert.Equal(60, clampedBackToCap.DailyAp);
		Assert.Equal(70, clampedBackToCap.WeeklyAp);
	}

	private static Player CreatePlayer(int rank)
	{
		return new Player
		{
			ObjectId = 1,
			Name = "Conditioner",
			AbyssRank = PlayerAbyssRank.Default() with { Rank = rank },
		};
	}

	private static InventoryItem CreateItem(
		int charge,
		int fusionedItem = 0,
		int itemId = 100,
		int objectId = 10,
		bool isEquipped = false)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = 1,
			Location = 0,
			Charge = charge,
			FusionedItem = fusionedItem,
			IsEquipped = isEquipped,
		};
	}

	private static ItemTemplateSummary CreateTemplate(
		int templateId,
		int level = 1,
		ItemImprovement? improvement = null,
		int recommendRank = 0)
	{
		return new ItemTemplateSummary(
			templateId,
			$"item_{templateId}",
			0,
			0,
			level,
			"SWORD",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			3,
			ConditioningMaxLevel: improvement?.Level ?? 0,
			Improvement: improvement,
			RecommendRank: recommendRank);
	}
}
