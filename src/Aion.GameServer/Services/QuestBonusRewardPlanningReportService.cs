namespace Aion.GameServer.Services;

public sealed class QuestBonusRewardPlanningReportService
{
	public QuestBonusRewardPlanningReport CreateReport(
		QuestBonusHandlerOutcomePlan handlerOutcome,
		QuestBonusSelectionEnvelope selectionEnvelope)
	{
		ArgumentNullException.ThrowIfNull(handlerOutcome);
		ArgumentNullException.ThrowIfNull(selectionEnvelope);

		// Java parity: services/QuestService#getRewardItems. Handler FAILED is
		// the only handler result that suppresses the later BonusService call.
		var bonusServiceAllowed = handlerOutcome.Result != QuestBonusHandlerResult.Failed;
		var bonusServiceStatus = bonusServiceAllowed
			? ToBonusServiceStatus(selectionEnvelope.Status)
			: QuestBonusServicePlanningStatus.SuppressedByHandlerFailed;

		return new QuestBonusRewardPlanningReport(
			handlerOutcome.Input.BonusType,
			handlerOutcome.Result,
			handlerOutcome.Status,
			handlerOutcome.HandlerQuestId,
			handlerOutcome.HandlerKind,
			handlerOutcome.DirectRewardItems,
			handlerOutcome.SideEffects,
			bonusServiceAllowed,
			bonusServiceStatus,
			selectionEnvelope.GroupChanceSum,
			selectionEnvelope.Groups,
			selectionEnvelope.SkippedItemCount);
	}

	private static QuestBonusServicePlanningStatus ToBonusServiceStatus(QuestBonusSelectionEnvelopeStatus status) =>
		status switch
		{
			QuestBonusSelectionEnvelopeStatus.NoCandidateGroups => QuestBonusServicePlanningStatus.NoCandidateGroups,
			QuestBonusSelectionEnvelopeStatus.NoPositiveGroupChance => QuestBonusServicePlanningStatus.NoPositiveGroupChance,
			QuestBonusSelectionEnvelopeStatus.HasGroupWithNoPositiveItemChance => QuestBonusServicePlanningStatus.HasGroupWithNoPositiveItemChance,
			QuestBonusSelectionEnvelopeStatus.SelectionInputsAvailable => QuestBonusServicePlanningStatus.SelectionInputsAvailable,
			_ => QuestBonusServicePlanningStatus.Unknown,
		};
}

public sealed record QuestBonusRewardPlanningReport(
	string BonusType,
	QuestBonusHandlerResult HandlerResult,
	QuestBonusHandlerOutcomeStatus HandlerStatus,
	int? HandlerQuestId,
	QuestBonusHandlerKind? HandlerKind,
	IReadOnlyList<QuestFinishRewardItem> HandlerDirectRewardItems,
	IReadOnlyList<QuestBonusHandlerSideEffectIntent> HandlerSideEffects,
	bool BonusServiceAllowed,
	QuestBonusServicePlanningStatus BonusServiceStatus,
	float GroupChanceSum,
	IReadOnlyList<QuestBonusSelectionGroupEnvelope> CandidateGroups,
	int SkippedItemCount);

public enum QuestBonusServicePlanningStatus
{
	Unknown,
	SuppressedByHandlerFailed,
	NoCandidateGroups,
	NoPositiveGroupChance,
	HasGroupWithNoPositiveItemChance,
	SelectionInputsAvailable,
}
