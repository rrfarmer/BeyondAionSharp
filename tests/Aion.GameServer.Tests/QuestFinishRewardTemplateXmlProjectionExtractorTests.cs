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
	private const int ExpectedRealDataAnyItemRewardTemplates = 6357;
	private const int ExpectedRealDataDefaultRegularItemRewardTemplates = 5527;
	private const int ExpectedRealDataDefaultRegularFixedRewardItemCount = 6182;
	private const int ExpectedRealDataDefaultRegularSelectableRewardItemCount = 5502;
	private const int ExpectedRealDataExtendedItemRewardTemplates = 233;
	private const int ExpectedRealDataExtendedFixedRewardItemCount = 245;
	private const int ExpectedRealDataExtendedSelectableRewardItemCount = 250;
	private const int ExpectedRealDataClassSelectableRewardTemplates = 80;
	private const int ExpectedRealDataClassSelectableRewardItemCount = 2324;
	private const int ExpectedRealDataClassRewardOnEveryRepeatTemplates = 69;
	private const int ExpectedRealDataSingleTimeClassRewardTemplates = 5;
	private const int ExpectedRealDataExtendedNonItemRewardTemplates = 82;
	private const long ExpectedRealDataExtendedKinahTotal = 1560700;
	private const int ExpectedRealDataExtendedTitleTemplates = 3;
	private const int ExpectedRealDataBonusTemplates = 782;

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
	public void CreateProjection_ReadsClassSelectableRewardsAndUseClassRewardFlags()
	{
		var quest = XElement.Parse("""
			<quest id="1001" use_class_reward="2">
				<rewards>
					<selectable_reward_item item_id="182400001" count="9" />
				</rewards>
				<fighter_selectable_reward item_id="100000001" count="2" />
				<fighter_selectable_reward item_id="100000002" />
				<knight_selectable_reward item_id="100900001" count="3" />
				<priest_selectable_reward item_id="101500001" count="4" />
				<elementalist_selectable_reward item_id="101300001" count="5" />
			</quest>
			""");
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();

		var projection = extractor.CreateProjection(quest, rewardGroupIndex: 0);

		Assert.True(projection.HasItemRewards);
		var itemProjection = Assert.IsType<QuestFinishRewardItemTemplateProjection>(projection.ItemProjection);
		Assert.True(itemProjection.SingleTimeClassReward);
		Assert.False(itemProjection.ClassRewardOnEveryRepeat);
		Assert.Collection(
			itemProjection.ClassSelectableRewards["GLADIATOR"],
			item =>
			{
				Assert.Equal(100000001, item.ItemId);
				Assert.Equal(2, item.Count);
			},
			item =>
			{
				Assert.Equal(100000002, item.ItemId);
				Assert.Equal(1, item.Count);
			});
		var templar = Assert.Single(itemProjection.ClassSelectableRewards["TEMPLAR"]);
		Assert.Equal(100900001, templar.ItemId);
		Assert.Equal(3, templar.Count);
		var cleric = Assert.Single(itemProjection.ClassSelectableRewards["CLERIC"]);
		Assert.Equal(101500001, cleric.ItemId);
		Assert.Equal(4, cleric.Count);
		var spiritMaster = Assert.Single(itemProjection.ClassSelectableRewards["SPIRIT_MASTER"]);
		Assert.Equal(101300001, spiritMaster.ItemId);
		Assert.Equal(5, spiritMaster.Count);
		Assert.DoesNotContain("WARRIOR", itemProjection.ClassSelectableRewards.Keys);
		Assert.DoesNotContain("PRIEST", itemProjection.ClassSelectableRewards.Keys);
		Assert.DoesNotContain("MAGE", itemProjection.ClassSelectableRewards.Keys);
	}

	[Fact]
	public void CreateProjection_ReadsQuestBonusMetadataWithoutRewardItems()
	{
		var quest = XElement.Parse("""
			<quest id="1001">
				<bonus level="40" type="MANASTONE" />
			</quest>
			""");
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();

		var projection = extractor.CreateProjection(quest, rewardGroupIndex: 0);

		Assert.True(projection.HasItemRewards);
		var itemProjection = Assert.IsType<QuestFinishRewardItemTemplateProjection>(projection.ItemProjection);
		Assert.True(itemProjection.HasBonus);
		var bonus = Assert.IsType<QuestFinishRewardBonusTemplateProjection>(itemProjection.BonusProjection);
		Assert.Equal("MANASTONE", bonus.BonusType);
		Assert.Equal(40, bonus.Level);
		Assert.Equal(QuestFinishRewardBonusSupportStatus.SupportedByJavaBonusService, bonus.SupportStatus);
		Assert.Empty(itemProjection.RewardGroups);
		Assert.Null(itemProjection.ExtendedRewards);
		Assert.Empty(itemProjection.ClassSelectableRewards);
	}

	[Fact]
	public void CreateProjection_ClassifiesUnsupportedAndSilentNoOpQuestBonusTypes()
	{
		var unsupportedQuest = XElement.Parse("""
			<quest id="1001">
				<bonus level="40" type="MAGICAL" />
			</quest>
			""");
		var silentNoOpQuest = XElement.Parse("""
			<quest id="1002">
				<bonus type="MOVIE" />
			</quest>
			""");
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();

		var unsupportedProjection = extractor.CreateProjection(unsupportedQuest, rewardGroupIndex: 0);
		var silentNoOpProjection = extractor.CreateProjection(silentNoOpQuest, rewardGroupIndex: 0);

		Assert.Equal(
			QuestFinishRewardBonusSupportStatus.UnsupportedByJavaBonusService,
			unsupportedProjection.ItemProjection?.BonusProjection?.SupportStatus);
		Assert.Equal(
			QuestFinishRewardBonusSupportStatus.SilentNoOpInJavaBonusService,
			silentNoOpProjection.ItemProjection?.BonusProjection?.SupportStatus);
	}

	[Fact]
	public void CreateProjection_ReadsExtendedRewardItemsWithoutRegularRewardGroup()
	{
		var quest = XElement.Parse("""
			<quest id="1001">
				<extended_rewards gold="500">
					<reward_item item_id="186000001" count="5" />
					<reward_item item_id="186000002" />
					<selectable_reward_item item_id="186000010" count="6" />
					<selectable_reward_item item_id="186000011" />
				</extended_rewards>
			</quest>
			""");
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();

		var projection = extractor.CreateProjection(quest, rewardGroupIndex: 0);

		Assert.Null(projection.RewardGroupCount);
		Assert.True(projection.HasItemRewards);
		Assert.True(projection.HasNonItemRewards);
		Assert.Null(projection.NonItemProjection);
		var extendedNonItem = Assert.IsType<QuestFinishRewardNonItemTemplateProjection>(projection.ExtendedNonItemProjection);
		Assert.Equal(500, extendedNonItem.Kinah);
		var itemProjection = Assert.IsType<QuestFinishRewardItemTemplateProjection>(projection.ItemProjection);
		Assert.Empty(itemProjection.RewardGroups);
		var extendedRewards = Assert.IsType<QuestFinishRewardGroupProjection>(itemProjection.ExtendedRewards);
		Assert.Equal(-1, extendedRewards.RewardGroupIndex);
		Assert.Collection(
			extendedRewards.FixedRewardItems,
			item =>
			{
				Assert.Equal(186000001, item.ItemId);
				Assert.Equal(5, item.Count);
			},
			item =>
			{
				Assert.Equal(186000002, item.ItemId);
				Assert.Equal(1, item.Count);
			});
		Assert.Collection(
			extendedRewards.SelectableRewardItems,
			item =>
			{
				Assert.Equal(186000010, item.ItemId);
				Assert.Equal(6, item.Count);
			},
			item =>
			{
				Assert.Equal(186000011, item.ItemId);
				Assert.Equal(1, item.Count);
			});
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
		Assert.Equal(ExpectedRealDataAnyItemRewardTemplates, projections.Values.Count(projection => projection.HasItemRewards));
		Assert.Equal(
			ExpectedRealDataDefaultRegularItemRewardTemplates,
			projections.Values.Count(projection =>
				projection.ItemProjection?.RewardGroups.Any(group =>
					group.FixedRewardItems.Count != 0 || group.SelectableRewardItems.Count != 0) == true));
		Assert.Equal(
			ExpectedRealDataDefaultRegularFixedRewardItemCount,
			projections.Values.Sum(projection => projection.ItemProjection?.RewardGroups.Sum(group => group.FixedRewardItems.Count) ?? 0));
		Assert.Equal(
			ExpectedRealDataDefaultRegularSelectableRewardItemCount,
			projections.Values.Sum(projection => projection.ItemProjection?.RewardGroups.Sum(group => group.SelectableRewardItems.Count) ?? 0));
		Assert.Equal(
			ExpectedRealDataExtendedItemRewardTemplates,
			projections.Values.Count(projection => projection.ItemProjection?.ExtendedRewards is not null));
		Assert.Equal(
			ExpectedRealDataExtendedFixedRewardItemCount,
			projections.Values.Sum(projection => projection.ItemProjection?.ExtendedRewards?.FixedRewardItems.Count ?? 0));
		Assert.Equal(
			ExpectedRealDataExtendedSelectableRewardItemCount,
			projections.Values.Sum(projection => projection.ItemProjection?.ExtendedRewards?.SelectableRewardItems.Count ?? 0));
		Assert.Equal(
			ExpectedRealDataClassSelectableRewardTemplates,
			projections.Values.Count(projection => projection.ItemProjection?.ClassSelectableRewards.Count > 0));
		Assert.Equal(
			ExpectedRealDataClassSelectableRewardItemCount,
			projections.Values.Sum(projection => projection.ItemProjection?.ClassSelectableRewards.Values.Sum(items => items.Count) ?? 0));
		Assert.Equal(
			ExpectedRealDataClassRewardOnEveryRepeatTemplates,
			projections.Values.Count(projection => projection.ItemProjection?.ClassRewardOnEveryRepeat == true));
		Assert.Equal(
			ExpectedRealDataSingleTimeClassRewardTemplates,
			projections.Values.Count(projection => projection.ItemProjection?.SingleTimeClassReward == true));
		Assert.Equal(
			ExpectedRealDataBonusTemplates,
			projections.Values.Count(projection => projection.ItemProjection?.HasBonus == true));
		Assert.Equal(
			ExpectedRealDataExtendedNonItemRewardTemplates,
			projections.Values.Count(projection =>
				projection.ExtendedNonItemProjection is { } nonItem
				&& (nonItem.Kinah != 0
					|| nonItem.Experience != 0
					|| nonItem.Title != 0
					|| nonItem.AbyssPoints != 0
					|| nonItem.DivinePoints != 0
					|| nonItem.GloryPoints != 0
					|| nonItem.ExtendInventory != 0
					|| nonItem.ExtendStigma != 0
					|| nonItem.CollectItemChecks.Count != 0
					|| nonItem.InventoryItemCheck != 0)));
		Assert.Equal(ExpectedRealDataExtendedKinahTotal, projections.Values.Sum(projection => projection.ExtendedNonItemProjection?.Kinah ?? 0));
		Assert.Equal(ExpectedRealDataExtendedTitleTemplates, projections.Values.Count(projection => projection.ExtendedNonItemProjection is { Title: not 0 }));
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
