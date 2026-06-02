namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus
{
	BlockedImplementationPlanNotReady,
	BlockedRuntimeRowsMissing,
	BlockedOutputEmissionDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus
{
	BlockedImplementationPlanNotReady,
	BlockedValueProjectionUnavailable,
	BlockedMissingRowDecisionUnavailable,
	BlockedContextAttachmentUnavailable,
	BlockedResultEmissionDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonDryRunOutputKind OutputKind,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus Status,
	IReadOnlyList<string> SchemaFields,
	bool RequiresProjectedValues,
	bool RequiresMissingRowDecision,
	bool AllowsRuntimeContextAttachment,
	bool HasImplementationPlanStep,
	bool HasResultSchemaRow,
	bool CanMaterializeOutput,
	bool CanEmitResult,
	string RequiredExecutorStep,
	string BlockingEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRow> Rows,
	int OutputKindCount,
	int MaterializableOutputCount,
	int EmittableOutputCount,
	bool HasImplementationPlan,
	bool HasResultSchema,
	bool HasRuntimeEvidence,
	bool CanMaterializeMatched,
	bool CanMaterializeMissingJavaRow,
	bool CanMaterializeMissingCSharpRow,
	bool CanMaterializeFieldMismatch,
	bool CanAttachIgnoredRuntimeContext,
	bool CanEmitAnyResult,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live blocked-output preview for future
/// CM_FIND_GROUP action 2/6 value-reader executor results. It projects which
/// result kinds remain unavailable from the implementation plan and result
/// schema without materializing any comparison output.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract? implementationPlan = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContract? resultSchema = null)
	{
		implementationPlan ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService.Create();
		resultSchema ??= FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService.Create();

		var status = StatusFor(implementationPlan, resultSchema);
		var rows = resultSchema.Rows
			.Select(row => PreviewRow(row, implementationPlan, status))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract(
			status,
			rows,
			OutputKindCount: rows.Length,
			MaterializableOutputCount: 0,
			EmittableOutputCount: 0,
			HasImplementationPlan: implementationPlan.Steps.Count > 0,
			HasResultSchema: resultSchema.Rows.Count > 0,
			HasRuntimeEvidence: implementationPlan.HasRuntimeEvidence,
			CanMaterializeMatched: false,
			CanMaterializeMissingJavaRow: false,
			CanMaterializeMissingCSharpRow: false,
			CanMaterializeFieldMismatch: false,
			CanAttachIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			resultSchema.TraceName,
			resultSchema.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract implementationPlan,
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContract resultSchema)
	{
		if (implementationPlan.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus.BlockedExecutorReadinessGateNotReady
			|| resultSchema.Status == FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus.BlockedRunbookNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedImplementationPlanNotReady;

		if (!implementationPlan.HasRuntimeEvidence)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing;

		return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedOutputEmissionDeferred;
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRow PreviewRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow resultRow,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract implementationPlan,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus previewStatus)
	{
		var requiredStep = RequiredStepFor(resultRow.OutputKind);
		var planStep = implementationPlan.Steps.FirstOrDefault(step => step.Step == requiredStep);
		var status = RowStatusFor(previewStatus, resultRow.OutputKind);

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRow(
			resultRow.Order,
			resultRow.OutputKind,
			status,
			resultRow.SchemaFields,
			resultRow.RequiresProjectedValues,
			resultRow.RequiresMissingRowDecision,
			resultRow.AllowsRuntimeContextAttachment,
			HasImplementationPlanStep: planStep is not null,
			HasResultSchemaRow: true,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			requiredStep.ToString(),
			$"previewStatus={previewStatus}; schemaRowStatus={resultRow.Status}; planStatus={implementationPlan.Status}; planStepStatus={planStep?.Status.ToString() ?? "Missing"}; hasRuntimeEvidence={implementationPlan.HasRuntimeEvidence}; canEmitResult={resultRow.CanEmitResult}",
			NotesFor(resultRow.OutputKind));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep RequiredStepFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus previewStatus,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		if (previewStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedImplementationPlanNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedImplementationPlanNotReady;

		if (previewStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedOutputEmissionDeferred)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedResultEmissionDeferred;

		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedMissingRowDecisionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedMissingRowDecisionUnavailable,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedContextAttachmentUnavailable,
		};
	}

	private static string NotesFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "Matched output remains unavailable until every equality value is projected and equal; ignored runtime context must not attach.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "MissingJavaRow output remains unavailable until a live C# row exists without a matching runtime-backed Java row.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "MissingCSharpRow output remains unavailable until a runtime-backed Java row exists without a matching live C# row.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "FieldMismatch output remains unavailable until a projected Java/C# equality field differs and mismatch context can attach diagnostically.",
			_ => "Ignored runtime context remains unavailable as standalone output and may attach only after MissingJavaRow, MissingCSharpRow, or FieldMismatch exists.",
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedImplementationPlanNotReady => "Value-reader executor output preview is blocked until implementation-plan and result-schema metadata are ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing => "Value-reader executor output preview is blocked because runtime-backed Java rows, accepted C# rows, and projected values are missing.",
			_ => "Value-reader executor output preview is defined, but output materialization and result emission remain intentionally deferred.",
		};
	}
}
