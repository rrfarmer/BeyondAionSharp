using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class NearbyQuestTemplateXmlExtractorTests
{
	[Fact]
	public void Extract_ReadsNearbyPredicateQuestTemplateFieldsLikeJavaQuestTemplate()
	{
		const string xml = """
			<quests>
				<quest id="1001" minlevel_permitted="19" maxlevel_permitted="45" race_permitted="ELYOS" rank="4"
				       max_repeat_count="3" combineskill="40001" combine_skillpoint="199" npcfaction_id="12"
				       repeat_cycle="Mon Wed">
					<class_permitted>
						GLADIATOR CLERIC
					</class_permitted>
					<gender_permitted>MALE</gender_permitted>
					<inventory_items>
						<inventory_item item_id="182200001" count="1" />
					</inventory_items>
					<start_conditions>
						<finished quest_id="1000" />
					</start_conditions>
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
		Assert.True(template.HasXmlStartConditions);
		Assert.True(template.HasInventoryItems);
		Assert.Equal(40001, template.CombineSkill);
		Assert.Equal(12, template.NpcFactionId);
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
		Assert.False(template.HasXmlStartConditions);
		Assert.False(template.HasInventoryItems);
		Assert.Equal(0, template.CombineSkill);
		Assert.Equal(0, template.NpcFactionId);
	}

	[Fact]
	public void Extract_StreamInputFeedsNearbyQuestTemplateTableAndPredicate()
	{
		const string xml = """
			<quests>
				<quest id="3001" minlevel_permitted="22" race_permitted="PC_ALL" />
			</quests>
			""";
		var extractor = new NearbyQuestTemplateXmlExtractor();
		using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

		var table = new NearbyQuestTemplateTable(extractor.Extract(stream));

		Assert.True(table.TryGetQuest(3001, out var template));
		Assert.NotNull(template);
		Assert.Equal(22, template.MinLevelPermitted);
	}
}
