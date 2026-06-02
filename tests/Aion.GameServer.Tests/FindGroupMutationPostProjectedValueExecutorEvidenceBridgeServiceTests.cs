using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedValueExecutorEvidenceBridgeServiceTests
{
	[Fact]
	public void Create_DefaultBridgeBlocksUntilResultEmissionBlockerIsReady()
	{
		var bridge = FindGroupMutationPostProjectedValueExecutorEvidenceBridgeService.Create();

		Assert.Equal(FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedResultEmissionBlockerNotReady, bridge.Status);
		Assert.False(bridge.IsLive);
		Assert.True(bridge.HasResultEmissionBlockerReport);
		Assert.True(bridge.HasEvidenceSummary);
		Assert.False(bridge.HasAnyRuntimeEvidence);
		Assert.False(bridge.CanWriteExecutableExecutor);
		Assert.False(bridge.CanExecuteExecutor);
		Assert.False(bridge.CanEmitResults);
		Assert.False(bridge.CanRunRuntimeComparison);
		Assert.False(bridge.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", bridge.TraceName);
		Assert.Contains("addRecruitment/addApplication", bridge.JavaSource, StringComparison.Ordinal);
		Assert.Contains("result-emission blocker metadata", bridge.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(bridge.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.ResultEmissionBlocker
			&& row.Status == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedResultEmissionBlockerNotReady
			&& row.CurrentEvidence.Contains("canEmitAnyResult=False", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("resultEmissionBlockerRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("materializationBlockerEvidence=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("csharpHandoffStatus=BlockedMissingAcceptedBoundaryRows", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ResultEmissionReadyStillBlocksUntilEvidenceSummaryIsReady()
	{
		var bridge = FindGroupMutationPostProjectedValueExecutorEvidenceBridgeService.Create(
			ResultEmissionBlocker(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable),
			EvidenceSummary(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady));

		Assert.Equal(FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedEvidenceSummaryNotReady, bridge.Status);
		Assert.Contains(bridge.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.ResultEmissionBlocker
			&& row.CurrentEvidence.Contains("resultEmissionBlockerRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("materializationBlockerEvidence=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("csharpHandoffCanFeedJavaArtifactPairing=True", StringComparison.Ordinal)
			&& !bridge.CanEmitResults);
		Assert.Contains(bridge.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.EvidenceSummary
			&& row.Status == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedEvidenceSummaryNotReady
			&& row.RequiredEvidence.Contains("blocked-output preview", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("summaryRows=5", StringComparison.Ordinal)
			&& row.Notes.Contains("cannot authorize executable", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeMissingBridgeBlocksExecutableImplementation()
	{
		var bridge = FindGroupMutationPostProjectedValueExecutorEvidenceBridgeService.Create(
			ResultEmissionBlocker(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable),
			EvidenceSummary(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing));

		Assert.Equal(FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedExecutorImplementationUnavailable, bridge.Status);
		Assert.Contains("executable implementation, runtime comparison handoff, live dispatch, and verified parity remain blocked", bridge.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(bridge.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.ImplementationReadinessAudit
			&& row.Status == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedExecutableImplementationDisabled
			&& row.BlocksExecutableImplementation
			&& row.BlocksRuntimeComparison
			&& bridge.Rows.Any(resultRow =>
				resultRow.Requirement == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.ResultEmissionBlocker
				&& resultRow.CurrentEvidence.Contains("materializationBlockerEvidence=", StringComparison.Ordinal))
			&& row.CurrentEvidence.Contains("resultEmissionBlocked=5", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("summaryBlocksImplementation=5", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("row identity pairing", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ShapedMetadataStillBlocksRuntimeComparisonHandoff()
	{
		var bridge = FindGroupMutationPostProjectedValueExecutorEvidenceBridgeService.Create(
			ResultEmissionBlocker(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable),
			EvidenceSummary(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedResultEmissionDeferred));

		Assert.Equal(FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedExecutorImplementationUnavailable, bridge.Status);
		Assert.Contains(bridge.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.RuntimeComparisonHandoff
			&& row.Status == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedRuntimeComparisonMissing
			&& row.BlocksVerifiedParity
			&& row.RequiredEvidence.Contains("deterministic Java/C# runtime or socket evidence", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("materialized results", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("resultRowsRequireRuntimeComparison=5", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("summaryRuntimeRows=1", StringComparison.Ordinal)
			&& row.Notes.Contains("live dispatch remain blocked", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedValueResultEmissionBlockerReport ResultEmissionBlocker(
		FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus status) =>
		new(
			status,
			[
				ResultEmissionBlockerRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched),
				ResultEmissionBlockerRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow),
				ResultEmissionBlockerRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow),
				ResultEmissionBlockerRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch),
				ResultEmissionBlockerRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext),
			],
			OutputKindCount: 5,
			EmittableOutputCount: 0,
			HasMaterializationBlockerReport: true,
			HasResultEmissionGate: true,
			CanEmitMatched: false,
			CanEmitMissingJavaRow: false,
			CanEmitMissingCSharpRow: false,
			CanEmitFieldMismatch: false,
			CanEmitIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			"Result emission blocker remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedValueResultEmissionBlockerRow ResultEmissionBlockerRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind) =>
		new(
			order,
			outputKind,
			outputKind switch
			{
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedValueProjectionUnavailable,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedValueProjectionUnavailable,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext => FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedContextAttachmentUnavailable,
				_ => FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedMissingRowDecisionUnavailable,
			},
			MaterializationBlockerStatus: null,
			EmissionGateStatus: null,
			["Runtime comparison evidence exists for action 2 and action 6."],
			["testField"],
			["projectedField"],
			RequiresMaterializedOutput: outputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			RequiresRuntimeComparison: true,
			RequiresParentResult: outputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			HasMaterializationBlockerRow: true,
			HasEmissionGateRow: true,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			"materializationBlockerEvidence=projectedValueRows=activePlayerObjectId=functionPreflightRows=ReaderImplementationGate=status=ReadyForFunctionExecutionBlocked; typedReaderGateRows=RuntimeRowValueIntake=runtimeRowValueIntakeRows=RuntimeRowValueIntake=status=ReadyForRuntimeRowsValueReadersBlocked; csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked; csharpHandoffCanFeedJavaArtifactPairing=True",
			"test required evidence",
			"test notes");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract EvidenceSummary(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus status) =>
		new(
			status,
			[
				SummaryRow(1, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.BlockedOutputPreview),
				SummaryRow(2, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeEvidenceIntake),
				SummaryRow(3, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.MaterializationPreflight),
				SummaryRow(4, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.ResultEmissionGate),
				SummaryRow(5, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeComparison),
			],
			HasBlockedOutputPreview: true,
			HasRuntimeEvidenceIntake: true,
			HasMaterializationPreflight: true,
			HasResultEmissionGate: true,
			HasAnyRuntimeEvidence: status != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			"Evidence summary remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRow SummaryRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement requirement) =>
		new(
			order,
			requirement,
			requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeComparison
				? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedRuntimeComparisonMissing
				: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedRuntimeEvidenceMissing,
			HasProvider: true,
			HasRuntimeEvidence: false,
			BlocksExecutorImplementation: true,
			BlocksVerifiedParity: true,
			"test provider",
			"test required evidence",
			"test current evidence",
			"test notes");
}
