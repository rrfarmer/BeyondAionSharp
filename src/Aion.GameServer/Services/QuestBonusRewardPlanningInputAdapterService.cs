using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class QuestBonusRewardPlanningInputAdapterService
{
	private readonly QuestBonusHandlerOutcomePlanService _handlerOutcomePlanService = new();
	private readonly QuestBonusCandidatePlanService _candidatePlanService = new();
	private readonly QuestBonusSelectionEnvelopeService _selectionEnvelopeService = new();
	private readonly QuestBonusRewardPlanningReportService _reportService = new();

	public QuestBonusRewardPlanningInputAdapterResult CreateReport(QuestBonusRewardPlanningInput input)
	{
		ArgumentNullException.ThrowIfNull(input);

		// Java parity: services/QuestService#getRewardItems gathers the handler
		// result before falling through to services/reward/BonusService#getQuestBonus.
		// This adapter only composes explicit non-live inputs; it never reads globals.
		var missingInputs = GetMissingInputs(input);
		if (missingInputs.Count != 0)
			return QuestBonusRewardPlanningInputAdapterResult.Missing(missingInputs);

		var bonusProjection = input.RewardProjection!.ItemProjection!.BonusProjection!;
		var handlerOutcome = _handlerOutcomePlanService.CreatePlan(new QuestBonusHandlerOutcomeInput(
			bonusProjection.BonusType,
			ToHandlerQuestStates(input),
			input.LoadedBonusHandlerQuestIds));
		var candidateInput = new QuestBonusCandidatePlanInput(
			bonusProjection.BonusType,
			bonusProjection.Level,
			input.PlayerRace!,
			input.QuestTemplate!.CombineSkill,
			input.QuestTemplate.CombineSkillPoint);
		var selectionEnvelope = _selectionEnvelopeService.CreateEnvelope(CreateCandidatePlan(input, bonusProjection, candidateInput));

		return QuestBonusRewardPlanningInputAdapterResult.Created(_reportService.CreateReport(handlerOutcome, selectionEnvelope));
	}

	private QuestBonusCandidatePlan CreateCandidatePlan(
		QuestBonusRewardPlanningInput input,
		QuestFinishRewardBonusTemplateProjection bonusProjection,
		QuestBonusCandidatePlanInput candidateInput)
	{
		if (bonusProjection.SupportStatus != QuestFinishRewardBonusSupportStatus.SupportedByJavaBonusService)
			return new QuestBonusCandidatePlan(candidateInput, [], []);

		return _candidatePlanService.CreatePlan(
			candidateInput,
			input.ItemGroups!,
			input.ItemTemplates!);
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

	private static IReadOnlyDictionary<int, QuestBonusHandlerQuestState> ToHandlerQuestStates(QuestBonusRewardPlanningInput input)
	{
		if (input.BonusHandlerQuestStates is not null)
		{
			return input.BonusHandlerQuestStates.ToDictionary(
				pair => pair.Key,
				pair => ToHandlerQuestState(pair.Value));
		}

		var currentQuestState = input.CurrentQuestState!;
		return new Dictionary<int, QuestBonusHandlerQuestState>
		{
			[currentQuestState.QuestId] = ToHandlerQuestState(currentQuestState),
		};
	}

	private static QuestBonusHandlerQuestState ToHandlerQuestState(PlayerQuestState questState) =>
		new(
			questState.Status,
			questState.GetQuestVarById(0),
			questState.CompleteCount);
}

public sealed record QuestBonusRewardPlanningInput(
	QuestFinishRewardTemplateProjection? RewardProjection,
	NearbyQuestTemplateSummary? QuestTemplate,
	PlayerQuestState? CurrentQuestState,
	string? PlayerRace,
	ItemTemplateTable? ItemTemplates = null,
	IReadOnlyList<QuestBonusItemGroupProjection>? ItemGroups = null,
	IReadOnlyDictionary<int, PlayerQuestState>? BonusHandlerQuestStates = null,
	IReadOnlySet<int>? LoadedBonusHandlerQuestIds = null);

public sealed record QuestBonusRewardPlanningInputAdapterResult(
	QuestBonusRewardPlanningInputAdapterStatus Status,
	IReadOnlyList<QuestBonusRewardPlanningMissingInput> MissingInputs,
	QuestBonusRewardPlanningReport? Report)
{
	public static QuestBonusRewardPlanningInputAdapterResult Created(QuestBonusRewardPlanningReport report) =>
		new(QuestBonusRewardPlanningInputAdapterStatus.ReportCreated, [], report);

	public static QuestBonusRewardPlanningInputAdapterResult Missing(IReadOnlyList<QuestBonusRewardPlanningMissingInput> missingInputs) =>
		new(QuestBonusRewardPlanningInputAdapterStatus.MissingRequiredInputs, missingInputs, Report: null);
}

public enum QuestBonusRewardPlanningInputAdapterStatus
{
	ReportCreated,
	MissingRequiredInputs,
}

public enum QuestBonusRewardPlanningMissingInput
{
	RewardProjection,
	ItemProjection,
	BonusProjection,
	QuestTemplate,
	QuestState,
	PlayerRace,
	ItemTemplates,
	ItemGroups,
}
