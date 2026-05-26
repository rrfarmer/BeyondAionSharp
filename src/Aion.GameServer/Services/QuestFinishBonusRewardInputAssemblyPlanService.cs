using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class QuestFinishBonusRewardInputAssemblyPlanService
{
	private const string JavaSource =
		"QuestService.finishQuest -> QuestService.getRewardItems -> QuestEngine.onBonusApplyEvent -> BonusService.getQuestBonus";

	public static QuestFinishBonusRewardInputAssemblyPlan CreatePlan(QuestFinishBonusRewardInputAssemblyRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		// Java parity breadcrumb: Java gathers bonus handler and BonusService inputs
		// during QuestService.getRewardItems. This C# planner only assembles an
		// explicit disabled input envelope; it never dispatches handlers or rewards.
		var handlerQuestStates = ToHandlerQuestStates(request.BonusHandlerQuestStates);
		var adapterInput = new QuestBonusRewardPlanningInput(
			request.RewardProjection,
			request.QuestTemplate,
			request.CurrentQuestState,
			request.Player?.Race,
			request.ItemTemplates,
			request.ItemGroups?.Groups,
			handlerQuestStates,
			request.LoadedBonusHandlerQuestIds);
		var missingInputs = GetMissingInputs(adapterInput);
		var handlerQuestStateIds = handlerQuestStates?.Keys.OrderBy(id => id).ToArray() ?? [];

		return missingInputs.Count == 0
			? QuestFinishBonusRewardInputAssemblyPlan.Created(adapterInput, handlerQuestStateIds)
			: QuestFinishBonusRewardInputAssemblyPlan.Missing(missingInputs, handlerQuestStateIds);
	}

	private static IReadOnlyDictionary<int, PlayerQuestState>? ToHandlerQuestStates(
		IEnumerable<PlayerQuestState>? questStates)
	{
		if (questStates is null)
			return null;

		return questStates
			.GroupBy(questState => questState.QuestId)
			.ToDictionary(group => group.Key, group => group.First());
	}

	private static IReadOnlyList<QuestBonusRewardPlanningMissingInput> GetMissingInputs(QuestBonusRewardPlanningInput input)
	{
		var missingInputs = new List<QuestBonusRewardPlanningMissingInput>();
		if (input.RewardProjection is null)
		{
			missingInputs.Add(QuestBonusRewardPlanningMissingInput.RewardProjection);
			return missingInputs;
		}

		if (input.RewardProjection.ItemProjection is null)
		{
			missingInputs.Add(QuestBonusRewardPlanningMissingInput.ItemProjection);
			return missingInputs;
		}

		var bonusProjection = input.RewardProjection.ItemProjection.BonusProjection;
		if (bonusProjection is null)
		{
			missingInputs.Add(QuestBonusRewardPlanningMissingInput.BonusProjection);
			return missingInputs;
		}

		if (input.QuestTemplate is null)
			missingInputs.Add(QuestBonusRewardPlanningMissingInput.QuestTemplate);
		if (input.CurrentQuestState is null && input.BonusHandlerQuestStates is null)
			missingInputs.Add(QuestBonusRewardPlanningMissingInput.QuestState);
		if (string.IsNullOrWhiteSpace(input.PlayerRace))
			missingInputs.Add(QuestBonusRewardPlanningMissingInput.PlayerRace);
		if (bonusProjection.SupportStatus == QuestFinishRewardBonusSupportStatus.SupportedByJavaBonusService)
		{
			if (input.ItemTemplates is null)
				missingInputs.Add(QuestBonusRewardPlanningMissingInput.ItemTemplates);
			if (input.ItemGroups is null)
				missingInputs.Add(QuestBonusRewardPlanningMissingInput.ItemGroups);
		}

		return missingInputs;
	}

	public static string JavaSourceBreadcrumb => JavaSource;
}

public sealed record QuestFinishBonusRewardInputAssemblyRequest(
	QuestFinishRewardTemplateProjection? RewardProjection,
	NearbyQuestTemplateSummary? QuestTemplate,
	PlayerQuestState? CurrentQuestState,
	Player? Player,
	ItemTemplateTable? ItemTemplates = null,
	QuestBonusItemGroupTable? ItemGroups = null,
	IReadOnlyList<PlayerQuestState>? BonusHandlerQuestStates = null,
	IReadOnlySet<int>? LoadedBonusHandlerQuestIds = null);

public sealed record QuestFinishBonusRewardInputAssemblyPlan(
	QuestFinishBonusRewardInputAssemblyStatus Status,
	string JavaSource,
	bool IsLive,
	IReadOnlyList<QuestBonusRewardPlanningMissingInput> MissingInputs,
	QuestBonusRewardPlanningInput? AdapterInput,
	IReadOnlyList<int> HandlerQuestStateIds)
{
	public bool CreatedInput => Status == QuestFinishBonusRewardInputAssemblyStatus.InputCreated;

	public static QuestFinishBonusRewardInputAssemblyPlan Created(
		QuestBonusRewardPlanningInput input,
		IReadOnlyList<int> handlerQuestStateIds) =>
		new(
			QuestFinishBonusRewardInputAssemblyStatus.InputCreated,
			QuestFinishBonusRewardInputAssemblyPlanService.JavaSourceBreadcrumb,
			IsLive: false,
			[],
			input,
			handlerQuestStateIds);

	public static QuestFinishBonusRewardInputAssemblyPlan Missing(
		IReadOnlyList<QuestBonusRewardPlanningMissingInput> missingInputs,
		IReadOnlyList<int> handlerQuestStateIds) =>
		new(
			QuestFinishBonusRewardInputAssemblyStatus.MissingRequiredInputs,
			QuestFinishBonusRewardInputAssemblyPlanService.JavaSourceBreadcrumb,
			IsLive: false,
			missingInputs,
			AdapterInput: null,
			handlerQuestStateIds);
}

public enum QuestFinishBonusRewardInputAssemblyStatus
{
	InputCreated,
	MissingRequiredInputs,
}
