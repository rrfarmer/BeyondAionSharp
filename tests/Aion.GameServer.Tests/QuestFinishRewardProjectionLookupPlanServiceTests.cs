using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishRewardProjectionLookupPlanServiceTests
{
	[Fact]
	public void CreatePlan_PreparesCorrectedRewardGroupProjectionWithPacketContext()
	{
		const string xml = """
			<quests>
				<quest id="1001" can_report="true" category="QUEST" reward_repeat_count="3">
					<rewards exp="100">
						<reward_item item_id="182400001" count="1" />
					</rewards>
					<rewards gold="55">
						<reward_item item_id="182400010" count="2" />
						<selectable_reward_item item_id="182400020" count="3" />
					</rewards>
				</quest>
			</quests>
			""";
		var lookupTable = CreateLookupTable(xml, 1001, [0, 1]);
		var input = new QuestFinishRewardProjectionLookupInput(
			QuestId: 1001,
			DialogActionId: 9,
			ExtendedRewardIndex: null,
			CompleteCount: 2,
			CorrectedRewardGroup: 1,
			PlayerClass: "RANGER",
			TargetNpcId: 7001,
			HasTargetNpcTemplate: true);

		var plan = QuestFinishRewardProjectionLookupPlanService.CreatePlan(input, lookupTable);

		Assert.Equal(QuestFinishRewardProjectionLookupStatus.Found, plan.Status);
		Assert.Empty(plan.Diagnostics);
		var projection = Assert.IsType<QuestFinishRewardTemplateProjection>(plan.Projection);
		Assert.Equal(2, projection.RewardGroupCount);
		Assert.Equal(3, projection.RewardRepeatCount);
		Assert.Equal(9, projection.DialogActionId);
		Assert.Null(projection.ExtendedRewardIndex);
		Assert.Equal("RANGER", projection.PlayerClass);
		Assert.Equal(7001, projection.TargetNpcId);
		Assert.True(projection.HasTargetNpcTemplate);
		Assert.Equal(55, projection.NonItemProjection?.Kinah);
		var group = Assert.Single(projection.ItemProjection?.RewardGroups ?? []);
		Assert.Equal(1, group.RewardGroupIndex);
		Assert.Equal(182400010, Assert.Single(group.FixedRewardItems).ItemId);
		Assert.Equal(182400020, Assert.Single(group.SelectableRewardItems).ItemId);
	}

	[Fact]
	public void CreatePlan_ReturnsMissingQuestTemplateWhenJavaQuestLookupWouldFail()
	{
		var lookupTable = new QuestFinishRewardProjectionLookupTable([]);
		var input = new QuestFinishRewardProjectionLookupInput(
			QuestId: 404,
			DialogActionId: 8,
			ExtendedRewardIndex: null,
			CompleteCount: 0,
			CorrectedRewardGroup: 0);

		var plan = QuestFinishRewardProjectionLookupPlanService.CreatePlan(input, lookupTable);

		Assert.Equal(QuestFinishRewardProjectionLookupStatus.MissingQuestTemplate, plan.Status);
		Assert.Null(plan.Projection);
		Assert.Empty(plan.Diagnostics);
	}

	[Fact]
	public void CreatePlan_ReturnsMissingRewardGroupProjectionForUnmaterializedRewardGroup()
	{
		const string xml = """
			<quests>
				<quest id="1002" can_report="true">
					<rewards gold="1" />
				</quest>
			</quests>
			""";
		var lookupTable = CreateLookupTable(xml, 1002, [0]);
		var input = new QuestFinishRewardProjectionLookupInput(
			QuestId: 1002,
			DialogActionId: 8,
			ExtendedRewardIndex: null,
			CompleteCount: 0,
			CorrectedRewardGroup: 1);

		var plan = QuestFinishRewardProjectionLookupPlanService.CreatePlan(input, lookupTable);

		Assert.Equal(QuestFinishRewardProjectionLookupStatus.MissingRewardGroupProjection, plan.Status);
		Assert.Null(plan.Projection);
		Assert.Empty(plan.Diagnostics);
	}

	[Fact]
	public void CreatePlan_ReportsMissingPlayerClassAndTargetNpcTemplateDiagnostics()
	{
		const string xml = """
			<quests>
				<quest id="1003" can_report="true" reward_repeat_count="5" use_class_reward="2">
					<rewards exp="25">
						<selectable_reward_item item_id="182400001" count="1" />
					</rewards>
					<ranger_selectable_reward item_id="100900001" count="2" />
				</quest>
			</quests>
			""";
		var lookupTable = CreateLookupTable(xml, 1003, [0]);
		var input = new QuestFinishRewardProjectionLookupInput(
			QuestId: 1003,
			DialogActionId: 8,
			ExtendedRewardIndex: null,
			CompleteCount: 4,
			CorrectedRewardGroup: 0,
			PlayerClass: null,
			TargetNpcId: 7002,
			HasTargetNpcTemplate: false);

		var plan = QuestFinishRewardProjectionLookupPlanService.CreatePlan(input, lookupTable);

		Assert.Equal(QuestFinishRewardProjectionLookupStatus.Found, plan.Status);
		Assert.Equal(
		[
			QuestFinishRewardProjectionLookupDiagnostic.MissingPlayerClassForClassSelectableReward,
			QuestFinishRewardProjectionLookupDiagnostic.MissingTargetNpcTemplateForExperienceReward,
		], plan.Diagnostics);
		var projection = Assert.IsType<QuestFinishRewardTemplateProjection>(plan.Projection);
		Assert.Null(projection.PlayerClass);
		Assert.Equal(7002, projection.TargetNpcId);
		Assert.False(projection.HasTargetNpcTemplate);
	}

	private static QuestFinishRewardProjectionLookupTable CreateLookupTable(
		string xml,
		int questId,
		IReadOnlyList<int> rewardGroupIndexes)
	{
		var template = new NearbyQuestTemplateXmlExtractor().Extract(xml).Single();
		var document = XDocument.Parse(xml);
		var quest = document.Descendants().Single(element => element.Name.LocalName == "quest");
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();
		var projections = rewardGroupIndexes.ToDictionary(
			rewardGroupIndex => rewardGroupIndex,
			rewardGroupIndex => extractor.CreateProjection(quest, rewardGroupIndex));
		return new QuestFinishRewardProjectionLookupTable(
		[
			(questId, new QuestFinishRewardProjectionLookupEntry(template, projections)),
		]);
	}
}
