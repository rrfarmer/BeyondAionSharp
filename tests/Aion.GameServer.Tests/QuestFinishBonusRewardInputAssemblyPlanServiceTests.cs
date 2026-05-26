using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishBonusRewardInputAssemblyPlanServiceTests
{
	[Fact]
	public void CreatePlan_AssemblesCompleteSupportedBonusInputWithoutInvokingAdapter()
	{
		var rewardProjection = RewardProjection("TASK", 30, QuestFinishRewardBonusSupportStatus.SupportedByJavaBonusService);
		var questState = QuestState(5001, "REWARD");
		var itemGroups = new QuestBonusItemGroupTable(
		[
			new QuestBonusItemGroupProjection(
				"craft_materials",
				"TASK",
				100f,
				QuestBonusItemShape.CraftItem,
				[new QuestBonusItemProjection(152020112, Skill: 40007, MinLevel: 1, MaxLevel: 50)]),
		]);
		var request = new QuestFinishBonusRewardInputAssemblyRequest(
			rewardProjection,
			new NearbyQuestTemplateSummary(5001, CombineSkill: 40007, CombineSkillPoint: 30),
			questState,
			new Player { Race = "ELYOS" },
			CreateTemplates(Template(152020112, "PC_ALL", 1)),
			itemGroups,
			[questState, QuestState(80016, "START")],
			new HashSet<int> { 5001, 80016 });

		var plan = QuestFinishBonusRewardInputAssemblyPlanService.CreatePlan(request);

		Assert.Equal(QuestFinishBonusRewardInputAssemblyStatus.InputCreated, plan.Status);
		Assert.True(plan.CreatedInput);
		Assert.False(plan.IsLive);
		Assert.Empty(plan.MissingInputs);
		Assert.Equal([5001, 80016], plan.HandlerQuestStateIds);
		Assert.Contains("QuestService.getRewardItems", plan.JavaSource);

		var input = Assert.IsType<QuestBonusRewardPlanningInput>(plan.AdapterInput);
		Assert.Same(rewardProjection, input.RewardProjection);
		Assert.Equal("ELYOS", input.PlayerRace);
		Assert.Same(questState, input.CurrentQuestState);
		Assert.Same(itemGroups.Groups, input.ItemGroups);
		Assert.Equal([5001, 80016], input.BonusHandlerQuestStates!.Keys.OrderBy(id => id).ToArray());
		Assert.Equal([5001, 80016], input.LoadedBonusHandlerQuestIds!.OrderBy(id => id).ToArray());
	}

	[Fact]
	public void CreatePlan_ReportsMissingSupportedBonusStaticDataBeforeInputCreation()
	{
		var request = new QuestFinishBonusRewardInputAssemblyRequest(
			RewardProjection("MANASTONE", 60, QuestFinishRewardBonusSupportStatus.SupportedByJavaBonusService),
			new NearbyQuestTemplateSummary(5002),
			QuestState(5002, "REWARD"),
			new Player { Race = "ASMODIANS" });

		var plan = QuestFinishBonusRewardInputAssemblyPlanService.CreatePlan(request);

		Assert.Equal(QuestFinishBonusRewardInputAssemblyStatus.MissingRequiredInputs, plan.Status);
		Assert.False(plan.CreatedInput);
		Assert.False(plan.IsLive);
		Assert.Null(plan.AdapterInput);
		Assert.Equal(
			[
				QuestBonusRewardPlanningMissingInput.ItemTemplates,
				QuestBonusRewardPlanningMissingInput.ItemGroups,
			],
			plan.MissingInputs);
	}

	[Fact]
	public void CreatePlan_AllowsMovieNoOpWithoutStaticBonusData()
	{
		var request = new QuestFinishBonusRewardInputAssemblyRequest(
			RewardProjection("MOVIE", 0, QuestFinishRewardBonusSupportStatus.SilentNoOpInJavaBonusService),
			new NearbyQuestTemplateSummary(80016),
			QuestState(80016, "REWARD"),
			new Player { Race = "ELYOS" });

		var plan = QuestFinishBonusRewardInputAssemblyPlanService.CreatePlan(request);

		Assert.Equal(QuestFinishBonusRewardInputAssemblyStatus.InputCreated, plan.Status);
		var input = Assert.IsType<QuestBonusRewardPlanningInput>(plan.AdapterInput);
		Assert.Null(input.ItemTemplates);
		Assert.Null(input.ItemGroups);
	}

	[Fact]
	public void CreatePlan_UsesExplicitHandlerQuestStatesWhenCurrentQuestStateIsMissing()
	{
		var handlerState = QuestState(80018, "REWARD");
		var request = new QuestFinishBonusRewardInputAssemblyRequest(
			RewardProjection("MOVIE", 0, QuestFinishRewardBonusSupportStatus.SilentNoOpInJavaBonusService),
			new NearbyQuestTemplateSummary(80018),
			CurrentQuestState: null,
			Player: new Player { Race = "ELYOS" },
			BonusHandlerQuestStates: [handlerState]);

		var plan = QuestFinishBonusRewardInputAssemblyPlanService.CreatePlan(request);

		Assert.Equal(QuestFinishBonusRewardInputAssemblyStatus.InputCreated, plan.Status);
		Assert.Equal([80018], plan.HandlerQuestStateIds);
		var input = Assert.IsType<QuestBonusRewardPlanningInput>(plan.AdapterInput);
		Assert.Null(input.CurrentQuestState);
		Assert.Same(handlerState, input.BonusHandlerQuestStates![80018]);
	}

	private static QuestFinishRewardTemplateProjection RewardProjection(
		string bonusType,
		int bonusLevel,
		QuestFinishRewardBonusSupportStatus supportStatus) =>
		new(ItemProjection: new QuestFinishRewardItemTemplateProjection(
			HasBonus: true,
			BonusProjection: new QuestFinishRewardBonusTemplateProjection(bonusType, bonusLevel, supportStatus)));

	private static PlayerQuestState QuestState(int questId, string status) =>
		new(questId, status, QuestVars: 0, Flags: 0, CompleteCount: 0);

	private static ItemTemplateTable CreateTemplates(params ItemTemplateSummary[] templates) => new(templates);

	private static ItemTemplateSummary Template(int itemId, string race, int level) =>
		new(itemId, $"Item {itemId}", 0, 0, level, "NONE", "NORMAL", "COMMON", race, 100, 0, 0);
}
