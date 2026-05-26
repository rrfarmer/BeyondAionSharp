using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestBonusRewardPlanningReportServiceTests
{
	[Fact]
	public void CreateReport_HandlerFailedSuppressesBonusServiceEvenWhenSelectionInputsExist()
	{
		var reportService = new QuestBonusRewardPlanningReportService();
		var handler = new QuestBonusHandlerOutcomePlan(
			new QuestBonusHandlerOutcomeInput("LUNAR", new Dictionary<int, QuestBonusHandlerQuestState>()),
			QuestBonusHandlerResult.Failed,
			QuestBonusHandlerOutcomeStatus.HandlerFailed,
			80034,
			QuestBonusHandlerKind.LunarGate,
			[],
			[]);
		var envelope = SelectionEnvelope("LUNAR", QuestBonusSelectionEnvelopeStatus.SelectionInputsAvailable);

		var report = reportService.CreateReport(handler, envelope);

		Assert.False(report.BonusServiceAllowed);
		Assert.Equal(QuestBonusServicePlanningStatus.SuppressedByHandlerFailed, report.BonusServiceStatus);
		Assert.Equal(QuestBonusHandlerResult.Failed, report.HandlerResult);
		Assert.Equal(80034, report.HandlerQuestId);
		Assert.Single(report.CandidateGroups);
	}

	[Fact]
	public void CreateReport_UnknownHandlerStillAllowsBonusServiceLikeJavaGetRewardItems()
	{
		var reportService = new QuestBonusRewardPlanningReportService();
		var handler = new QuestBonusHandlerOutcomePlan(
			new QuestBonusHandlerOutcomeInput("MANASTONE", new Dictionary<int, QuestBonusHandlerQuestState>()),
			QuestBonusHandlerResult.Unknown,
			QuestBonusHandlerOutcomeStatus.NoRegisteredHandler,
			HandlerQuestId: null,
			HandlerKind: null,
			DirectRewardItems: [],
			SideEffects: []);
		var envelope = SelectionEnvelope("MANASTONE", QuestBonusSelectionEnvelopeStatus.SelectionInputsAvailable);

		var report = reportService.CreateReport(handler, envelope);

		Assert.True(report.BonusServiceAllowed);
		Assert.Equal(QuestBonusServicePlanningStatus.SelectionInputsAvailable, report.BonusServiceStatus);
		Assert.Null(report.HandlerQuestId);
		Assert.Single(report.CandidateGroups);
	}

	[Fact]
	public void CreateReport_MovieHandlerCarriesDirectRewardAndNoCandidateGroupStatus()
	{
		var reportService = new QuestBonusRewardPlanningReportService();
		var handler = new QuestBonusHandlerOutcomePlan(
			new QuestBonusHandlerOutcomeInput("MOVIE", new Dictionary<int, QuestBonusHandlerQuestState>()),
			QuestBonusHandlerResult.Success,
			QuestBonusHandlerOutcomeStatus.HandlerSucceeded,
			80016,
			QuestBonusHandlerKind.Movie,
			[new QuestFinishRewardItem(188051106, 1)],
			[new QuestBonusHandlerSideEffectIntent(QuestBonusHandlerSideEffectKind.RandomMovie, [103, 104])]);
		var envelope = new QuestBonusSelectionEnvelope(
			new QuestBonusCandidatePlanInput("MOVIE", 0, "ELYOS"),
			QuestBonusSelectionEnvelopeStatus.NoCandidateGroups,
			GroupChanceSum: 0f,
			Groups: [],
			SkippedItemCount: 0);

		var report = reportService.CreateReport(handler, envelope);

		Assert.True(report.BonusServiceAllowed);
		Assert.Equal(QuestBonusServicePlanningStatus.NoCandidateGroups, report.BonusServiceStatus);
		Assert.Equal(188051106, Assert.Single(report.HandlerDirectRewardItems).ItemId);
		Assert.Equal([103, 104], Assert.Single(report.HandlerSideEffects).CandidateIds);
		Assert.Empty(report.CandidateGroups);
	}

	[Fact]
	public void CreateReport_MapsSelectionNullResultStatusesWhenHandlerAllowsBonusService()
	{
		var reportService = new QuestBonusRewardPlanningReportService();
		var handler = new QuestBonusHandlerOutcomePlan(
			new QuestBonusHandlerOutcomeInput("MEDAL", new Dictionary<int, QuestBonusHandlerQuestState>()),
			QuestBonusHandlerResult.Unknown,
			QuestBonusHandlerOutcomeStatus.NoRegisteredHandler,
			HandlerQuestId: null,
			HandlerKind: null,
			DirectRewardItems: [],
			SideEffects: []);

		Assert.Equal(
			QuestBonusServicePlanningStatus.NoPositiveGroupChance,
			reportService.CreateReport(handler, SelectionEnvelope("MEDAL", QuestBonusSelectionEnvelopeStatus.NoPositiveGroupChance)).BonusServiceStatus);
		Assert.Equal(
			QuestBonusServicePlanningStatus.HasGroupWithNoPositiveItemChance,
			reportService.CreateReport(handler, SelectionEnvelope("MEDAL", QuestBonusSelectionEnvelopeStatus.HasGroupWithNoPositiveItemChance)).BonusServiceStatus);
	}

	private static QuestBonusSelectionEnvelope SelectionEnvelope(string bonusType, QuestBonusSelectionEnvelopeStatus status) =>
		new(
			new QuestBonusCandidatePlanInput(bonusType, 0, "ELYOS"),
			status,
			GroupChanceSum: 100f,
			Groups:
			[
				new QuestBonusSelectionGroupEnvelope(
					"test_group",
					bonusType,
					GroupChance: 100f,
					ItemChanceSum: 100f,
					QuestBonusItemShape.FullRewardItem,
					QuestBonusSelectionGroupStatus.ItemChanceInputsAvailable,
					[
						new QuestBonusSelectionItemEnvelope(
							188000001,
							ItemChance: 100f,
							CountMin: 1,
							CountMax: 1,
							QuestBonusCandidateCountMode.Fixed),
					]),
			],
			SkippedItemCount: 2);
}
