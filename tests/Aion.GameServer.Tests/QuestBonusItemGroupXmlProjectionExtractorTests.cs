using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestBonusItemGroupXmlProjectionExtractorTests
{
	private const int ExpectedRealDataSupportedGroupCount = 12;
	private const int ExpectedRealDataSupportedItemCount = 4701;
	private const int ExpectedRealDataTaskItemCount = 4300;
	private const int ExpectedRealDataManastoneItemCount = 158;
	private const int ExpectedRealDataMedalItemCount = 30;
	private const int ExpectedRealDataFoodItemCount = 116;
	private const int ExpectedRealDataMedicineItemCount = 51;
	private const int ExpectedRealDataEventsItemCount = 46;

	[Fact]
	public void ExtractSupportedGroups_ReadsJavaLiveBonusGroupShapesAndRawItemAttributes()
	{
		const string xml = """
			<item_groups>
				<craft_materials bonusType="TASK" chance="47">
					<item id="152020112" skill="40007" minLevel="5" maxLevel="40" race="ELYOS" />
				</craft_materials>
				<manastones_common bonusType="MANASTONE" chance="95">
					<item id="167000001" />
				</manastones_common>
				<medals bonusType="MEDAL">
					<item id="186000030" level="50" count="2" chance="25" race="ASMODIANS" />
				</medals>
				<food bonusType="FOOD">
					<item id="160000001" level="20" />
				</food>
				<medicine_rare bonusType="MEDICINE" chance="20">
					<item id="162000001" level="30" />
				</medicine_rare>
				<events bonusType="EVENTS">
					<item id="188000001" level="1" count="5" chance="100" />
				</events>
				<boss_rare bonusType="BOSS">
					<item id="100000001" level="50" />
				</boss_rare>
			</item_groups>
			""";
		var extractor = new QuestBonusItemGroupXmlProjectionExtractor();

		var groups = extractor.ExtractSupportedGroups(xml);

		Assert.Equal(6, groups.Count);
		Assert.DoesNotContain(groups, group => group.ElementName == "boss_rare");
		Assert.Collection(
			groups,
			group =>
			{
				Assert.Equal("craft_materials", group.ElementName);
				Assert.Equal("TASK", group.BonusType);
				Assert.Equal(47f, group.Chance);
				Assert.Equal(QuestBonusItemShape.CraftItem, group.ItemShape);
				var item = Assert.Single(group.Items);
				Assert.Equal(152020112, item.ItemId);
				Assert.Equal("ELYOS", item.Race);
				Assert.Equal(40007, item.Skill);
				Assert.Equal(5, item.MinLevel);
				Assert.Equal(40, item.MaxLevel);
			},
			group =>
			{
				Assert.Equal("manastones_common", group.ElementName);
				Assert.Equal(QuestBonusItemShape.ItemRaceEntry, group.ItemShape);
				Assert.Equal(95f, group.Chance);
				Assert.Null(Assert.Single(group.Items).Count);
			},
			group =>
			{
				Assert.Equal("medals", group.ElementName);
				Assert.Equal(QuestBonusItemShape.FullRewardItem, group.ItemShape);
				Assert.Equal(100f, group.Chance);
				var item = Assert.Single(group.Items);
				Assert.Equal(50, item.Level);
				Assert.Equal(2, item.Count);
				Assert.Equal(25f, item.Chance);
				Assert.Equal("ASMODIANS", item.Race);
			},
			group =>
			{
				Assert.Equal("food", group.ElementName);
				Assert.Equal(QuestBonusItemShape.FoodItem, group.ItemShape);
				Assert.Equal(20, Assert.Single(group.Items).Level);
			},
			group =>
			{
				Assert.Equal("medicine_rare", group.ElementName);
				Assert.Equal(QuestBonusItemShape.MedicineItem, group.ItemShape);
				Assert.Equal(20f, group.Chance);
				Assert.Equal(30, Assert.Single(group.Items).Level);
			},
			group =>
			{
				Assert.Equal("events", group.ElementName);
				Assert.Equal("EVENTS", group.BonusType);
				Assert.Equal(QuestBonusItemShape.FullRewardItem, group.ItemShape);
			});
	}

	[Fact]
	public void RealDataAudit_LoadsSupportedJavaBonusGroupsWithoutSelection()
	{
		var repoRoot = FindRepoRoot();
		var itemGroupsPath = Path.Combine(repoRoot, "game-server", "data", "static_data", "items", "item_groups.xml");
		var extractor = new QuestBonusItemGroupXmlProjectionExtractor();

		using var stream = File.OpenRead(itemGroupsPath);
		var groups = extractor.ExtractSupportedGroups(stream);

		Assert.Equal(ExpectedRealDataSupportedGroupCount, groups.Count);
		Assert.Equal(ExpectedRealDataSupportedItemCount, groups.Sum(group => group.Items.Count));
		Assert.Equal(ExpectedRealDataTaskItemCount, groups.Where(group => group.BonusType == "TASK").Sum(group => group.Items.Count));
		Assert.Equal(ExpectedRealDataManastoneItemCount, groups.Where(group => group.BonusType == "MANASTONE").Sum(group => group.Items.Count));
		Assert.Equal(ExpectedRealDataMedalItemCount, groups.Where(group => group.BonusType == "MEDAL").Sum(group => group.Items.Count));
		Assert.Equal(ExpectedRealDataFoodItemCount, groups.Where(group => group.BonusType == "FOOD").Sum(group => group.Items.Count));
		Assert.Equal(ExpectedRealDataMedicineItemCount, groups.Where(group => group.BonusType == "MEDICINE").Sum(group => group.Items.Count));
		Assert.Equal(ExpectedRealDataEventsItemCount, groups.Where(group => group.BonusType == "EVENTS").Sum(group => group.Items.Count));
		Assert.DoesNotContain(groups, group => group.BonusType is "BOSS" or "GATHER" or "ENCHANT");
	}

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
