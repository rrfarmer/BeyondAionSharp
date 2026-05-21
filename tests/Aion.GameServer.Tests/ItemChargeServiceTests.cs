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
	public void PlayerAbyssRank_AddApMatchesSoldierRankThresholds()
	{
		var rank = PlayerAbyssRank.Default() with { Ap = 200_000, Rank = 9, MaxRank = 9 };

		var updated = rank.AddAp(-60_000);

		Assert.Equal(140_000, updated.Ap);
		Assert.Equal(8, updated.Rank);
		Assert.Equal(9, updated.MaxRank);
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

	private static InventoryItem CreateItem(int charge, int fusionedItem = 0)
	{
		return new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100,
			Count = 1,
			Location = 0,
			Charge = charge,
			FusionedItem = fusionedItem,
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
