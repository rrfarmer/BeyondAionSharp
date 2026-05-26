using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestBonusCandidatePlanServiceTests
{
	[Fact]
	public void CreatePlan_FiltersRaceAndBonusLevelLikeJavaItemRaceEntryAndIdLevelReward()
	{
		var service = new QuestBonusCandidatePlanService();
		var groups = new[]
		{
			new QuestBonusItemGroupProjection(
				"manastones_common",
				"MANASTONE",
				80f,
				QuestBonusItemShape.ItemRaceEntry,
				[
					new QuestBonusItemProjection(167000001),
					new QuestBonusItemProjection(167000002),
					new QuestBonusItemProjection(167000003, Race: "ASMODIANS"),
					new QuestBonusItemProjection(167000004),
				]),
			new QuestBonusItemGroupProjection(
				"medals",
				"MEDAL",
				100f,
				QuestBonusItemShape.FullRewardItem,
				[
					new QuestBonusItemProjection(186000030, Level: 50, Count: 2, Chance: 25f),
					new QuestBonusItemProjection(186000031, Level: 55, Count: 1, Chance: 75f),
				]),
		};
		var itemTemplates = CreateTemplates(
			Template(167000001, "PC_ALL", 40),
			Template(167000002, "PC_ALL", 41),
			Template(167000003, "PC_ALL", 40),
			Template(167000004, "ASMODIANS", 40),
			Template(186000030, "PC_ALL", 1),
			Template(186000031, "PC_ALL", 1));

		var plan = service.CreatePlan(
			new QuestBonusCandidatePlanInput("MANASTONE", BonusLevel: 40, PlayerRace: "ELYOS"),
			groups,
			itemTemplates);

		var group = Assert.Single(plan.CandidateGroups);
		Assert.Equal("manastones_common", group.ElementName);
		var candidate = Assert.Single(group.Items);
		Assert.Equal(167000001, candidate.ItemId);
		Assert.Equal(100f, candidate.EffectiveChance);
		Assert.Equal(1L, candidate.CountMin);
		Assert.Equal(1L, candidate.CountMax);
		Assert.Equal(QuestBonusCandidateCountMode.Fixed, candidate.CountMode);
		Assert.Equal(3, plan.SkippedItems.Count);
		Assert.Contains(plan.SkippedItems, item => item.ItemId == 167000002 && item.Reason == QuestBonusCandidateSkipReason.BonusLevelMismatch);
		Assert.Contains(plan.SkippedItems, item => item.ItemId == 167000003 && item.Reason == QuestBonusCandidateSkipReason.XmlRaceMismatch);
		Assert.Contains(plan.SkippedItems, item => item.ItemId == 167000004 && item.Reason == QuestBonusCandidateSkipReason.TemplateRaceMismatch);
		Assert.DoesNotContain(plan.SkippedItems, item => item.BonusType == "MEDAL");
	}

	[Fact]
	public void CreatePlan_FiltersCraftGroupsBySkillAndSkillPointLikeJavaCraftRewards()
	{
		var service = new QuestBonusCandidatePlanService();
		var groups = new[]
		{
			new QuestBonusItemGroupProjection(
				"craft_materials",
				"TASK",
				50f,
				QuestBonusItemShape.CraftItem,
				[
					new QuestBonusItemProjection(152020112, Skill: 40007, MinLevel: 5, MaxLevel: 40),
					new QuestBonusItemProjection(152020113, Skill: 40008, MinLevel: 5, MaxLevel: 40),
					new QuestBonusItemProjection(152020114, Skill: 40007, MinLevel: 31, MaxLevel: 60),
					new QuestBonusItemProjection(152020115, Skill: 40007, MinLevel: 1, MaxLevel: 29),
				]),
			new QuestBonusItemGroupProjection(
				"craft_recipes",
				"TASK",
				50f,
				QuestBonusItemShape.CraftRecipe,
				[
					new QuestBonusItemProjection(155000001, Level: 10, Skill: 40007),
					new QuestBonusItemProjection(155000002, Level: 100, Skill: 40007),
					new QuestBonusItemProjection(155000003, Level: 200, Skill: 40008),
				]),
		};
		var itemTemplates = CreateTemplates(
			Template(152020112, "PC_ALL", 1),
			Template(152020113, "PC_ALL", 1),
			Template(152020114, "PC_ALL", 1),
			Template(152020115, "PC_ALL", 1),
			Template(155000001, "PC_ALL", 1),
			Template(155000002, "PC_ALL", 1),
			Template(155000003, "PC_ALL", 1));

		var plan = service.CreatePlan(
			new QuestBonusCandidatePlanInput("TASK", BonusLevel: 0, PlayerRace: "ELYOS", CombineSkill: 40007, CombineSkillPoint: 30),
			groups,
			itemTemplates);

		Assert.Equal(2, plan.CandidateGroups.Count);
		var material = Assert.Single(plan.CandidateGroups, group => group.ElementName == "craft_materials").Items.Single();
		Assert.Equal(152020112, material.ItemId);
		Assert.Equal(3L, material.CountMin);
		Assert.Equal(5L, material.CountMax);
		Assert.Equal(QuestBonusCandidateCountMode.RandomInclusiveRange, material.CountMode);
		var recipe = Assert.Single(plan.CandidateGroups, group => group.ElementName == "craft_recipes").Items.Single();
		Assert.Equal(155000001, recipe.ItemId);
		Assert.Equal(1L, recipe.CountMin);
		Assert.Equal(1L, recipe.CountMax);
		Assert.Contains(plan.SkippedItems, item => item.ItemId == 152020113 && item.Reason == QuestBonusCandidateSkipReason.CraftSkillMismatch);
		Assert.Contains(plan.SkippedItems, item => item.ItemId == 152020114 && item.Reason == QuestBonusCandidateSkipReason.CraftSkillPointTooLow);
		Assert.Contains(plan.SkippedItems, item => item.ItemId == 152020115 && item.Reason == QuestBonusCandidateSkipReason.CraftSkillPointTooHigh);
		Assert.Contains(plan.SkippedItems, item => item.ItemId == 155000002 && item.Reason == QuestBonusCandidateSkipReason.CraftSkillPointTooLow);
		Assert.Contains(plan.SkippedItems, item => item.ItemId == 155000003 && item.Reason == QuestBonusCandidateSkipReason.CraftSkillMismatch);
	}

	[Fact]
	public void CreatePlan_ReportsFullFoodMedicineCountAndChanceMetadataWithoutSelectingRewards()
	{
		var service = new QuestBonusCandidatePlanService();
		var groups = new[]
		{
			new QuestBonusItemGroupProjection(
				"medals",
				"MEDAL",
				100f,
				QuestBonusItemShape.FullRewardItem,
				[new QuestBonusItemProjection(186000030, Level: 50, Count: 2, Chance: 25f)]),
			new QuestBonusItemGroupProjection(
				"food",
				"FOOD",
				100f,
				QuestBonusItemShape.FoodItem,
				[new QuestBonusItemProjection(160000001, Level: 50)]),
			new QuestBonusItemGroupProjection(
				"medicine_common",
				"MEDICINE",
				100f,
				QuestBonusItemShape.MedicineItem,
				[new QuestBonusItemProjection(162000001, Level: 50)]),
		};
		var itemTemplates = CreateTemplates(
			Template(186000030, "PC_ALL", 1),
			Template(160000001, "PC_ALL", 1),
			Template(162000001, "PC_ALL", 1));

		var medalPlan = service.CreatePlan(new QuestBonusCandidatePlanInput("MEDAL", 50, "ELYOS"), groups, itemTemplates);
		var foodPlan = service.CreatePlan(new QuestBonusCandidatePlanInput("FOOD", 50, "ELYOS"), groups, itemTemplates);
		var medicinePlan = service.CreatePlan(new QuestBonusCandidatePlanInput("MEDICINE", 50, "ELYOS"), groups, itemTemplates);

		var medal = Assert.Single(Assert.Single(medalPlan.CandidateGroups).Items);
		Assert.Equal(25f, medal.EffectiveChance);
		Assert.Equal(2L, medal.CountMin);
		Assert.Equal(2L, medal.CountMax);
		Assert.Equal(QuestBonusCandidateCountMode.Fixed, medal.CountMode);
		var food = Assert.Single(Assert.Single(foodPlan.CandidateGroups).Items);
		Assert.Equal(5L, food.CountMin);
		Assert.Equal(10L, food.CountMax);
		Assert.Equal(QuestBonusCandidateCountMode.RandomChoice, food.CountMode);
		var medicine = Assert.Single(Assert.Single(medicinePlan.CandidateGroups).Items);
		Assert.Equal(1L, medicine.CountMin);
		Assert.Equal(3L, medicine.CountMax);
		Assert.Equal(QuestBonusCandidateCountMode.RandomInclusiveRange, medicine.CountMode);
	}

	[Fact]
	public async Task RealDataAudit_ComputesDeterministicCandidatePoolForSupportedBonusTypes()
	{
		var repoRoot = FindRepoRoot();
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-bonus-candidates-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		var staticDataPath = Path.Combine(repoRoot, "game-server", "data", "static_data");
		try
		{
			var staticData = await XmlDataLoader.LoadStaticDataAsync(
				new XmlDataLoaderOptions
				{
					MainXmlFilePath = Path.Combine(staticDataPath, "static_data.xml"),
					CacheXmlFilePath = Path.Combine(tempPath, "static_data.xml"),
					SchemaFilePath = Path.Combine(staticDataPath, "static_data.xsd"),
					ValidateWhenCacheChanges = false,
				});
			var itemGroupsPath = Path.Combine(repoRoot, "game-server", "data", "static_data", "items", "item_groups.xml");
			using var stream = File.OpenRead(itemGroupsPath);
			var groups = new QuestBonusItemGroupXmlProjectionExtractor().ExtractSupportedGroups(stream);
			var service = new QuestBonusCandidatePlanService();

			var manastonePlan = service.CreatePlan(new QuestBonusCandidatePlanInput("MANASTONE", 60, "ELYOS"), groups, staticData.ItemTemplates);
			var craftPlan = service.CreatePlan(new QuestBonusCandidatePlanInput("TASK", 0, "ELYOS", CombineSkill: 40007, CombineSkillPoint: 100), groups, staticData.ItemTemplates);

			Assert.True(manastonePlan.CandidateItemCount > 0);
			Assert.True(craftPlan.CandidateItemCount > 0);
			Assert.DoesNotContain(manastonePlan.SkippedItems, item => item.Reason == QuestBonusCandidateSkipReason.MissingItemTemplate);
			Assert.DoesNotContain(craftPlan.SkippedItems, item => item.Reason == QuestBonusCandidateSkipReason.MissingItemTemplate);
			Assert.All(manastonePlan.CandidateGroups, group => Assert.Equal("MANASTONE", group.BonusType));
			Assert.All(craftPlan.CandidateGroups, group => Assert.Equal("TASK", group.BonusType));
		}
		finally
		{
			Directory.Delete(tempPath, recursive: true);
		}
	}

	private static ItemTemplateTable CreateTemplates(params ItemTemplateSummary[] templates) => new(templates);

	private static ItemTemplateSummary Template(int itemId, string race, int level) =>
		new(itemId, $"Item {itemId}", 0, 0, level, "NONE", "NORMAL", "COMMON", race, 100, 0, 0);

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "game-server"))
				&& Directory.Exists(Path.Combine(directory.FullName, "dotnetConversion")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Unable to locate repository root.");
	}
}
