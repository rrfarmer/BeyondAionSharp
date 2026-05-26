using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;

namespace Aion.GameServer.Tests;

public sealed class QuestBonusStaticDataBridgeTests
{
	[Fact]
	public async Task LoadFromCacheAsync_ExposesSupportedQuestBonusItemGroups()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		await File.WriteAllTextAsync(
			cacheFile,
			"""
			<static_data>
				<item_groups>
					<food bonusType="FOOD">
						<item id="160000001" level="20" />
					</food>
					<boss_rare bonusType="BOSS">
						<item id="100000001" level="50" />
					</boss_rare>
					<events bonusType="EVENTS" chance="25">
						<item id="188000001" level="1" count="5" chance="100" />
						<item id="188000002" level="1" count="1" chance="50" />
					</events>
				</item_groups>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());

		Assert.Equal(2, staticData.QuestBonusItemGroups.Count);
		Assert.Equal(3, staticData.QuestBonusItemGroups.ItemCount);
		Assert.Equal(4, staticData.GetElementCount("item"));
		var events = Assert.Single(staticData.QuestBonusItemGroups.GetGroupsByBonusType("EVENTS"));
		Assert.Equal("events", events.ElementName);
		Assert.Equal(25f, events.Chance);
		Assert.Equal(2, events.Items.Count);
		Assert.Empty(staticData.QuestBonusItemGroups.GetGroupsByBonusType("BOSS"));
	}

	[Fact]
	public async Task LoadStaticDataAsync_RealDataExposesQuestBonusItemGroupTable()
	{
		var repoRoot = FindRepoRoot();
		using var temp = TempDirectory.Create();
		var staticDataPath = Path.Combine(repoRoot, "game-server", "data", "static_data");

		var staticData = await XmlDataLoader.LoadStaticDataAsync(
			new XmlDataLoaderOptions
			{
				MainXmlFilePath = Path.Combine(staticDataPath, "static_data.xml"),
				CacheXmlFilePath = Path.Combine(temp.Path, "static_data.xml"),
				SchemaFilePath = Path.Combine(staticDataPath, "static_data.xsd"),
				ValidateWhenCacheChanges = false,
			});

		Assert.Equal(12, staticData.QuestBonusItemGroups.Count);
		Assert.Equal(4701, staticData.QuestBonusItemGroups.ItemCount);
		Assert.Equal(4300, staticData.QuestBonusItemGroups.GetGroupsByBonusType("TASK").Sum(group => group.Items.Count));
		Assert.Equal(158, staticData.QuestBonusItemGroups.GetGroupsByBonusType("MANASTONE").Sum(group => group.Items.Count));
		Assert.Equal(30, staticData.QuestBonusItemGroups.GetGroupsByBonusType("MEDAL").Sum(group => group.Items.Count));
		Assert.Equal(116, staticData.QuestBonusItemGroups.GetGroupsByBonusType("FOOD").Sum(group => group.Items.Count));
		Assert.Equal(51, staticData.QuestBonusItemGroups.GetGroupsByBonusType("MEDICINE").Sum(group => group.Items.Count));
		Assert.Equal(46, staticData.QuestBonusItemGroups.GetGroupsByBonusType("EVENTS").Sum(group => group.Items.Count));
		Assert.Empty(staticData.QuestBonusItemGroups.GetGroupsByBonusType("BOSS"));
		Assert.Empty(staticData.QuestBonusItemGroups.GetGroupsByBonusType("GATHER"));
		Assert.Empty(staticData.QuestBonusItemGroups.GetGroupsByBonusType("ENCHANT"));
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

	private sealed class TempDirectory : IDisposable
	{
		private TempDirectory(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public static TempDirectory Create()
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-quest-bonus-static-data-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return new TempDirectory(path);
		}

		public void Dispose()
		{
			if (Directory.Exists(Path))
				Directory.Delete(Path, recursive: true);
		}
	}
}
