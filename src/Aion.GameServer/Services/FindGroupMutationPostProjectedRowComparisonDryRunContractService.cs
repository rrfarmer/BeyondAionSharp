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

public sealed record FindGroupMutationPostProjectedRowComparisonDryRunContract(
	FindGroupMutationPostProjectedRowComparisonDryRunStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunAction> Actions,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunField> Fields,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonDryRunOutputKind> OutputKinds,
	bool HasExecutionBlockerReport,
	bool HasResultContract,
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
		FindGroupMutationPostComparisonExecutionResultContract? resultContract = null)
	{
		blockerReport ??= FindGroupMutationPostComparisonExecutionBlockerReportService.Create();
		resultContract ??= FindGroupMutationPostComparisonExecutionResultContractService.Create();

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
		var fields = resultContract.Fields
			.Select((field, index) => CreateField(index + 1, field))
			.ToArray();
		var status = blockerReport.ShouldExecuteComparison
			? FindGroupMutationPostProjectedRowComparisonDryRunStatus.ReadyForFutureExecutor
			: FindGroupMutationPostProjectedRowComparisonDryRunStatus.BlockedByExecutionReport;

		return new FindGroupMutationPostProjectedRowComparisonDryRunContract(
			status,
			actions,
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
			ShouldCompareRows: blockerReport.ShouldExecuteComparison,
			blockerReport.ExecutionDecision,
			resultContract.TraceName,
			resultContract.JavaSource,
			IsLive: false);
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
