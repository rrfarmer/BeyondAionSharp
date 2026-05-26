using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishRewardProjectionStaticDataBridgeTests
{
	[Fact]
	public async Task LoadFromCacheAsync_ExposesQuestFinishRewardProjectionLookupTableWithoutSocketWiring()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		await File.WriteAllTextAsync(
			cacheFile,
			"""
			<static_data>
				<events>
					<event id="1">
						<quest id="9999" />
					</event>
				</events>
				<quests>
					<quest id="1001" can_report="true" reward_repeat_count="2">
						<rewards exp="100">
							<reward_item item_id="182400001" count="1" />
						</rewards>
						<rewards gold="55">
							<reward_item item_id="182400010" count="2" />
						</rewards>
					</quest>
				</quests>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());

		Assert.Equal(1, staticData.QuestFinishRewardProjections.Count);
		Assert.False(staticData.QuestFinishRewardProjections.TryGetQuest(9999, out _));
		Assert.True(staticData.QuestFinishRewardProjections.TryGetQuest(1001, out var entry));
		Assert.NotNull(entry);
		Assert.Equal(2, entry.RewardGroupProjections.Count);
		Assert.Equal(100, entry.RewardGroupProjections[0].NonItemProjection?.Experience);
		Assert.Equal(55, entry.RewardGroupProjections[1].NonItemProjection?.Kinah);

		var plan = QuestFinishRewardProjectionLookupPlanService.CreatePlan(
			new QuestFinishRewardProjectionLookupInput(
				QuestId: 1001,
				DialogActionId: 8,
				ExtendedRewardIndex: null,
				CompleteCount: 0,
				CorrectedRewardGroup: 1),
			staticData.QuestFinishRewardProjections);

		Assert.Equal(QuestFinishRewardProjectionLookupStatus.Found, plan.Status);
		Assert.Equal(55, plan.Projection?.NonItemProjection?.Kinah);
	}

	[Fact]
	public async Task LoadStaticDataAsync_RealDataExposesQuestFinishRewardProjectionLookupTable()
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

		Assert.Equal(8043, staticData.QuestFinishRewardProjections.Count);
		Assert.Equal(8464, staticData.QuestFinishRewardProjections.Entries.Sum(entry => entry.RewardGroupProjections.Count));
		Assert.True(staticData.QuestFinishRewardProjections.TryGetQuest(1007, out var multiGroupEntry));
		Assert.NotNull(multiGroupEntry);
		Assert.Equal(6, multiGroupEntry.RewardGroupProjections.Count);
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
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-quest-finish-reward-static-data-" + Guid.NewGuid().ToString("N"));
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
