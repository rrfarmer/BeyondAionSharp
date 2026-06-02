namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonDryRunStatus
{
	BlockedByExecutionReport,
	ReadyForFutureExecutor,
}

public enum FindGroupMutationPostProjectedRowComparisonDryRunOutputKind
{
	Matched,
	MissingJavaRow,
	MissingCSharpRow,
	FieldMismatch,
	IgnoredRuntimeContext,
}

public enum FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus
{
	RequiredEqualityInput,
	IgnoredRuntimeContext,
}

public sealed record FindGroupMutationPostProjectedRowComparisonDryRunField(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus Status,
	FindGroupMutationPostComparisonDifferenceKind DifferenceKind,
	string PlannedOutputShape,
	string JavaSource,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonDryRunAction(
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string JavaMethod,
	string RequiredRowIdentity,
	int ExpectedPostedSystemMessageId,
	int ExpectedRefreshedListAction,
	string PlannedMatchOutput);

public sealed record FindGroupMutationPostProjectedRowComparisonDryRunAcceptedCSharpRowReference(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	FindGroupMutationPostGuardedFixtureCandidateRowStatus GuardedStatus,
	string RequiredRowIdentity,
	bool IsAcceptedLiveBoundaryEvidence,
	string Evidence,
	string PlannedInputSource);

public sealed record FindGroupMutationPostProjectedRowComparisonDryRunAcceptedJavaRowReference(
	int Order,
	int Action,
	string MutationKind,
	string RequiredRowIdentity,
	bool IsShapeValidJavaArtifact,
	string Evidence,
	string PlannedInputSource);

public sealed record FindGroupMutationPostProjectedRowComparisonDryRunPairedRowReadiness(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string RequiredRowIdentity,
	bool HasAcceptedJavaRow,
	bool HasAcceptedCSharpRow,
	bool IsReadyForFutureComparisonInput,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonDryRunContract(
	FindGroupMutationPostProjectedRowComparisonDryRunStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunAction> Actions,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunAcceptedJavaRowReference> AcceptedJavaRows,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunAcceptedCSharpRowReference> AcceptedCSharpRows,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunPairedRowReadiness> PairedRowReadiness,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunField> Fields,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind> OutputKinds,
	bool HasExecutionBlockerReport,
	bool HasResultContract,
	bool HasJavaArtifactDirectoryReport,
	bool HasGuardedFixtureResultContract,
	bool ShouldCompareRows,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: dry-run contract for a future CM_FIND_GROUP action 2/6
/// projected-row comparison executor. This names inputs and planned outputs, but it
/// does not compare Java and C# rows.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonDryRunContractService
{
	public static FindGroupMutationPostProjectedRowComparisonDryRunContract Create(
		FindGroupMutationPostComparisonExecutionBlockerReport? blockerReport = null,
		FindGroupMutationPostComparisonExecutionResultContract? resultContract = null,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? javaArtifacts = null,
		FindGroupMutationPostGuardedFixtureResultContract? guardedFixtureResultContract = null)
	{
		blockerReport ??= FindGroupMutationPostComparisonExecutionBlockerReportService.Create();
		resultContract ??= FindGroupMutationPostComparisonExecutionResultContractService.Create();
		javaArtifacts ??= FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create();
		guardedFixtureResultContract ??= FindGroupMutationPostGuardedFixtureResultContractService.Create();

		var actions = resultContract.Actions
			.Select(action => new FindGroupMutationPostProjectedRowComparisonDryRunAction(
				action.Action,
				action.MutationKind,
				action.JavaMethod,
				action.RowIdentityFields,
				action.ExpectedPostedSystemMessageId,
				action.ExpectedRefreshedListAction,
				$"Emit {FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched} only when all required equality fields match for action={action.Action}."))
			.ToArray();
		var acceptedJavaRows = javaArtifacts.Files
			.Where(file => file.Status == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid)
			.SelectMany(file => file.ValidationReport?.Metadata?.TraceRows
				.Where(row => row.Action == file.Action)
				.Select(row => CreateAcceptedJavaRowReference(file, row, resultContract)) ?? [])
			.Select((row, index) => row with { Order = index + 1 })
			.ToArray();
		var acceptedRows = guardedFixtureResultContract.AcceptedLiveRows
			.Select((row, index) => CreateAcceptedCSharpRowReference(index + 1, row, resultContract))
			.ToArray();
		var pairedReadiness = actions
			.Select((action, index) => CreatePairedRowReadiness(index + 1, action, acceptedJavaRows, acceptedRows))
			.ToArray();
		var fields = resultContract.Fields
			.Select((field, index) => CreateField(index + 1, field))
			.ToArray();
		var status = blockerReport.ShouldExecuteComparison
			? FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor
			: FindGroupMutationPostProjectedRowComparisonDryRunStatus.BlockedByExecutionReport;

		return new FindGroupMutationPostProjectedRowComparisonDryRunContract(
			status,
			actions,
			acceptedJavaRows,
			acceptedRows,
			pairedReadiness,
			fields,
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			],
			HasExecutionBlockerReport: blockerReport.Rows.Count > 0 || blockerReport.ShouldExecuteComparison,
			HasResultContract: resultContract.Fields.Count > 0,
			HasJavaArtifactDirectoryReport: javaArtifacts.Files.Count > 0,
			HasGuardedFixtureResultContract: guardedFixtureResultContract.Requirements.Count > 0,
			ShouldCompareRows: blockerReport.ShouldExecuteComparison,
			blockerReport.ExecutionDecision,
			resultContract.TraceName,
			resultContract.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonDryRunPairedRowReadiness CreatePairedRowReadiness(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunAction action,
		IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunAcceptedJavaRowReference> acceptedJavaRows,
		IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunAcceptedCSharpRowReference> acceptedCSharpRows)
	{
		var hasJavaRow = acceptedJavaRows.Any(row => row.Action == action.Action
			&& row.MutationKind == action.MutationKind.ToString()
			&& row.RequiredRowIdentity == action.RequiredRowIdentity
			&& row.IsShapeValidJavaArtifact);
		var hasCSharpRow = acceptedCSharpRows.Any(row => row.Action == action.Action
			&& row.MutationKind == action.MutationKind
			&& row.RequiredRowIdentity == action.RequiredRowIdentity
			&& row.IsAcceptedLiveBoundaryEvidence);
		var readiness = hasJavaRow && hasCSharpRow;

		return new FindGroupMutationPostProjectedRowComparisonDryRunPairedRowReadiness(
			order,
			action.Action,
			action.MutationKind,
			action.RequiredRowIdentity,
			hasJavaRow,
			hasCSharpRow,
			readiness,
			$"action={action.Action}; mutationKind={action.MutationKind}; hasJavaRow={hasJavaRow}; hasCSharpRow={hasCSharpRow}; requiredIdentity={action.RequiredRowIdentity}",
			readiness
				? "Both accepted row references exist for a future executor input, but this dry-run still does not compare Java/C# values."
				: "Future executor input is incomplete; this dry-run still does not compare Java/C# values.");
	}

	private static FindGroupMutationPostProjectedRowComparisonDryRunAcceptedJavaRowReference CreateAcceptedJavaRowReference(
		FindGroupMutationPostJavaTraceArtifactDirectoryFileRow file,
		FindGroupMutationPostJavaTraceArtifactValidationTraceRow row,
		FindGroupMutationPostComparisonExecutionResultContract resultContract)
	{
		var action = resultContract.Actions.Single(item => item.Action == row.Action);
		return new FindGroupMutationPostProjectedRowComparisonDryRunAcceptedJavaRowReference(
			Order: 0,
			row.Action,
			row.MutationKind,
			action.RowIdentityFields,
			IsShapeValidJavaArtifact: true,
			$"path={file.Path}; action={row.Action}; mutationKind={row.MutationKind}; posted={row.PostedSystemMessageId}; refreshed={row.RefreshedListAction}; status={file.Status}",
			"Shape-valid Java artifact row from FindGroupMutationPostJavaTraceArtifactDirectoryReportService; future executor may use this row only after blocker report allows comparison.");
	}

	private static FindGroupMutationPostProjectedRowComparisonDryRunAcceptedCSharpRowReference CreateAcceptedCSharpRowReference(
		int order,
		FindGroupMutationPostGuardedFixtureCandidateRow row,
		FindGroupMutationPostComparisonExecutionResultContract resultContract)
	{
		var action = resultContract.Actions.Single(item => item.Action == row.Action);
		return new FindGroupMutationPostProjectedRowComparisonDryRunAcceptedCSharpRowReference(
			order,
			row.Action,
			row.MutationKind,
			row.Status,
			action.RowIdentityFields,
			row.IsLiveBoundaryEvidence,
			row.Evidence,
			"Accepted C# row from FindGroupMutationPostGuardedFixtureResultContractService; future executor may use this row only after blocker report allows comparison.");
	}

	private static FindGroupMutationPostProjectedRowComparisonDryRunField CreateField(
		int order,
		FindGroupMutationPostComparisonExecutionResultFieldContract field)
	{
		var status = field.Status == FindGroupMutationPostComparisonDifferenceFieldStatus.IgnoredForEquality
			? FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus.IgnoredRuntimeContext
			: FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus.RequiredEqualityInput;
		var outputShape = status == FindGroupMutationPostProjectedRowComparisonDryRunFieldStatus.IgnoredRuntimeContext
			? $"Copy {field.FieldName} into {FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext} only when a mismatch report needs runtime context."
			: $"If {field.FieldName} differs, emit {FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch} with differenceKind={field.DifferenceKind}, javaValue, csharpValue, and Java source evidence.";

		return new FindGroupMutationPostProjectedRowComparisonDryRunField(
			order,
			field.Action,
			field.MutationKind,
			field.FieldName,
			status,
			field.DifferenceKind,
			outputShape,
			field.JavaSource,
			field.Notes);
	}
}
