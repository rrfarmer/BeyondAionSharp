using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedValueExecutorConsistencyAuditServiceTests
{
	[Fact]
	public void Create_DefaultAuditBlocksBeforeMaterializationBlockerIsReady()
	{
		var audit = FindGroupMutationPostProjectedValueExecutorConsistencyAuditService.Create();

		Assert.Equal(FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedMaterializationBlockerNotReady, audit.Status);
		Assert.False(audit.IsLive);
		Assert.True(audit.HasMaterializationBlockerReport);
		Assert.True(audit.HasResultEmissionGate);
		Assert.True(audit.HasResultEmissionBlockerReport);
		Assert.True(audit.HasEvidenceSummary);
		Assert.True(audit.HasExecutorEvidenceBridge);
		Assert.False(audit.CanMaterializeOutputs);
		Assert.False(audit.CanEmitResults);
		Assert.False(audit.CanWriteExecutableExecutor);
		Assert.False(audit.CanRunRuntimeComparison);
		Assert.False(audit.CanEnableLiveDispatch);
		Assert.False(audit.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", audit.TraceName);
		Assert.Contains("addRecruitment/addApplication", audit.JavaSource, StringComparison.Ordinal);
		Assert.Contains("materialization blockers", audit.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(audit.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.MaterializationBlocker
			&& row.Status == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedMaterializationUnavailable
			&& row.CurrentEvidence.Contains("unreadEqualityFields=38", StringComparison.Ordinal)
			&& row.BlocksVerifiedParity);
		Assert.Contains(audit.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ExecutorEvidenceBridge
			&& row.CurrentEvidence.Contains("executorEvidenceBridgeRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("resultEmissionBlockerRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("materializationBlockerEvidence=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("csharpHandoffStatus=BlockedMissingAcceptedBoundaryRows", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyMaterializationStillBlocksWhenEmissionGateIsNotReady()
	{
		var audit = FindGroupMutationPostProjectedValueExecutorConsistencyAuditService.Create(
			MaterializationBlocker(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread),
			ResultEmissionGate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady));

		Assert.Equal(FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedResultEmissionBlockerNotReady, audit.Status);
		Assert.Contains(audit.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ResultEmissionGate
			&& row.Status == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedEmissionUnavailable
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady.ToString()
			&& row.CurrentEvidence.Contains("emittableOutputs=0", StringComparison.Ordinal)
			&& row.Notes.Contains("runtime comparison", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_EmissionReadyStillBlocksWhenEvidenceBridgeIsNotReady()
	{
		var audit = FindGroupMutationPostProjectedValueExecutorConsistencyAuditService.Create(
			MaterializationBlocker(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread),
			ResultEmissionGate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing),
			ResultEmissionBlocker(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable),
			EvidenceSummary(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady));

		Assert.Equal(FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.BlockedExecutorEvidenceBridgeNotReady, audit.Status);
		Assert.Contains(audit.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.EvidenceSummary
			&& row.Status == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedEvidenceSummaryUnavailable
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady.ToString()
			&& row.CurrentEvidence.Contains("rows=5", StringComparison.Ordinal)
			&& row.BlocksExecutableImplementation);
	}

	[Fact]
	public void Create_RuntimeMissingMetadataIsConsistentButStillFullyBlocked()
	{
		var audit = FindGroupMutationPostProjectedValueExecutorConsistencyAuditService.Create(
			MaterializationBlocker(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread),
			ResultEmissionGate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing),
			ResultEmissionBlocker(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable),
			EvidenceSummary(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing),
			ExecutorEvidenceBridge(FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus.BlockedExecutorImplementationUnavailable));

		Assert.Equal(FindGroupMutationPostProjectedValueExecutorConsistencyAuditStatus.ConsistentBlockedReadiness, audit.Status);
		Assert.Contains("internally consistent", audit.ExecutionDecision, StringComparison.Ordinal);
		Assert.All(audit.Rows, row =>
		{
			Assert.True(row.HasProvider);
			Assert.True(row.BlocksRuntimeComparison);
			Assert.True(row.BlocksLiveDispatch);
			Assert.True(row.BlocksVerifiedParity);
		});
		Assert.Contains(audit.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.ExecutorEvidenceBridge
			&& row.CurrentEvidence.Contains("executorEvidenceBridgeRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("resultEmissionBlockerRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("materializationBlockerEvidence=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("csharpHandoffCanFeedJavaArtifactPairing=True", StringComparison.Ordinal));
		Assert.Contains(audit.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRequirement.RuntimeComparisonAndLiveDispatch
			&& row.Status == FindGroupMutationPostProjectedValueExecutorConsistencyAuditRowStatus.BlockedRuntimeComparisonMissing
			&& row.CurrentEvidence.Contains("bridgeRuntimeRows=1", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("executorEvidenceBridgeRows=", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("resultEmissionBlockerRows=", StringComparison.Ordinal)
			&& row.Notes.Contains("verified parity remain blocked", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedValueMaterializationBlockerReport MaterializationBlocker(
		FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus status) =>
		new(
			status,
			OutputRows(MaterializationRow),
			OutputKindCount: 5,
			RequiredEqualityFieldCount: 38,
			UnreadEqualityFieldCount: 38,
			IgnoredRuntimeContextFieldCount: 4,
			HasProjectedValueRows: true,
			HasMaterializationPreflight: true,
			CanMaterializeMatched: false,
			CanMaterializeMissingJavaRow: false,
			CanMaterializeMissingCSharpRow: false,
			CanMaterializeFieldMismatch: false,
			CanAttachIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			"Projected-value materialization remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract ResultEmissionGate(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus status) =>
		new(
			status,
			OutputRows((order, outputKind) => new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow(
				order,
				outputKind,
				status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady
					? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMaterializationPreflightNotReady
					: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedRuntimeEvidenceMissing,
				["Runtime comparison evidence exists for action 2 and action 6."],
				["testField"],
				HasMaterializationPreflightRow: true,
				RequiresMaterializedOutput: outputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
				RequiresRuntimeComparison: true,
				RequiresParentResult: outputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
				CanEmitResult: false,
				"test gate evidence",
				"test gate notes")),
			OutputKindCount: 5,
			EmittableOutputCount: 0,
			HasMaterializationPreflight: true,
			HasAnyRuntimeEvidence: false,
			CanEmitMatched: false,
			CanEmitMissingJavaRow: false,
			CanEmitMissingCSharpRow: false,
			CanEmitFieldMismatch: false,
			CanEmitIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			"Result emission gate remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedValueResultEmissionBlockerReport ResultEmissionBlocker(
		FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus status) =>
		new(
			status,
			OutputRows(ResultEmissionBlockerRow),
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
			HasAnyRuntimeEvidence: false,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			"Evidence summary remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedValueExecutorEvidenceBridge ExecutorEvidenceBridge(
		FindGroupMutationPostProjectedValueExecutorEvidenceBridgeStatus status) =>
		new(
			status,
			[
				BridgeRow(1, FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.ResultEmissionBlocker),
				BridgeRow(2, FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.EvidenceSummary),
				BridgeRow(3, FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.ImplementationReadinessAudit),
				BridgeRow(4, FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.RuntimeComparisonHandoff),
			],
			HasResultEmissionBlockerReport: true,
			HasEvidenceSummary: true,
			HasAnyRuntimeEvidence: false,
			CanWriteExecutableExecutor: false,
			CanExecuteExecutor: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			"Executor evidence bridge remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedValueMaterializationBlockerRow MaterializationRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind) =>
		new(
			order,
			outputKind,
			FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedValueProjectionUnavailable,
			RequiredEqualityFieldCount: 38,
			UnreadEqualityFieldCount: 38,
			IgnoredRuntimeContextFieldCount: 4,
			["testField"],
			outputKind is FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched or FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch ? ["projectedField"] : [],
			RequiresProjectedValues: outputKind is FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched or FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
			RequiresMissingRowDecision: outputKind is FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow or FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
			AllowsRuntimeContextAttachment: outputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
			HasProjectedValueRows: true,
			HasMaterializationPreflightRow: true,
			HasUnreadProjectedValues: true,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			"test materialization evidence",
			"test required materialization evidence",
			"test materialization notes");

	private static FindGroupMutationPostProjectedValueResultEmissionBlockerRow ResultEmissionBlockerRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind) =>
		new(
			order,
			outputKind,
			FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedRuntimeEvidenceMissing,
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
			"test blocker evidence",
			"test required blocker evidence",
			"test blocker notes");

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
			"test summary notes");

	private static FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRow BridgeRow(
		int order,
		FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement requirement) =>
		new(
			order,
			requirement,
			requirement == FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRequirement.RuntimeComparisonHandoff
				? FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedRuntimeComparisonMissing
				: FindGroupMutationPostProjectedValueExecutorEvidenceBridgeRowStatus.BlockedExecutableImplementationDisabled,
			HasResultEmissionBlockerReport: true,
			HasEvidenceSummary: true,
			BlocksExecutableImplementation: true,
			BlocksRuntimeComparison: true,
			BlocksVerifiedParity: true,
			FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable.ToString(),
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing.ToString(),
			"test required bridge evidence",
			"resultEmissionBlockerRows=Matched=materializationBlockerEvidence=projectedValueRows=activePlayerObjectId=functionPreflightRows=ReaderImplementationGate=status=ReadyForFunctionExecutionBlocked; csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked; csharpHandoffCanFeedJavaArtifactPairing=True",
			"test bridge notes");

	private static T[] OutputRows<T>(Func<int, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind, T> factory) =>
	[
		factory(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched),
		factory(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow),
		factory(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow),
		factory(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch),
		factory(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext),
	];
}
