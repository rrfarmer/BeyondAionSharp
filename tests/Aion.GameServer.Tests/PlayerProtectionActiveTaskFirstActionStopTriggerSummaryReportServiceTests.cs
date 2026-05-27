using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportServiceTests
{
	[Fact]
	public void Create_AllDetailedRowsSummarizesKnownPacketSources()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(CreateDetailedAudit());

		Assert.False(report.IsLive);
		Assert.True(report.HasAllKnownPacketSources);
		Assert.True(report.HasThresholdedMovementSource);
		Assert.True(report.HasEarlyActionStopSources);
		Assert.True(report.HasLateGuardedEmotionSource);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmAttack);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCastSpell);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmCompositeStones);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmDialogSelect);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmShowDialog);
		Assert.Contains(report.Rows, row => row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem);
	}

	[Fact]
	public void Create_ClassifiesCmMoveSeparatelyFromUnconditionalStops()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(CreateDetailedAudit());

		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMove
			&& row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.ThresholdedMovement
			&& row.Notes.Contains("exact x/y float inequality", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmMoveInAir
			&& row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.AcceptedAirMovement);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmUseItem
			&& row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.EarlyActionStop);
	}

	[Fact]
	public void Create_ClassifiesEmotionAsLateGuarded()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(CreateDetailedAudit());

		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion
			&& row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.LateGuardedEmotionStop
			&& row.Notes.Contains("many early returns do not stop protection", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ProductionReadinessRemainsBlockedByWiringAndRuntimeComparison()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(CreateDetailedAudit());

		Assert.False(report.ReadyForProductionPacketStopWiring);
		Assert.True(report.HasProductionWiringBlocker);
		Assert.True(report.HasRuntimeComparisonBlocker);
		Assert.Contains(report.Rows, row =>
			row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.ProductionBoundary
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.BlockedProductionWiring);
		Assert.Contains(report.Rows, row =>
			row.Category == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryCategory.RuntimeComparisonBlocker
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.NeedsRuntimeVerification);
	}

	[Fact]
	public void Create_DefaultAuditStillReportsPendingDetailedRows()
	{
		var report = PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(
			PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateBaseRequest()));

		Assert.False(report.ReadyForProductionPacketStopWiring);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmAttack
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.PendingDetailedAudit);
		Assert.Contains(report.Rows, row =>
			row.Source == PlayerProtectionActiveTaskFirstActionStopTriggerSource.CmEmotion
			&& row.Status == PlayerProtectionActiveTaskFirstActionStopTriggerSummaryStatus.PendingDetailedAudit);
	}

	private static PlayerProtectionActiveTaskFirstActionStopTriggerAuditReport CreateDetailedAudit() =>
		PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateBaseRequest(
			packetX: 101f,
			evaluateCmMoveInAir: true,
			evaluateCmAttack: true,
			evaluateCmCastSpell: true,
			evaluateCmUseItem: true,
			evaluateCmShowDialog: true,
			evaluateCmDialogSelect: true,
			evaluateCmCompositeStones: true,
			evaluateCmEmotion: true));

	private static PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest CreateBaseRequest(
		float packetX = CurrentX,
		bool evaluateCmMoveInAir = false,
		bool evaluateCmAttack = false,
		bool evaluateCmCastSpell = false,
		bool evaluateCmUseItem = false,
		bool evaluateCmShowDialog = false,
		bool evaluateCmDialogSelect = false,
		bool evaluateCmCompositeStones = false,
		bool evaluateCmEmotion = false) =>
		new(
			PlayerSpawned: true,
			AntiHackAccepted: true,
			TeleportationModeAbsoluteMove: false,
			PlayerProtectionActive: true,
			CurrentX,
			CurrentY,
			CurrentZ,
			packetX,
			CurrentY,
			CurrentZ,
			EvaluateCmMoveInAir: evaluateCmMoveInAir,
			EvaluateCmAttack: evaluateCmAttack,
			EvaluateCmCastSpell: evaluateCmCastSpell,
			EvaluateCmUseItem: evaluateCmUseItem,
			EvaluateCmShowDialog: evaluateCmShowDialog,
			EvaluateCmDialogSelect: evaluateCmDialogSelect,
			EvaluateCmCompositeStones: evaluateCmCompositeStones,
			EvaluateCmEmotion: evaluateCmEmotion);

	private const float CurrentX = 100f;
	private const float CurrentY = 200f;
	private const float CurrentZ = 50f;
}
