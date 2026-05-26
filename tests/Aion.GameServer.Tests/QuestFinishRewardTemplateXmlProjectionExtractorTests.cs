using System.Xml.Linq;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishRewardTemplateXmlProjectionExtractorTests
{
	private const int ExpectedRealDataTemplates = 8043;
	private const int ExpectedRealDataDefaultRegularNonItemRewardTemplates = 6832;
	private const int ExpectedRealDataDefaultRegularQuestRateIgnoredFieldTemplates = 103;
	private const int ExpectedRealDataDefaultRegularChallengeTaskTemplates = 174;
	private const long ExpectedRealDataDefaultRegularKinahTotal = 3425802649;
	private const long ExpectedRealDataDefaultRegularExperienceTotal = 20544638479;
	private const int ExpectedRealDataDefaultRegularItemRewardTemplates = 5527;
	private const int ExpectedRealDataDefaultRegularFixedRewardItemCount = 6182;
	private const int ExpectedRealDataDefaultRegularSelectableRewardItemCount = 5502;

	[Fact]
	public void ExtractDefaultRegularNonItemProjections_ReadsJavaRewardsAttributes()
	{
		const string xml = """
			<quests>
				<quest id="1001" reward_repeat_count="3" category="CHALLENGE_TASK">
					<rewards gold="123456789012" exp="400" ap="30" dp="40" gp="50" title="6"
					         extend_inventory="2" extend_stigma="7" ccheck="-1 9" icheck="12" />
					<rewards gold="1" exp="2" />
				</quest>
			</quests>
			""";
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();

		var projection = extractor.ExtractDefaultRegularNonItemProjections(xml)[1001];

		Assert.Equal(2, projection.RewardGroupCount);
		Assert.Equal(3, projection.RewardRepeatCount);
		Assert.True(projection.IsChallengeTask);
		Assert.True(projection.HasNonItemRewards);
		var nonItem = Assert.IsType<QuestFinishRewardNonItemTemplateProjection>(projection.NonItemProjection);
		Assert.Equal(123456789012, nonItem.Kinah);
		Assert.Equal(400, nonItem.Experience);
		Assert.Equal(30, nonItem.AbyssPoints);
		Assert.Equal(40, nonItem.DivinePoints);
		Assert.Equal(50, nonItem.GloryPoints);
		Assert.Equal(6, nonItem.Title);
		Assert.Equal(2, nonItem.ExtendInventory);
		Assert.Equal(7, nonItem.ExtendStigma);
		Assert.Equal([-1, 9], nonItem.CollectItemChecks);
		Assert.Equal(12, nonItem.InventoryItemCheck);
		Assert.False(projection.HasItemRewards);
	}

	[Fact]
	public void CreateProjection_UsesRequestedRegularRewardGroupIndexForNonItemAndItemRewards()
	{
		var quest = XElement.Parse("""
			<quest id="1001">
				<rewards exp="100">
					<reward_item item_id="182400001" count="9" />
				</rewards>
				<rewards gold="99" exp="200">
					<reward_item item_id="182400010" count="2" />
					<reward_item item_id="182400011" />
					<selectable_reward_item item_id="182400020" count="3" />
					<selectable_reward_item item_id="182400021" />
				</rewards>
			</quest>
			""");
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();

		var projection = extractor.CreateProjection(quest, rewardGroupIndex: 1);

		Assert.Equal(2, projection.RewardGroupCount);
		Assert.True(projection.HasNonItemRewards);
		Assert.True(projection.HasItemRewards);
		Assert.Equal(99, projection.NonItemProjection?.Kinah);
		Assert.Equal(200, projection.NonItemProjection?.Experience);
		var itemProjection = Assert.IsType<QuestFinishRewardItemTemplateProjection>(projection.ItemProjection);
		var rewardGroup = Assert.Single(itemProjection.RewardGroups);
		Assert.Equal(1, rewardGroup.RewardGroupIndex);
		Assert.Collection(
			rewardGroup.FixedRewardItems,
			item =>
			{
				Assert.Equal(182400010, item.ItemId);
				Assert.Equal(2, item.Count);
			},
			item =>
			{
				Assert.Equal(182400011, item.ItemId);
				Assert.Equal(1, item.Count);
			});
		Assert.Collection(
			rewardGroup.SelectableRewardItems,
			item =>
			{
				Assert.Equal(182400020, item.ItemId);
				Assert.Equal(3, item.Count);
			},
			item =>
			{
				Assert.Equal(182400021, item.ItemId);
				Assert.Equal(1, item.Count);
			});
		Assert.Null(itemProjection.ExtendedRewards);
		Assert.Empty(itemProjection.ClassSelectableRewards);
		Assert.False(itemProjection.HasBonus);
	}

	[Fact]
	public void CreateProjection_DefaultsMissingAndOutOfRangeRewardsToEmptyJavaRewards()
	{
		var missingRewards = XElement.Parse("""<quest id="1001" />""");
		var oneReward = XElement.Parse("""<quest id="1002"><rewards /></quest>""");
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();

		var missingProjection = extractor.CreateProjection(missingRewards, rewardGroupIndex: 0);
		var outOfRangeProjection = extractor.CreateProjection(oneReward, rewardGroupIndex: 5);

		Assert.Null(missingProjection.RewardGroupCount);
		Assert.False(missingProjection.HasNonItemRewards);
		Assert.False(missingProjection.HasItemRewards);
		Assert.Null(missingProjection.NonItemProjection);
		Assert.Null(missingProjection.ItemProjection);
		Assert.Equal(1, outOfRangeProjection.RewardGroupCount);
		Assert.False(outOfRangeProjection.HasNonItemRewards);
		Assert.False(outOfRangeProjection.HasItemRewards);
		Assert.Null(outOfRangeProjection.NonItemProjection);
		Assert.Null(outOfRangeProjection.ItemProjection);
	}

	[Fact]
	public void RealDataAudit_LoadsDefaultRegularNonItemRewardProjectionWithoutProductionWiring()
	{
		var repoRoot = FindRepoRoot();
		var questDataPath = Path.Combine(repoRoot, "game-server", "data", "static_data", "quest_data", "quest_data.xml");
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();

		using var stream = File.OpenRead(questDataPath);
		var projections = extractor.ExtractDefaultRegularNonItemProjections(stream);

		Assert.Equal(ExpectedRealDataTemplates, projections.Count);
		Assert.Equal(ExpectedRealDataDefaultRegularNonItemRewardTemplates, projections.Values.Count(projection => projection.HasNonItemRewards));
		Assert.Equal(
			ExpectedRealDataDefaultRegularQuestRateIgnoredFieldTemplates,
			projections.Values.Count(projection =>
				projection.NonItemProjection is { } nonItem
				&& (nonItem.ExtendStigma != 0
					|| nonItem.CollectItemChecks.Count != 0
					|| nonItem.InventoryItemCheck != 0)));
		Assert.Equal(ExpectedRealDataDefaultRegularChallengeTaskTemplates, projections.Values.Count(projection => projection.IsChallengeTask));
		Assert.Equal(ExpectedRealDataDefaultRegularKinahTotal, projections.Values.Sum(projection => projection.NonItemProjection?.Kinah ?? 0));
		Assert.Equal(ExpectedRealDataDefaultRegularExperienceTotal, projections.Values.Sum(projection => (long)(projection.NonItemProjection?.Experience ?? 0)));
		Assert.Equal(ExpectedRealDataDefaultRegularItemRewardTemplates, projections.Values.Count(projection => projection.HasItemRewards));
		Assert.Equal(
			ExpectedRealDataDefaultRegularFixedRewardItemCount,
			projections.Values.Sum(projection => projection.ItemProjection?.RewardGroups.Sum(group => group.FixedRewardItems.Count) ?? 0));
		Assert.Equal(
			ExpectedRealDataDefaultRegularSelectableRewardItemCount,
			projections.Values.Sum(projection => projection.ItemProjection?.RewardGroups.Sum(group => group.SelectableRewardItems.Count) ?? 0));
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
