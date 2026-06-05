using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class NearbyQuestTemplateXmlExtractorTests
{
	private const int ExpectedRealDataTemplates = 8043;
	private const int ExpectedRealDataXmlStartConditionTemplates = 2427;
	private const int ExpectedRealDataInventoryItemTemplates = 363;
	private const int ExpectedRealDataCombineSkillTemplates = 628;
	private const int ExpectedRealDataNpcFactionTemplates = 365;
	private const int ExpectedRealDataTimeBasedTemplates = 927;
	private const int ExpectedRealDataRaceRestrictedTemplates = 7431;
	private const int ExpectedRealDataClassRestrictedTemplates = 483;
	private const int ExpectedRealDataGenderRestrictedTemplates = 18;
	private const int ExpectedRealDataRankRestrictedTemplates = 12;
	private const int ExpectedRealDataMinLevelTemplates = 8043;
	private const int ExpectedRealDataMaxLevelTemplates = 1355;
	private const int ExpectedRealDataMaxClassListCount = 16;
	private const int ExpectedRealDataReportableTemplates = 64;
	private const int ExpectedRealDataRewardRepeatTemplates = 241;
	private const int ExpectedRealDataRewardTemplates = 7935;
	private const int ExpectedRealDataExtendedRewardTemplates = 235;
	private const int ExpectedRealDataBonusTemplates = 782;
	private const int ExpectedRealDataQuestWorkItemTemplates = 1630;
	private const int ExpectedRealDataCannotShareTemplates = 4876;
	private const int ExpectedRealDataCannotGiveupTemplates = 347;
	private const int ExpectedRealDataTimerTemplates = 0;
	private const int ExpectedRealDataAllianceTargetTemplates = 159;
	private const int ExpectedRealDataAreaTargetTemplates = 30;
	private const int ExpectedRealDataLeagueTargetTemplates = 59;

	[Fact]
	public void Extract_ReadsNearbyPredicateQuestTemplateFieldsLikeJavaQuestTemplate()
	{
		const string xml = """
			<quests>
				<quest id="1001" minlevel_permitted="19" maxlevel_permitted="45" race_permitted="ELYOS" rank="4"
				       max_repeat_count="3" combineskill="40001" combine_skillpoint="199" npcfaction_id="12"
				       category="TASK" mentor_type="MENTOR" repeat_cycle="Mon Wed"
				       can_report="true" reward_repeat_count="2" cannot_share="true" cannot_giveup="true"
				       timer="true" target="ALLIANCE">
					<class_permitted>
						GLADIATOR CLERIC
					</class_permitted>
					<gender_permitted>MALE</gender_permitted>
					<inventory_items>
						<inventory_item item_id="182200001" count="1" />
						<inventory_item item_id="182200002" />
					</inventory_items>
					<quest_work_items>
						<quest_work_item item_id="182200003" count="1" />
					</quest_work_items>
					<start_conditions>
						<finished quest_id="1000" reward="2" />
						<unfinished>1002 1003</unfinished>
						<noacquired>1004</noacquired>
						<acquired>1005</acquired>
						<equipped>110101001</equipped>
						<required_title>7</required_title>
					</start_conditions>
					<rewards exp="200" />
					<extended_rewards exp="300" />
					<bonus />
				</quest>
			</quests>
			""";
		var extractor = new NearbyQuestTemplateXmlExtractor();

		var template = Assert.Single(extractor.Extract(xml));

		Assert.Equal(1001, template.QuestId);
		Assert.Equal(19, template.MinLevelPermitted);
		Assert.Equal(45, template.MaxLevelPermitted);
		Assert.Equal("ELYOS", template.RacePermitted);
		Assert.Equal(["CLERIC", "GLADIATOR"], template.ClassPermitted.Order());
		Assert.Equal("MALE", template.GenderPermitted);
		Assert.Equal(4, template.RequiredRank);
		Assert.Equal(3, template.MaxRepeatCount);
		Assert.True(template.IsTimeBased);
		Assert.Equal(["MON", "WED"], template.RepeatCycle);
		Assert.True(template.HasXmlStartConditions);
		var startCondition = Assert.Single(template.XmlStartConditions);
		var finished = Assert.Single(startCondition.Finished);
		Assert.Equal(1000, finished.QuestId);
		Assert.Equal(2, finished.Reward);
		Assert.Equal([1002, 1003], startCondition.Unfinished.Order());
		Assert.Equal([1004], startCondition.NoAcquired.Order());
		Assert.Equal([1005], startCondition.Acquired.Order());
		Assert.Equal([110101001], startCondition.Equipped.Order());
		Assert.Equal(7, startCondition.RequiredTitle);
		Assert.False(template.HasUnsupportedXmlStartConditionElements);
		Assert.True(template.HasInventoryItems);
		Assert.Equal([182200001, 182200002], template.InventoryItems.Select(item => item.ItemId).Order());
		Assert.Equal(1, template.InventoryItems.Single(item => item.ItemId == 182200001).Count);
		Assert.Null(template.InventoryItems.Single(item => item.ItemId == 182200002).Count);
		Assert.Equal(40001, template.CombineSkill);
		Assert.Equal(199, template.CombineSkillPoint);
		Assert.Equal("TASK", template.QuestCategory);
		Assert.Equal(12, template.NpcFactionId);
		Assert.True(template.IsMentorQuest);
		Assert.True(template.CanReport);
		Assert.Equal(2, template.RewardRepeatCount);
		Assert.True(template.HasRewards);
		Assert.True(template.HasExtendedRewards);
		Assert.True(template.HasBonus);
		Assert.True(template.HasQuestWorkItems);
		Assert.True(template.CannotShare);
		Assert.True(template.CannotGiveup);
		Assert.True(template.IsTimer);
		Assert.Equal("ALLIANCE", template.Target);
	}

	[Fact]
	public void Extract_AppliesJavaQuestTemplateDefaultsForMissingOptionalFields()
	{
		const string xml = """
			<quests>
				<quest id="2001" />
			</quests>
			""";
		var extractor = new NearbyQuestTemplateXmlExtractor();

		var template = Assert.Single(extractor.Extract(xml));

		Assert.Equal(2001, template.QuestId);
		Assert.Equal(0, template.MinLevelPermitted);
		Assert.Equal(0, template.MaxLevelPermitted);
		Assert.Equal(string.Empty, template.RacePermitted);
		Assert.Empty(template.ClassPermitted);
		Assert.Equal(string.Empty, template.GenderPermitted);
		Assert.Equal(0, template.RequiredRank);
		Assert.Equal(1, template.MaxRepeatCount);
		Assert.False(template.IsTimeBased);
		Assert.Empty(template.RepeatCycle);
		Assert.False(template.HasXmlStartConditions);
		Assert.Empty(template.XmlStartConditions);
		Assert.False(template.HasUnsupportedXmlStartConditionElements);
		Assert.False(template.HasInventoryItems);
		Assert.Empty(template.InventoryItems);
		Assert.Equal(0, template.CombineSkill);
		Assert.Equal(0, template.CombineSkillPoint);
		Assert.Equal("QUEST", template.QuestCategory);
		Assert.Equal(0, template.NpcFactionId);
		Assert.False(template.IsMentorQuest);
		Assert.False(template.CanReport);
		Assert.Equal(0, template.RewardRepeatCount);
		Assert.False(template.HasRewards);
		Assert.False(template.HasExtendedRewards);
		Assert.False(template.HasBonus);
		Assert.False(template.HasQuestWorkItems);
		Assert.False(template.CannotShare);
		Assert.False(template.CannotGiveup);
		Assert.False(template.IsTimer);
		Assert.Equal("NONE", template.Target);
	}

	[Fact]
	public void Extract_MarksUnknownXmlStartConditionChildrenUnsupported()
	{
		const string xml = """
			<quests>
				<quest id="2501">
					<start_conditions>
						<finished quest_id="2500" />
						<future_condition>1</future_condition>
					</start_conditions>
				</quest>
			</quests>
			""";
		var extractor = new NearbyQuestTemplateXmlExtractor();

		var template = Assert.Single(extractor.Extract(xml));

		Assert.True(template.HasXmlStartConditions);
		Assert.True(template.HasUnsupportedXmlStartConditionElements);
		Assert.Single(template.XmlStartConditions);
	}

	[Fact]
	public void Extract_StreamInputFeedsNearbyQuestTemplateTableAndPredicate()
	{
		const string xml = """
			<static_data>
				<events>
					<event id="1">
						<quest id="9999" />
					</event>
				</events>
				<quests>
					<quest id="3001" minlevel_permitted="22" race_permitted="PC_ALL" />
				</quests>
			</static_data>
			""";
		var extractor = new NearbyQuestTemplateXmlExtractor();
		using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

		var table = new NearbyQuestTemplateTable(extractor.Extract(stream));

		Assert.True(table.TryGetQuest(3001, out var template));
		Assert.NotNull(template);
		Assert.Equal(22, template.MinLevelPermitted);
		Assert.False(table.TryGetQuest(9999, out _));
	}

	[Fact]
	public void RealDataAudit_LoadsNearbyQuestTemplateSummariesWithoutProductionWiring()
	{
		var repoRoot = FindRepoRoot();
		var questDataPath = Path.Combine(repoRoot, "game-server", "data", "static_data", "quest_data", "quest_data.xml");
		var extractor = new NearbyQuestTemplateXmlExtractor();

		using var stream = File.OpenRead(questDataPath);
		var templates = extractor.Extract(stream);

		Assert.Equal(ExpectedRealDataTemplates, templates.Count);
		Assert.Equal(ExpectedRealDataXmlStartConditionTemplates, templates.Count(template => template.HasXmlStartConditions));
		Assert.Equal(ExpectedRealDataInventoryItemTemplates, templates.Count(template => template.HasInventoryItems));
		Assert.Equal(ExpectedRealDataCombineSkillTemplates, templates.Count(template => template.CombineSkill != 0));
		Assert.Equal(ExpectedRealDataNpcFactionTemplates, templates.Count(template => template.NpcFactionId != 0));
		Assert.Equal(ExpectedRealDataTimeBasedTemplates, templates.Count(template => template.IsTimeBased));
		Assert.Equal(ExpectedRealDataRaceRestrictedTemplates, templates.Count(template => !string.IsNullOrWhiteSpace(template.RacePermitted) && template.RacePermitted != "PC_ALL"));
		Assert.Equal(ExpectedRealDataClassRestrictedTemplates, templates.Count(template => template.ClassPermitted.Count != 0));
		Assert.Equal(ExpectedRealDataGenderRestrictedTemplates, templates.Count(template => !string.IsNullOrWhiteSpace(template.GenderPermitted)));
		Assert.Equal(ExpectedRealDataRankRestrictedTemplates, templates.Count(template => template.RequiredRank != 0));
		Assert.Equal(ExpectedRealDataMinLevelTemplates, templates.Count(template => template.MinLevelPermitted != 0));
		Assert.Equal(ExpectedRealDataMaxLevelTemplates, templates.Count(template => template.MaxLevelPermitted != 0));
		Assert.Equal(ExpectedRealDataMaxClassListCount, templates.Max(template => template.ClassPermitted.Count));
		Assert.Equal(ExpectedRealDataReportableTemplates, templates.Count(template => template.CanReport));
		Assert.Equal(ExpectedRealDataRewardRepeatTemplates, templates.Count(template => template.RewardRepeatCount != 0));
		Assert.Equal(ExpectedRealDataRewardTemplates, templates.Count(template => template.HasRewards));
		Assert.Equal(ExpectedRealDataExtendedRewardTemplates, templates.Count(template => template.HasExtendedRewards));
		Assert.Equal(ExpectedRealDataBonusTemplates, templates.Count(template => template.HasBonus));
		Assert.Equal(ExpectedRealDataQuestWorkItemTemplates, templates.Count(template => template.HasQuestWorkItems));
		Assert.Equal(ExpectedRealDataCannotShareTemplates, templates.Count(template => template.CannotShare));
		Assert.Equal(ExpectedRealDataCannotGiveupTemplates, templates.Count(template => template.CannotGiveup));
		Assert.Equal(ExpectedRealDataTimerTemplates, templates.Count(template => template.IsTimer));
		Assert.Equal(ExpectedRealDataAllianceTargetTemplates, templates.Count(template => template.Target == "ALLIANCE"));
		Assert.Equal(ExpectedRealDataAreaTargetTemplates, templates.Count(template => template.Target == "AREA"));
		Assert.Equal(ExpectedRealDataLeagueTargetTemplates, templates.Count(template => template.Target == "LEAGUE"));
		Assert.Equal(ExpectedRealDataTemplates, new NearbyQuestTemplateTable(templates).Count);
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
