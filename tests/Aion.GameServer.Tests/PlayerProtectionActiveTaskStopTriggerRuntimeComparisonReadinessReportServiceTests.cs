using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportServiceTests
{
	[Fact]
	public void Create_WithRuntimeDesignAndTraceSchemaKeepsArtifactBlockersExplicit()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.False(report.IsLive);
		Assert.True(report.HasRuntimeComparisonDesign);
		Assert.True(report.HasTraceArtifactSchema);
		Assert.True(report.NeedsJavaInstrumentation);
		Assert.True(report.NeedsJavaTraceSerializer);
		Assert.True(report.NeedsGeneratedJavaTraceArtifacts);
		Assert.True(report.NeedsCSharpArtifactReader);
		Assert.True(report.NeedsLiveCSharpPacketHooks);
		Assert.True(report.NeedsRuntimeComparisonEvidence);
		Assert.False(report.ReadyForRuntimeComparison);
	}

	[Fact]
	public void Create_MissingTraceSchemaBlocksBeforeArtifactReaderWork()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			traceSchema: null);

		Assert.False(report.HasTraceArtifactSchema);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.TraceArtifactSchema
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
			&& row.BlocksRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpArtifactReader
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
			&& row.Evidence.Contains("trace schema missing", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingRuntimeDesignBlocksLiveHookAndScenarioReadiness()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			runtimeDesign: null,
			CreateTraceSchema());

		Assert.False(report.HasRuntimeComparisonDesign);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonDesign
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.LiveCSharpPacketHooks
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite);
	}

	[Fact]
	public void Create_TraceSchemaRowSummarizesPhaseFieldAndReturnReasonCounts()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.TraceArtifactSchema
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata
			&& row.Evidence.Contains("phases=", StringComparison.Ordinal)
			&& row.Evidence.Contains("fields=", StringComparison.Ordinal)
			&& row.Evidence.Contains("returnReasons=", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CSharpArtifactReaderBlockerDocumentsParserValidationRequirements()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpArtifactReader
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpImplementation
			&& row.Notes.Contains("schema version", StringComparison.Ordinal)
			&& row.Notes.Contains("enum return reasons", StringComparison.Ordinal)
			&& row.Notes.Contains("invariant floats", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeEvidenceBlockerPreventsVerifiedParityClaim()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingRuntimeEvidence
			&& row.Notes.Contains("Verified parity cannot be claimed", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport CreateTraceSchema() =>
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport CreateRuntimeDesign() =>
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

	private static PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport CreateDetailedSummary() =>
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(
			PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateBaseRequest(
				packetX: 101f,
				evaluateCmMoveInAir: true,
				evaluateCmAttack: true,
				evaluateCmCastSpell: true,
				evaluateCmUseItem: true,
				evaluateCmShowDialog: true,
				evaluateCmDialogSelect: true,
				evaluateCmCompositeStones: true,
				evaluateCmEmotion: true)));

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
