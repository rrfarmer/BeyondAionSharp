using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishRewardProjectionLookupTableXmlFactoryTests
{
	private const int ExpectedRealDataTemplates = 8043;
	private const int ExpectedRealDataRewardGroupProjections = 8464;

	[Fact]
	public void Create_MaterializesEveryRegularRewardGroupForLookup()
	{
		const string xml = """
			<quests>
				<quest id="1001" can_report="true" reward_repeat_count="2">
					<rewards exp="100">
						<reward_item item_id="182400001" count="1" />
					</rewards>
					<rewards gold="55">
						<reward_item item_id="182400010" count="2" />
					</rewards>
				</quest>
				<quest id="1002" can_report="true">
					<extended_rewards gold="10">
						<reward_item item_id="186000001" count="3" />
					</extended_rewards>
				</quest>
			</quests>
			""";
		var factory = new QuestFinishRewardProjectionLookupTableXmlFactory();

		var table = factory.Create(xml);

		Assert.Equal(2, table.Count);
		Assert.True(table.TryGetQuest(1001, out var firstEntry));
		Assert.NotNull(firstEntry);
		Assert.Equal(2, firstEntry.RewardGroupProjections.Count);
		Assert.Equal(100, firstEntry.RewardGroupProjections[0].NonItemProjection?.Experience);
		Assert.Equal(55, firstEntry.RewardGroupProjections[1].NonItemProjection?.Kinah);
		Assert.True(table.TryGetQuest(1002, out var secondEntry));
		Assert.NotNull(secondEntry);
		var projection = Assert.Single(secondEntry.RewardGroupProjections);
		Assert.Equal(0, projection.Key);
		Assert.Equal(10, projection.Value.ExtendedNonItemProjection?.Kinah);
		Assert.NotNull(projection.Value.ItemProjection?.ExtendedRewards);
	}

	[Fact]
	public void RealDataAudit_MaterializesLookupTableWithoutProductionStaticDataExposure()
	{
		var repoRoot = FindRepoRoot();
		var questDataPath = Path.Combine(repoRoot, "game-server", "data", "static_data", "quest_data", "quest_data.xml");
		var factory = new QuestFinishRewardProjectionLookupTableXmlFactory();

		using var stream = File.OpenRead(questDataPath);
		var table = factory.Create(stream);

		Assert.Equal(ExpectedRealDataTemplates, table.Count);
		Assert.Equal(ExpectedRealDataRewardGroupProjections, table.Entries.Sum(entry => entry.RewardGroupProjections.Count));
		Assert.True(table.TryGetQuest(1007, out var multiGroupEntry));
		Assert.NotNull(multiGroupEntry);
		Assert.Equal(6, multiGroupEntry.RewardGroupProjections.Count);
		Assert.All(multiGroupEntry.RewardGroupProjections.Keys, rewardGroupIndex => Assert.InRange(rewardGroupIndex, 0, 5));
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
