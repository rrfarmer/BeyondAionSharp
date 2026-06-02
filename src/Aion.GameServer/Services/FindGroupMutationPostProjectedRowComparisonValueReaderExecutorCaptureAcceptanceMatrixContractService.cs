namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus
{
	BlockedLiveCapturePreflightNotReady,
	BlockedAcceptanceEvidenceMissing,
	BlockedRuntimeComparisonExecution,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField
{
	ExecutorConsistencyAudit,
	JavaArtifactRows,
	JavaArtifactShapeValidation,
	CSharpBoundaryRows,
	BoundaryExecutorObservation,
	RegistrySendObservation,
	RowIdentityMatching,
	ValueProjection,
	ResultMaterialization,
	ResultEmission,
	RuntimeComparisonExecution,
	ExecutableImplementationGate,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus
{
	BlockedLiveCapturePreflightNotReady,
	MissingExecutorConsistencyAuditEvidence,
	MissingJavaArtifactEvidence,
	MissingCSharpBoundaryEvidence,
	MissingExecutorObservation,
	MissingRegistryObservation,
	MissingValueProjection,
	MissingMaterializedResults,
	MissingResultEmission,
	MissingRuntimeComparison,
	ExecutableImplementationDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField Field,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep SourceStep,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement SourceRequirement,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus Status,
	bool HasLiveCapturePreflight,
	bool HasProvider,
	bool HasRuntimeEvidence,
	bool AcceptancePassed,
	bool BlocksRuntimeComparison,
	bool BlocksExecutableImplementation,
	bool BlocksVerifiedParity,
	string EvidenceField,
	string RequiredEvidence,
	string CurrentEvidence,
	string RuntimeComparisonBlocker,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixRow> Rows,
	int RequiredEvidenceFieldCount,
	int MissingEvidenceFieldCount,
	int RuntimeComparisonBlockerCount,
	bool HasLiveCapturePreflight,
	bool HasAnyRuntimeEvidence,
	bool AllAcceptanceGatesPassed,
	bool CanRunRuntimeComparison,
	bool CanStartExecutableImplementation,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live acceptance matrix for future CM_FIND_GROUP
/// action 2/6 value-reader executor capture evidence. It turns the capture
/// preflight gates into pass/fail evidence fields and names the blockers that
/// still prevent runtime comparison execution.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract? liveCapturePreflight = null)
	{
		liveCapturePreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.Create();

		var status = StatusFor(liveCapturePreflight);
		var rows = new[]
		{
			MatrixRow(
				1,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutorConsistencyAudit,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ExecutorConsistencyAudit,
				status,
				"executorConsistencyAuditAccepted",
				"Runtime-comparison handoff carries an explicit internally consistent projected-value executor consistency audit.",
				"Executor consistency audit has not been accepted as a visible capture preflight evidence field.",
				"Runtime comparison cannot execute while consistency blockers are hidden behind upstream handoff status."),
			MatrixRow(
				2,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactCapture,
				status,
				"javaArtifactRowsPresent",
				"Both action 2 and action 6 Java mutation-post artifact files generated from capture-enabled runtime hook rows.",
				"Java runtime artifact rows are absent; checked-in shape artifacts are not enough.",
				"Runtime comparison cannot execute without Java source-of-truth rows."),
			MatrixRow(
				3,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactShapeValidation,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactValidation,
				status,
				"javaArtifactsShapeValid",
				"C# artifact directory reader validates both generated Java artifacts for schema, action mapping, traceSource=Java, and zero broadcast/invite counts.",
				"Generated Java artifacts have not been accepted as runtime-backed shape-valid rows.",
				"Runtime comparison cannot trust Java row inputs until validation passes."),
			MatrixRow(
				4,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.CSharpBoundaryRows,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.CSharpGuardedBoundaryCapture,
				status,
				"csharpBoundaryRowsAccepted",
				"Action 2 and action 6 C# rows captured from guarded CM_FIND_GROUP boundary with boundaryAccepted=true.",
				"Accepted live C# boundary rows are missing.",
				"Runtime comparison cannot execute with disabled C# projections only."),
			MatrixRow(
				5,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.BoundaryExecutorObservation,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.BoundaryExecutorObservation,
				status,
				"boundaryExecutorObserved",
				"executorInvokedFromBoundary=true only after the guarded boundary invokes the side-effect executor.",
				"Boundary executor observation is missing.",
				"Runtime comparison cannot prove the C# row came from the same boundary path without executor observation."),
			MatrixRow(
				6,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RegistrySendObservation,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RegistrySendObservation,
				status,
				"registrySendsObservedInOrder",
				"Posted SmSystemMessage observed before refreshed SmFindGroup for both actions, with zero broadcasts and zero invite dispatches.",
				"Registry send ordering is missing.",
				"Runtime comparison cannot validate direct packet side effects until registry sends are observed."),
			MatrixRow(
				7,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RowIdentityMatching,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RowIdentityAndValueProjection,
				status,
				"rowIdentityMatched",
				"Runtime Java/C# rows paired by action, mutationKind, activePlayerObjectId, and mutatedEntryObjectId.",
				"Row identity matching is missing.",
				"Runtime comparison cannot select Matched or missing-row results until row identities are paired."),
			MatrixRow(
				8,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ValueProjection,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RowIdentityAndValueProjection,
				status,
				"projectedEqualityValuesRead",
				"Every equality field, including ordered visibleEntryObjectIdsAfterMutation, read from runtime Java JSON and accepted C# trace-export rows.",
				"Projected Java/C# values are missing.",
				"Runtime comparison cannot evaluate equality or field mismatch results until values are projected."),
			MatrixRow(
				9,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ResultMaterialization,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ResultMaterialization,
				status,
				"comparisonResultsMaterialized",
				"Exactly one Matched, MissingJavaRow, MissingCSharpRow, or FieldMismatch result materialized per row identity.",
				"Materialized comparison result rows are missing.",
				"Runtime comparison cannot emit or verify output rows until materialization exists."),
			MatrixRow(
				10,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ResultEmission,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ResultEmission,
				status,
				"comparisonResultsEmitted",
				"Result rows emitted through the value-reader result schema, with ignored runtime context attached only to a parent result.",
				"Result emission is missing.",
				"Runtime comparison cannot claim output evidence until result rows are emitted."),
			MatrixRow(
				11,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RuntimeComparisonExecution,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RuntimeComparisonExecution,
				status,
				"runtimeComparisonExecuted",
				"Deterministic Java/C# runtime or socket comparison executed for action 2/6 mutation, packet, side-effect, value, materialization, and result-output observations.",
				"Runtime comparison has not executed.",
				"Verified parity is impossible without deterministic runtime comparison evidence."),
			MatrixRow(
				12,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutableImplementationGate,
				liveCapturePreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ExecutableImplementationGate,
				status,
				"executableImplementationAllowed",
				"Executable value-reader executor implementation allowed only after every capture and runtime comparison evidence field passes.",
				"Executable implementation remains deferred.",
				"Executable implementation must stay blocked until every preceding evidence field passes.",
				blocksRuntimeComparison: false),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContract(
			status,
			rows,
			RequiredEvidenceFieldCount: rows.Count(row => row.Field != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutableImplementationGate),
			MissingEvidenceFieldCount: rows.Count(row => !row.AcceptancePassed),
			RuntimeComparisonBlockerCount: rows.Count(row => row.BlocksRuntimeComparison),
			HasLiveCapturePreflight: liveCapturePreflight.Rows.Count > 0,
			HasAnyRuntimeEvidence: false,
			AllAcceptanceGatesPassed: false,
			CanRunRuntimeComparison: false,
			CanStartExecutableImplementation: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			liveCapturePreflight.TraceName,
			liveCapturePreflight.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract liveCapturePreflight)
	{
		return liveCapturePreflight.Status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedRuntimeComparisonHandoffNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedLiveCapturePreflightNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedCaptureEvidenceMissing => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedAcceptanceEvidenceMissing,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedRuntimeComparisonExecution,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixRow MatrixRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField field,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract liveCapturePreflight,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep sourceStep,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus matrixStatus,
		string evidenceField,
		string requiredEvidence,
		string currentEvidence,
		string runtimeComparisonBlocker,
		bool blocksRuntimeComparison = true)
	{
		var sourceRow = liveCapturePreflight.Rows.FirstOrDefault(row => row.Step == sourceStep);
		var sourceRequirement = sourceRow?.SourceRequirement
			?? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation;
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixRow(
			order,
			field,
			sourceStep,
			sourceRequirement,
			FieldStatusFor(matrixStatus, field),
			HasLiveCapturePreflight: liveCapturePreflight.Rows.Count > 0,
			HasProvider: sourceRow?.HasExistingProvider ?? false,
			HasRuntimeEvidence: false,
			AcceptancePassed: false,
			blocksRuntimeComparison && matrixStatus != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedLiveCapturePreflightNotReady,
			BlocksExecutableImplementation: true,
			BlocksVerifiedParity: true,
			evidenceField,
			requiredEvidence,
			$"{currentEvidence}; sourceStatus={sourceRow?.Status.ToString() ?? "Missing"}; sourceCanAcceptEvidence={sourceRow?.CanAcceptEvidence.ToString() ?? "False"}; sourceCanRunCommand={sourceRow?.CanRunCommand.ToString() ?? "False"}; matrixStatus={matrixStatus}",
			runtimeComparisonBlocker,
			sourceRow?.AcceptanceGate ?? "Source preflight row is missing.");
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus FieldStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus matrixStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField field)
	{
		if (matrixStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedLiveCapturePreflightNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.BlockedLiveCapturePreflightNotReady;

		return field switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutorConsistencyAudit => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingExecutorConsistencyAuditEvidence,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingJavaArtifactEvidence,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactShapeValidation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingJavaArtifactEvidence,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.CSharpBoundaryRows => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingCSharpBoundaryEvidence,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.BoundaryExecutorObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingExecutorObservation,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RegistrySendObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingRegistryObservation,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RowIdentityMatching => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingValueProjection,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ValueProjection => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingValueProjection,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ResultMaterialization => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingMaterializedResults,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ResultEmission => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingResultEmission,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RuntimeComparisonExecution => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingRuntimeComparison,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.ExecutableImplementationDeferred,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedLiveCapturePreflightNotReady => "Value-reader executor capture acceptance matrix is blocked until live-capture preflight metadata is ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedAcceptanceEvidenceMissing => "Value-reader executor capture acceptance matrix is blocked because required Java artifact, C# boundary, value projection, materialization, emission, and runtime comparison evidence fields are missing.",
			_ => "Value-reader executor capture acceptance matrix is defined, but runtime comparison execution and executable implementation remain blocked until every evidence field passes.",
		};
	}
}
