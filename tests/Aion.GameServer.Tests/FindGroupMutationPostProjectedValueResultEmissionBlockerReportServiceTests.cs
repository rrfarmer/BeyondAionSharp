using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedValueResultEmissionBlockerReportServiceTests
{
	[Fact]
	public void Create_DefaultReportBlocksBeforeMaterializationBlockerIsReady()
	{
		var report = FindGroupMutationPostProjectedValueResultEmissionBlockerReportService.Create();

		Assert.Equal(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedMaterializationBlockerReportNotReady, report.Status);
		Assert.False(report.IsLive);
		Assert.True(report.HasMaterializationBlockerReport);
		Assert.True(report.HasResultEmissionGate);
		Assert.Equal(5, report.OutputKindCount);
		Assert.Equal(0, report.EmittableOutputCount);
		Assert.False(report.CanEmitMatched);
		Assert.False(report.CanEmitMissingJavaRow);
		Assert.False(report.CanEmitMissingCSharpRow);
		Assert.False(report.CanEmitFieldMismatch);
		Assert.False(report.CanEmitIgnoredRuntimeContext);
		Assert.False(report.CanEmitAnyResult);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Contains("addRecruitment/addApplication", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("materialization blocker report", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.All(report.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedMaterializationBlockerReportNotReady, row.Status);
			Assert.True(row.HasMaterializationBlockerRow);
			Assert.True(row.HasEmissionGateRow);
			Assert.False(row.CanMaterializeOutput);
			Assert.False(row.CanEmitResult);
			Assert.Contains("materializationBlockerEvidence=", row.BlockingEvidence, StringComparison.Ordinal);
			Assert.Contains("projectedValueRows=", row.BlockingEvidence, StringComparison.Ordinal);
			Assert.Contains("csharpHandoffStatus=BlockedMissingAcceptedBoundaryRows", row.BlockingEvidence, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void Create_ReadyMaterializationBlockerStillBlocksWhenEmissionGateNotReady()
	{
		var report = FindGroupMutationPostProjectedValueResultEmissionBlockerReportService.Create(
			MaterializationBlocker(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread),
			ResultEmissionGate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady));

		Assert.Equal(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionGateNotReady, report.Status);
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedResultEmissionGateNotReady
			&& row.MaterializationBlockerStatus == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedValueProjectionUnavailable
			&& row.EmissionGateStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMaterializationPreflightNotReady
			&& row.RequiredEmissionConditions.Any(condition => condition.Contains("Every schema equality field", StringComparison.Ordinal))
			&& row.BlockingEvidence.Contains("materializationBlockerEvidence=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("projectedValueRows=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("csharpHandoffCanFeedJavaArtifactPairing=True", StringComparison.Ordinal)
			&& !row.CanEmitResult);
	}

	[Fact]
	public void Create_RuntimeMissingReportMapsMatchedAndFieldMismatchToValueProjectionBlockers()
	{
		var report = FindGroupMutationPostProjectedValueResultEmissionBlockerReportService.Create(
			MaterializationBlocker(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread),
			ResultEmissionGate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing));

		Assert.Equal(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable, report.Status);
		Assert.Contains("materialization, result emission, runtime comparison, and verified parity remain unavailable", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedValueProjectionUnavailable
			&& row.RequiresMaterializedOutput
			&& row.RequiresRuntimeComparison
			&& !row.RequiresParentResult
			&& row.RequiredProjectedFieldNames.SequenceEqual(["activePlayerObjectId", "message", "groupType"])
			&& row.BlockingEvidence.Contains("materializationBlockerEvidence=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("projectedValueRows=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("all values equal", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("No ignored runtime context", StringComparison.Ordinal)
			&& row.Notes.Contains("projected values are placeholders", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch
			&& row.Status == FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedValueProjectionUnavailable
			&& row.RequiredSchemaFields.SequenceEqual(["fieldName", "javaValue", "csharpValue", "runtimeContext"])
			&& row.RequiredEvidence.Contains("concrete field name", StringComparison.Ordinal)
			&& row.Notes.Contains("both projected values", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingRowsRequireMaterializedMissingRowDecision()
	{
		var report = FindGroupMutationPostProjectedValueResultEmissionBlockerReportService.Create(
			MaterializationBlocker(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread),
			ResultEmissionGate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing));

		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow
			&& row.Status == FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedMissingRowDecisionUnavailable
			&& row.MaterializationBlockerStatus == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMissingRowDecisionUnavailable
			&& row.RequiresMaterializedOutput
			&& row.RequiredEvidence.Contains("no runtime-backed Java row exists", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("accepted live C# row has no matching runtime-backed Java row", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow
			&& row.Status == FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedMissingRowDecisionUnavailable
			&& row.MaterializationBlockerStatus == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMissingRowDecisionUnavailable
			&& row.RequiredEvidence.Contains("no accepted live C# row exists", StringComparison.Ordinal)
			&& row.Notes.Contains("accepted live C# boundary rows", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_IgnoredRuntimeContextRequiresParentResultAndIsNotStandalone()
	{
		var report = FindGroupMutationPostProjectedValueResultEmissionBlockerReportService.Create(
			MaterializationBlocker(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread),
			ResultEmissionGate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing));

		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& row.Status == FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedContextAttachmentUnavailable
			&& !row.RequiresMaterializedOutput
			&& row.RequiresParentResult
			&& row.RequiredSchemaFields.SequenceEqual(["traceSource", "serverEpochSeconds"])
			&& row.RequiredEvidence.Contains("must not emit as a standalone result", StringComparison.Ordinal)
			&& row.Notes.Contains("never creates a standalone result", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DeferredGateDisablesEveryResultEmission()
	{
		var report = FindGroupMutationPostProjectedValueResultEmissionBlockerReportService.Create(
			MaterializationBlocker(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread),
			ResultEmissionGate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedResultEmissionDeferred));

		Assert.Equal(FindGroupMutationPostProjectedValueResultEmissionBlockerReportStatus.BlockedResultEmissionUnavailable, report.Status);
		Assert.All(report.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedValueResultEmissionBlockerRowStatus.BlockedResultEmissionDisabled, row.Status);
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedResultEmissionDisabled, row.EmissionGateStatus);
			Assert.False(row.CanEmitResult);
			Assert.Contains("gateCanEmitResult=False", row.BlockingEvidence, StringComparison.Ordinal);
			Assert.Contains("materializationBlockerEvidence=", row.BlockingEvidence, StringComparison.Ordinal);
		});
	}

	private static FindGroupMutationPostProjectedValueMaterializationBlockerReport MaterializationBlocker(
		FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus status) =>
		new(
			status,
			[
				MaterializationBlockerRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched, FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedValueProjectionUnavailable, ["matchedFields", "matchedFieldCount"], ["activePlayerObjectId", "message", "groupType"]),
				MaterializationBlockerRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow, FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMissingRowDecisionUnavailable, ["csharpRowReference", "runtimeContext"], []),
				MaterializationBlockerRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow, FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMissingRowDecisionUnavailable, ["javaRowReference", "runtimeContext"], []),
				MaterializationBlockerRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch, FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedValueProjectionUnavailable, ["fieldName", "javaValue", "csharpValue", "runtimeContext"], ["activePlayerObjectId", "message", "groupType"]),
				MaterializationBlockerRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext, FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedContextAttachmentUnavailable, ["traceSource", "serverEpochSeconds"], []),
			],
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
			"Projected-value materialization blocker remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedValueMaterializationBlockerRow MaterializationBlockerRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus status,
		IReadOnlyList<string> schemaFields,
		IReadOnlyList<string> projectedFieldNames) =>
		new(
			order,
			outputKind,
			status,
			RequiredEqualityFieldCount: 38,
			UnreadEqualityFieldCount: 38,
			IgnoredRuntimeContextFieldCount: 4,
			schemaFields,
			projectedFieldNames,
			RequiresProjectedValues: projectedFieldNames.Count > 0,
			RequiresMissingRowDecision: outputKind is FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow or FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
			AllowsRuntimeContextAttachment: outputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
			HasProjectedValueRows: true,
			HasMaterializationPreflightRow: true,
			HasUnreadProjectedValues: true,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			"projectedValueRows=activePlayerObjectId=functionPreflightRows=ReaderImplementationGate=status=ReadyForFunctionExecutionBlocked; typedReaderGateRows=RuntimeRowValueIntake=runtimeRowValueIntakeRows=RuntimeRowValueIntake=status=ReadyForRuntimeRowsValueReadersBlocked; csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked; csharpHandoffCanFeedJavaArtifactPairing=True",
			RequiredEvidenceFor(outputKind),
			"test materialization blocker notes");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract ResultEmissionGate(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus status) =>
		new(
			status,
			[
				GateRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched, RowStatusFor(status, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched), ["matchedFields", "matchedFieldCount"]),
				GateRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow, RowStatusFor(status, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow), ["csharpRowReference", "runtimeContext"]),
				GateRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow, RowStatusFor(status, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow), ["javaRowReference", "runtimeContext"]),
				GateRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch, RowStatusFor(status, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch), ["fieldName", "javaValue", "csharpValue", "runtimeContext"]),
				GateRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext, RowStatusFor(status, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext), ["traceSource", "serverEpochSeconds"]),
			],
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

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow GateRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus status,
		IReadOnlyList<string> schemaFields) =>
		new(
			order,
			outputKind,
			status,
			ConditionsFor(outputKind),
			schemaFields,
			HasMaterializationPreflightRow: true,
			RequiresMaterializedOutput: outputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			RequiresRuntimeComparison: true,
			RequiresParentResult: outputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			CanEmitResult: false,
			"test gate blocker evidence",
			outputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
				? "IgnoredRuntimeContext remains diagnostic attachment data and never creates a standalone result."
				: "test gate notes");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus status,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		if (status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMaterializationPreflightNotReady;

		if (status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedResultEmissionDeferred)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedResultEmissionDisabled;

		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMissingRowDecisionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMissingRowDecisionUnavailable,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedContextAttachmentUnavailable,
		};
	}

	private static IReadOnlyList<string> ConditionsFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => [
				"Every schema equality field has projected Java and C# values.",
				"All projected equality values compare equal.",
				"No ignored runtime context is attached to Matched output.",
			],
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => [
				"Materialized MissingJavaRow exists after accepted live C# row has no matching runtime-backed Java row.",
				"Runtime comparison evidence exists for action 2 and action 6.",
			],
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => [
				"Materialized MissingCSharpRow exists after runtime-backed Java row has no matching accepted live C# row.",
				"Runtime comparison evidence exists for action 2 and action 6.",
			],
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => [
				"Differing field name, difference kind, Java value, and C# value are selected.",
				"Runtime comparison evidence exists for action 2 and action 6.",
			],
			_ => [
				"IgnoredRuntimeContext is attached to an emitted MissingJavaRow, MissingCSharpRow, or FieldMismatch result.",
				"IgnoredRuntimeContext is not emitted as an independent result row.",
			],
		};
	}

	private static string RequiredEvidenceFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "Matched materialization requires all values equal.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "FieldMismatch materialization requires concrete differing field.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "MissingJavaRow materialization requires accepted live C# row has no matching runtime-backed Java row.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "MissingCSharpRow materialization requires runtime-backed Java row has no matching accepted live C# row.",
			_ => "IgnoredRuntimeContext can attach only after parent output materializes.",
		};
	}
}
