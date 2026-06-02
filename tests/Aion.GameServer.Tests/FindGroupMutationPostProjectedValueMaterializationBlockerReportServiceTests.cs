using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedValueMaterializationBlockerReportServiceTests
{
	[Fact]
	public void Create_DefaultReportBlocksBeforeProjectedValueRowsAreReady()
	{
		var report = FindGroupMutationPostProjectedValueMaterializationBlockerReportService.Create();

		Assert.Equal(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValueRowsNotReady, report.Status);
		Assert.False(report.IsLive);
		Assert.True(report.HasProjectedValueRows);
		Assert.True(report.HasMaterializationPreflight);
		Assert.Equal(5, report.OutputKindCount);
		Assert.Equal(38, report.RequiredEqualityFieldCount);
		Assert.Equal(38, report.UnreadEqualityFieldCount);
		Assert.Equal(4, report.IgnoredRuntimeContextFieldCount);
		Assert.False(report.CanMaterializeMatched);
		Assert.False(report.CanMaterializeMissingJavaRow);
		Assert.False(report.CanMaterializeMissingCSharpRow);
		Assert.False(report.CanMaterializeFieldMismatch);
		Assert.False(report.CanAttachIgnoredRuntimeContext);
		Assert.False(report.CanEmitAnyResult);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Contains("addRecruitment/addApplication", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("projected-value rows", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedProjectedValueRowsNotReady
			&& row.BlockingEvidence.Contains("projectedValueRows=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("functionPreflightRows=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("typedReaderGateRows=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("csharpHandoffStatus=BlockedMissingAcceptedBoundaryRows", StringComparison.Ordinal)
			&& !row.CanMaterializeOutput
			&& !row.CanEmitResult);
	}

	[Fact]
	public void Create_ReadyProjectedRowsStillBlockWhenMaterializationPreflightNotReady()
	{
		var report = FindGroupMutationPostProjectedValueMaterializationBlockerReportService.Create(
			ReadyProjectedRows(),
			MaterializationPreflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedIntakeNotReady));

		Assert.Equal(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedMaterializationPreflightNotReady, report.Status);
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMaterializationPreflightNotReady
			&& row.HasProjectedValueRows
			&& row.HasMaterializationPreflightRow
			&& row.BlockingEvidence.Contains("projectedValueRows=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("functionPreflightRows=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("csharpHandoffCanFeedJavaArtifactPairing=True", StringComparison.Ordinal)
			&& !row.CanMaterializeOutput);
	}

	[Fact]
	public void Create_RuntimeMissingReportMapsMatchedAndFieldMismatchToUnreadProjectedValues()
	{
		var report = FindGroupMutationPostProjectedValueMaterializationBlockerReportService.Create(
			ReadyProjectedRows(),
			MaterializationPreflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing));

		Assert.Equal(FindGroupMutationPostProjectedValueMaterializationBlockerReportStatus.BlockedProjectedValuesUnread, report.Status);
		Assert.Contains("unread projected values", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedValueProjectionUnavailable
			&& row.RequiresProjectedValues
			&& !row.RequiresMissingRowDecision
			&& !row.AllowsRuntimeContextAttachment
			&& row.RequiredProjectedFieldNames.Count == 19
			&& row.UnreadEqualityFieldCount == 38
			&& row.BlockingEvidence.Contains("projectedValueRows=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("functionPreflightRows=", StringComparison.Ordinal)
			&& row.BlockingEvidence.Contains("csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("all values equal", StringComparison.Ordinal)
			&& row.Notes.Contains("placeholder values", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch
			&& row.Status == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedValueProjectionUnavailable
			&& row.RequiresProjectedValues
			&& row.AllowsRuntimeContextAttachment
			&& row.RequiredEvidence.Contains("concrete differing field", StringComparison.Ordinal)
			&& row.Notes.Contains("unread placeholders", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingRowsRequireRowIdentityDecisionInsteadOfProjectedValues()
	{
		var report = FindGroupMutationPostProjectedValueMaterializationBlockerReportService.Create(
			ReadyProjectedRows(),
			MaterializationPreflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing));

		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow
			&& row.Status == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMissingRowDecisionUnavailable
			&& !row.RequiresProjectedValues
			&& row.RequiresMissingRowDecision
			&& row.RequiredProjectedFieldNames.Count == 0
			&& row.RequiredEvidence.Contains("accepted live C# row has no matching runtime-backed Java row", StringComparison.Ordinal)
			&& row.Notes.Contains("row identity matching", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow
			&& row.Status == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedMissingRowDecisionUnavailable
			&& !row.RequiresProjectedValues
			&& row.RequiresMissingRowDecision
			&& row.RequiredEvidence.Contains("runtime-backed Java row has no matching accepted live C# row", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_IgnoredRuntimeContextRequiresParentOutput()
	{
		var report = FindGroupMutationPostProjectedValueMaterializationBlockerReportService.Create(
			ReadyProjectedRows(),
			MaterializationPreflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing));

		Assert.Contains(report.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& row.Status == FindGroupMutationPostProjectedValueMaterializationBlockerRowStatus.BlockedContextAttachmentUnavailable
			&& !row.RequiresProjectedValues
			&& !row.RequiresMissingRowDecision
			&& row.AllowsRuntimeContextAttachment
			&& row.IgnoredRuntimeContextFieldCount == 4
			&& row.RequiredEvidence.Contains("not a standalone output", StringComparison.Ordinal)
			&& row.Notes.Contains("diagnostic", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostValueReaderProjectedValueRowContract ReadyProjectedRows()
	{
		var rows = Enumerable.Range(1, 38)
			.Select(order => ProjectedRow(
				order,
				order <= 19 ? 2 : 6,
				order <= 19 ? FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment : FindGroupDirectPacketMutationPostTraceMutationKind.Application,
				$"field{(order - 1) % 19 + 1}",
				FindGroupMutationPostProjectedRowComparisonValueReadMode.RequiredEqualityValue,
				FindGroupMutationPostValueReaderProjectedValueRowStatus.BlockedReaderInvocationDeferred,
				FindGroupMutationPostValueReaderProjectedValueReadStatus.NotRead,
				"<not-read>",
				RequiresRows: true))
			.Concat(Enumerable.Range(39, 4)
				.Select(order => ProjectedRow(
					order,
					order <= 40 ? 2 : 6,
					order <= 40 ? FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment : FindGroupDirectPacketMutationPostTraceMutationKind.Application,
					order % 2 == 1 ? "traceSource" : "serverEpochSeconds",
					FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext,
					FindGroupMutationPostValueReaderProjectedValueRowStatus.IgnoredRuntimeContextOnly,
					FindGroupMutationPostValueReaderProjectedValueReadStatus.IgnoredRuntimeContext,
					"<ignored-runtime-context>",
					RequiresRows: false)))
			.ToArray();

		return new FindGroupMutationPostValueReaderProjectedValueRowContract(
			FindGroupMutationPostValueReaderProjectedValueRowContractStatus.ReadyForProjectedRowsBlocked,
			rows,
			HasFunctionExecutionPreflight: true,
			HasExecutorImplementationPlan: true,
			HasTypedValueReaderPreflight: true,
			HasProjectedValueRows: true,
			RequiredEqualityFieldCount: 38,
			IgnoredRuntimeContextFieldCount: 4,
			CanInvokeReaderFunctions: false,
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanAttachRuntimeContext: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			"Projected rows ready but unread.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
	}

	private static FindGroupMutationPostValueReaderProjectedValueRow ProjectedRow(
		int order,
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind,
		string fieldName,
		FindGroupMutationPostProjectedRowComparisonValueReadMode readMode,
		FindGroupMutationPostValueReaderProjectedValueRowStatus status,
		FindGroupMutationPostValueReaderProjectedValueReadStatus readStatus,
		string value,
		bool RequiresRows) =>
		new(
			order,
			action,
			mutationKind,
			fieldName,
			FindGroupMutationPostProjectedRowComparisonValueReaderKind.Int32Scalar,
			readMode,
			status,
			readStatus,
			readStatus,
			"int",
			RequiresRows ? "ReadJavaInt32Scalar" : "AttachJavaMismatchContext",
			RequiresRows ? "ReadCSharpInt32Scalar" : "AttachCSharpMismatchContext",
			value,
			value,
			RequiresJavaRow: RequiresRows,
			RequiresCSharpRow: RequiresRows,
			RequiresReaderFunctions: RequiresRows,
			PreservesCollectionOrder: false,
			CanReadJavaValue: false,
			CanReadCSharpValue: false,
			CanCompareValue: false,
			CanEmitResult: false,
			"test blocker",
			"CM_FIND_GROUP.runImpl action 2 -> FindGroupService.addRecruitment; action 6 -> FindGroupService.addApplication",
			"functionPreflightRows=ReaderImplementationGate=status=ReadyForFunctionExecutionBlocked; typedReaderGateRows=RuntimeRowValueIntake=runtimeRowValueIntakeRows=RuntimeRowValueIntake=status=ReadyForRuntimeRowsValueReadersBlocked; csharpHandoffStatus=ReadyForJavaArtifactPairingRuntimeComparisonBlocked; csharpHandoffCanFeedJavaArtifactPairing=True",
			"test notes");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract MaterializationPreflight(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus status) =>
		new(
			status,
			[
				PreflightRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched, ["matchedFields", "matchedFieldCount"]),
				PreflightRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow, ["csharpRowReference", "runtimeContext"]),
				PreflightRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow, ["javaRowReference", "runtimeContext"]),
				PreflightRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch, ["fieldName", "javaValue", "csharpValue", "runtimeContext"]),
				PreflightRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext, ["traceSource", "serverEpochSeconds"]),
			],
			OutputKindCount: 5,
			MaterializableOutputCount: 0,
			EmittableOutputCount: 0,
			HasRuntimeEvidenceIntake: true,
			HasBlockedOutputPreview: true,
			HasAnyRuntimeEvidence: status != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing,
			CanMaterializeMatched: false,
			CanMaterializeMissingJavaRow: false,
			CanMaterializeMissingCSharpRow: false,
			CanMaterializeFieldMismatch: false,
			CanAttachIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			"Materialization preflight remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRow PreflightRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		IReadOnlyList<string> schemaFields) =>
		new(
			order,
			outputKind,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedRuntimeEvidenceMissing,
			[FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison],
			schemaFields,
			HasPreviewRow: true,
			HasRuntimeEvidenceIntake: true,
			HasAllRequiredIntakeRows: true,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			"test blocking evidence",
			"test required evidence",
			"test notes");
}
