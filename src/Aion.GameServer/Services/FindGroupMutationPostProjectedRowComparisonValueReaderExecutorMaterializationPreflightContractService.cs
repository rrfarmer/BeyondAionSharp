namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus
{
	BlockedIntakeNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedOutputPreviewNotMaterializable,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus
{
	BlockedIntakeNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedMissingRowDecisionUnavailable,
	BlockedValueProjectionUnavailable,
	BlockedContextAttachmentUnavailable,
	BlockedRuntimeComparisonMissing,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonDryRunOutputKind OutputKind,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement> RequiredIntakeRequirements,
	IReadOnlyList<string> RequiredSchemaFields,
	bool HasPreviewRow,
	bool HasRuntimeEvidenceIntake,
	bool HasAllRequiredIntakeRows,
	bool CanMaterializeOutput,
	bool CanEmitResult,
	string BlockingEvidence,
	string RequiredEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRow> Rows,
	int OutputKindCount,
	int MaterializableOutputCount,
	int EmittableOutputCount,
	bool HasRuntimeEvidenceIntake,
	bool HasBlockedOutputPreview,
	bool HasAnyRuntimeEvidence,
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
/// Java parity breadcrumb: non-live materialization preflight for future
/// CM_FIND_GROUP action 2/6 value-reader executor outputs. It joins runtime
/// evidence intake with blocked output preview rows so a future executor cannot
/// materialize result rows until every Java/C# runtime prerequisite is present.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract? runtimeEvidenceIntake = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract? blockedOutputPreview = null)
	{
		runtimeEvidenceIntake ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService.Create();
		blockedOutputPreview ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService.Create();

		var status = StatusFor(runtimeEvidenceIntake, blockedOutputPreview);
		var rows = blockedOutputPreview.Rows
			.Select(row => MaterializationRow(row, runtimeEvidenceIntake, status))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract(
			status,
			rows,
			OutputKindCount: rows.Length,
			MaterializableOutputCount: 0,
			EmittableOutputCount: 0,
			HasRuntimeEvidenceIntake: runtimeEvidenceIntake.Rows.Count > 0,
			HasBlockedOutputPreview: blockedOutputPreview.Rows.Count > 0,
			HasAnyRuntimeEvidence: false,
			CanMaterializeMatched: false,
			CanMaterializeMissingJavaRow: false,
			CanMaterializeMissingCSharpRow: false,
			CanMaterializeFieldMismatch: false,
			CanAttachIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			blockedOutputPreview.TraceName,
			blockedOutputPreview.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract runtimeEvidenceIntake,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract blockedOutputPreview)
	{
		if (runtimeEvidenceIntake.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedLiveInputHandoffNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedIntakeNotReady;

		if (runtimeEvidenceIntake.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing
			|| blockedOutputPreview.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing;

		return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedOutputPreviewNotMaterializable;
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRow MaterializationRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRow previewRow,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract runtimeEvidenceIntake,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus preflightStatus)
	{
		var requirements = RequirementsFor(previewRow.OutputKind);
		var hasAllRequiredIntakeRows = requirements.All(requirement => runtimeEvidenceIntake.Rows.Any(row => row.Requirement == requirement));

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRow(
			previewRow.Order,
			previewRow.OutputKind,
			RowStatusFor(previewRow, preflightStatus),
			requirements,
			previewRow.SchemaFields,
			HasPreviewRow: true,
			HasRuntimeEvidenceIntake: runtimeEvidenceIntake.Rows.Count > 0,
			hasAllRequiredIntakeRows,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			$"intakeStatus={runtimeEvidenceIntake.Status}; previewStatus={previewRow.Status}; requiredIntakeRows={string.Join(",", requirements)}; hasAllRequiredIntakeRows={hasAllRequiredIntakeRows}; canMaterializePreviewRow={previewRow.CanMaterializeOutput}; canEmitPreviewRow={previewRow.CanEmitResult}",
			RequiredEvidenceFor(previewRow.OutputKind),
			NotesFor(previewRow.OutputKind));
	}

	private static IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement> RequirementsFor(
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => [
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.CSharpBoundaryRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RowIdentityMatching,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ValueProjection,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ResultOutputPrerequisites,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison,
			],
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => MissingRowRequirements(),
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => MissingRowRequirements(),
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => [
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.CSharpBoundaryRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RowIdentityMatching,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ValueProjection,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ResultOutputPrerequisites,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison,
			],
			_ => [
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.CSharpBoundaryRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ResultOutputPrerequisites,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison,
			],
		};
	}

	private static IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement> MissingRowRequirements() =>
	[
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.CSharpBoundaryRows,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RowIdentityMatching,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ResultOutputPrerequisites,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison,
	];

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRow previewRow,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus preflightStatus)
	{
		if (preflightStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedIntakeNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedIntakeNotReady;

		if (preflightStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedOutputPreviewNotMaterializable)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedRuntimeComparisonMissing;

		return previewRow.OutputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedValueProjectionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedMissingRowDecisionUnavailable,
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedMissingRowDecisionUnavailable,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedContextAttachmentUnavailable,
		};
	}

	private static string RequiredEvidenceFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "Matched output requires paired runtime Java/C# rows, projected equality values, a result-output prerequisite row, and runtime comparison evidence proving all equality fields match.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "MissingJavaRow output requires an accepted live C# row, no matching runtime-backed Java row for the same identity, a missing-row decision, and runtime comparison evidence.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "MissingCSharpRow output requires a runtime-backed Java row, no matching accepted live C# row for the same identity, a missing-row decision, and runtime comparison evidence.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "FieldMismatch output requires paired runtime rows, projected Java/C# equality values, mismatch selection, diagnostic context attachment, and runtime comparison evidence.",
			_ => "IgnoredRuntimeContext cannot materialize as a standalone output; it may attach only to MissingJavaRow, MissingCSharpRow, or FieldMismatch after runtime comparison evidence exists.",
		};
	}

	private static string NotesFor(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind)
	{
		return outputKind switch
		{
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched => "Java action 2/6 mutation-post parity must not report Matched until live rows are paired and every schema equality value is read.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow => "A C#-only row is not meaningful until the Java runtime artifact capture is complete enough to prove the Java row is absent.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow => "A Java-only row is not meaningful until accepted C# boundary capture is complete enough to prove the C# row is absent.",
			FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch => "Mismatch output must wait for projected value reads so differences are field-specific rather than inferred.",
			_ => "Runtime context remains diagnostic metadata and must not be emitted as an independent result row.",
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedIntakeNotReady => "Value-reader executor materialization preflight is blocked until runtime-evidence intake is ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing => "Value-reader executor materialization preflight is blocked because Java/C# runtime evidence and output prerequisites are missing.",
			_ => "Value-reader executor materialization preflight is defined, but runtime comparison and result emission remain deferred.",
		};
	}
}
