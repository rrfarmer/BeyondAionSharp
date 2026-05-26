using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestBonusItemGroupTableTests
{
	[Fact]
	public void GetGroupsByBonusType_NormalizesTypeAndPreservesProjectionOrder()
	{
		var table = new QuestBonusItemGroupTable(
		[
			new QuestBonusItemGroupProjection(
				"food_low",
				"FOOD",
				10f,
				QuestBonusItemShape.FoodItem,
				[new QuestBonusItemProjection(160000001, Level: 20)]),
			new QuestBonusItemGroupProjection(
				"medicine_common",
				"MEDICINE",
				30f,
				QuestBonusItemShape.MedicineItem,
				[new QuestBonusItemProjection(162000001, Level: 20)]),
			new QuestBonusItemGroupProjection(
				"food_high",
				"FOOD",
				90f,
				QuestBonusItemShape.FoodItem,
				[new QuestBonusItemProjection(160000002, Level: 50)]),
		]);

		var foodGroups = table.GetGroupsByBonusType(" food ");

		Assert.Equal(3, table.Count);
		Assert.Equal(3, table.ItemCount);
		Assert.Contains("FOOD", table.BonusTypes);
		Assert.Equal(["food_low", "food_high"], foodGroups.Select(group => group.ElementName));
		Assert.Empty(table.GetGroupsByBonusType("TASK"));
	}

	[Fact]
	public void FromXml_LoadsOnlySupportedQuestBonusGroups()
	{
		const string xml = """
			<item_groups>
				<food bonusType="FOOD">
					<item id="160000001" level="20" />
				</food>
				<boss_rare bonusType="BOSS">
					<item id="100000001" level="50" />
				</boss_rare>
				<events bonusType="EVENTS">
					<item id="188000001" level="1" count="5" chance="100" />
					<item id="188000002" level="1" count="1" chance="50" />
				</events>
			</item_groups>
			""";

		var table = QuestBonusItemGroupTable.FromXml(xml);

		Assert.Equal(2, table.Count);
		Assert.Equal(3, table.ItemCount);
		Assert.Equal(["EVENTS", "FOOD"], table.BonusTypes.OrderBy(type => type, StringComparer.Ordinal));
		Assert.Single(table.GetGroupsByBonusType("FOOD"));
		Assert.Empty(table.GetGroupsByBonusType("BOSS"));
	}

	[Fact]
	public void FromXml_RealDataPreservesSupportedGroupAndItemCounts()
	{
		var repoRoot = FindRepoRoot();
		var itemGroupsPath = Path.Combine(repoRoot, "game-server", "data", "static_data", "items", "item_groups.xml");

		using var stream = File.OpenRead(itemGroupsPath);
		var table = QuestBonusItemGroupTable.FromXml(stream);

		Assert.Equal(12, table.Count);
		Assert.Equal(4701, table.ItemCount);
		Assert.Equal(4300, table.GetGroupsByBonusType("TASK").Sum(group => group.Items.Count));
		Assert.Equal(158, table.GetGroupsByBonusType("MANASTONE").Sum(group => group.Items.Count));
		Assert.Equal(30, table.GetGroupsByBonusType("MEDAL").Sum(group => group.Items.Count));
		Assert.Equal(116, table.GetGroupsByBonusType("FOOD").Sum(group => group.Items.Count));
		Assert.Equal(51, table.GetGroupsByBonusType("MEDICINE").Sum(group => group.Items.Count));
		Assert.Equal(46, table.GetGroupsByBonusType("EVENTS").Sum(group => group.Items.Count));
		Assert.Empty(table.GetGroupsByBonusType("BOSS"));
		Assert.Empty(table.GetGroupsByBonusType("GATHER"));
		Assert.Empty(table.GetGroupsByBonusType("ENCHANT"));
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
