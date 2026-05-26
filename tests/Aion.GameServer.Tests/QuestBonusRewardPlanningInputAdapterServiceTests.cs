using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestBonusRewardPlanningInputAdapterServiceTests
{
	[Fact]
	public void CreateReport_ComposesSupportedBonusFromExplicitInputsWithoutProductionWiring()
	{
		var service = new QuestBonusRewardPlanningInputAdapterService();
		var input = new QuestBonusRewardPlanningInput(
			RewardProjection("TASK", 0, QuestFinishRewardBonusSupportStatus.SupportedByJavaBonusService),
			new NearbyQuestTemplateSummary(5001, CombineSkill: 40007, CombineSkillPoint: 30),
			QuestState(5001, "REWARD"),
			PlayerRace: "ELYOS",
			ItemTemplates: CreateTemplates(Template(152020112, "PC_ALL", 1), Template(152020113, "PC_ALL", 1)),
			ItemGroups:
			[
				new QuestBonusItemGroupProjection(
					"craft_materials",
					"TASK",
					50f,
					QuestBonusItemShape.CraftItem,
					[
						new QuestBonusItemProjection(152020112, Skill: 40007, MinLevel: 5, MaxLevel: 40),
						new QuestBonusItemProjection(152020113, Skill: 40008, MinLevel: 5, MaxLevel: 40),
					]),
			]);

		var result = service.CreateReport(input);

		Assert.Equal(QuestBonusRewardPlanningInputAdapterStatus.ReportCreated, result.Status);
		Assert.Empty(result.MissingInputs);
		var report = Assert.IsType<QuestBonusRewardPlanningReport>(result.Report);
		Assert.True(report.BonusServiceAllowed);
		Assert.Equal(QuestBonusServicePlanningStatus.SelectionInputsAvailable, report.BonusServiceStatus);
		var group = Assert.Single(report.CandidateGroups);
		Assert.Equal("craft_materials", group.ElementName);
		Assert.Equal(152020112, Assert.Single(group.Items).ItemId);
		Assert.Equal(1, report.SkippedItemCount);
	}

	[Fact]
	public void CreateReport_ReportsMissingSupportedBonusStaticDataBeforeComposing()
	{
		var service = new QuestBonusRewardPlanningInputAdapterService();
		var input = new QuestBonusRewardPlanningInput(
			RewardProjection("MANASTONE", 60, QuestFinishRewardBonusSupportStatus.SupportedByJavaBonusService),
			new NearbyQuestTemplateSummary(5002),
			QuestState(5002, "REWARD"),
			PlayerRace: "ELYOS");

		var result = service.CreateReport(input);

		Assert.Equal(QuestBonusRewardPlanningInputAdapterStatus.MissingRequiredInputs, result.Status);
		Assert.Null(result.Report);
		Assert.Equal(
			[
				QuestBonusRewardPlanningMissingInput.ItemTemplates,
				QuestBonusRewardPlanningMissingInput.ItemGroups,
			],
			result.MissingInputs);
	}

	[Fact]
	public void CreateReport_SilentNoOpMovieBonusDoesNotRequireItemGroupStaticData()
	{
		var service = new QuestBonusRewardPlanningInputAdapterService();
		var input = new QuestBonusRewardPlanningInput(
			RewardProjection("MOVIE", 0, QuestFinishRewardBonusSupportStatus.SilentNoOpInJavaBonusService),
			new NearbyQuestTemplateSummary(80016),
			QuestState(80016, "REWARD", completeCount: 9),
			PlayerRace: "ELYOS");

		var result = service.CreateReport(input);

		Assert.Equal(QuestBonusRewardPlanningInputAdapterStatus.ReportCreated, result.Status);
		var report = Assert.IsType<QuestBonusRewardPlanningReport>(result.Report);
		Assert.True(report.BonusServiceAllowed);
		Assert.Equal(QuestBonusServicePlanningStatus.NoCandidateGroups, report.BonusServiceStatus);
		Assert.Equal(188051106, Assert.Single(report.HandlerDirectRewardItems).ItemId);
		Assert.Equal([103, 104], Assert.Single(report.HandlerSideEffects).CandidateIds);
		Assert.Empty(report.CandidateGroups);
	}

	[Fact]
	public void CreateReport_UsesExplicitHandlerQuestStatesForFirstLoadedHandlerOrdering()
	{
		var service = new QuestBonusRewardPlanningInputAdapterService();
		var handlerStates = new Dictionary<int, PlayerQuestState>
		{
			[80016] = QuestState(80016, "START", completeCount: 9),
			[80018] = QuestState(80018, "REWARD", completeCount: 9),
		};
		var input = new QuestBonusRewardPlanningInput(
			RewardProjection("MOVIE", 0, QuestFinishRewardBonusSupportStatus.SilentNoOpInJavaBonusService),
			new NearbyQuestTemplateSummary(80018),
			CurrentQuestState: null,
			PlayerRace: "ELYOS",
			BonusHandlerQuestStates: handlerStates,
			LoadedBonusHandlerQuestIds: new HashSet<int> { 80016, 80018 });

		var result = service.CreateReport(input);

		var report = Assert.IsType<QuestBonusRewardPlanningReport>(result.Report);
		Assert.Equal(QuestBonusHandlerResult.Failed, report.HandlerResult);
		Assert.Equal(80016, report.HandlerQuestId);
		Assert.False(report.BonusServiceAllowed);
		Assert.Equal(QuestBonusServicePlanningStatus.SuppressedByHandlerFailed, report.BonusServiceStatus);
	}

	[Fact]
	public void CreateReport_ReportsMissingBonusProjection()
	{
		var service = new QuestBonusRewardPlanningInputAdapterService();
		var input = new QuestBonusRewardPlanningInput(
			new QuestFinishRewardTemplateProjection(ItemProjection: new QuestFinishRewardItemTemplateProjection(HasBonus: true)),
			new NearbyQuestTemplateSummary(5003),
			QuestState(5003, "REWARD"),
			PlayerRace: "ELYOS");

		var result = service.CreateReport(input);

		Assert.Equal(QuestBonusRewardPlanningInputAdapterStatus.MissingRequiredInputs, result.Status);
		Assert.Equal([QuestBonusRewardPlanningMissingInput.BonusProjection], result.MissingInputs);
		Assert.Null(result.Report);
	}

	private static QuestFinishRewardTemplateProjection RewardProjection(
		string bonusType,
		int bonusLevel,
		QuestFinishRewardBonusSupportStatus supportStatus) =>
		new(ItemProjection: new QuestFinishRewardItemTemplateProjection(
			HasBonus: true,
			BonusProjection: new QuestFinishRewardBonusTemplateProjection(bonusType, bonusLevel, supportStatus)));

	private static PlayerQuestState QuestState(int questId, string status, int completeCount = 0) =>
		new(questId, status, QuestVars: 0, Flags: 0, completeCount);

	private static ItemTemplateTable CreateTemplates(params ItemTemplateSummary[] templates) => new(templates);

	private static ItemTemplateSummary Template(int itemId, string race, int level) =>
		new(itemId, $"Item {itemId}", 0, 0, level, "NONE", "NORMAL", "COMMON", race, 100, 0, 0);
}
