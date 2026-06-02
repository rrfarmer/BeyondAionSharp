namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus
{
	BlockedRuntimeComparisonHandoffNotReady,
	BlockedCaptureEvidenceMissing,
	BlockedExecutableImplementationDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep
{
	JavaArtifactCapture,
	JavaArtifactValidation,
	CSharpGuardedBoundaryCapture,
	BoundaryExecutorObservation,
	RegistrySendObservation,
	RowIdentityAndValueProjection,
	ResultMaterialization,
	ResultEmission,
	RuntimeComparisonExecution,
	ExecutableImplementationGate,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus
{
	BlockedRuntimeComparisonHandoffNotReady,
	BlockedMissingJavaArtifacts,
	BlockedMissingCSharpLiveRows,
	BlockedMissingExecutorObservation,
	BlockedMissingRegistryObservation,
	BlockedMissingValueProjection,
	BlockedMissingMaterializedResults,
	BlockedMissingResultEmission,
	BlockedMissingRuntimeComparison,
	BlockedExecutableImplementationDeferred,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep Step,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement SourceRequirement,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus Status,
	bool HasRuntimeComparisonHandoff,
	bool HasExistingProvider,
	bool HasRuntimeEvidence,
	bool CanRunCommand,
	bool CanAcceptEvidence,
	string Provider,
	string Command,
	string ArtifactRoot,
	string AcceptanceGate,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookRow> Rows,
	string JavaArtifactRoot,
	string JavaCaptureCommand,
	string CSharpBoundaryFixtureName,
	string RuntimeComparisonProvider,
	bool HasRuntimeComparisonHandoff,
	bool HasJavaArtifactCaptureRunbook,
	bool HasGuardedBoundarySkeleton,
	bool HasAnyRuntimeEvidence,
	bool CanRunJavaCapture,
	bool CanRunCSharpCapture,
	bool CanRunRuntimeComparison,
	bool CanStartExecutableImplementation,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live live-capture preflight/runbook for future
/// CM_FIND_GROUP action 2/6 value-reader executor evidence collection. It maps
/// runtime-comparison handoff requirements to concrete capture commands,
/// artifact roots, and acceptance gates without executing capture or comparison.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService
{
	public const string JavaArtifactRootProperty = "aion.findGroupMutationPost.artifactRoot";
	public const string RuntimeComparisonProvider = "FindGroupRuntimeComparisonPreflightContractService, FindGroupMutationPostRuntimeComparisonReadinessReportService";

	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContract? runtimeComparisonHandoff = null,
		FindGroupMutationPostJavaArtifactCaptureRunbook? javaCaptureRunbook = null,
		FindGroupMutationPostGuardedLiveBoundaryFixtureSkeleton? guardedBoundarySkeleton = null)
	{
		runtimeComparisonHandoff ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractService.Create();
		javaCaptureRunbook ??= FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();
		guardedBoundarySkeleton ??= FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.Create();

		var status = StatusFor(runtimeComparisonHandoff);
		var javaCaptureCommand = JavaCaptureCommand(javaCaptureRunbook.ArtifactRoot);
		var rows = new[]
		{
			Row(
				1,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactCapture,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows,
				status,
				runtimeComparisonHandoff,
				provider: $"{javaCaptureRunbook.FixtureClassName}; FindGroupMutationPostTraceCaptureInMemoryArtifactBridge",
				command: javaCaptureCommand,
				artifactRoot: javaCaptureRunbook.ArtifactRoot,
				acceptanceGate: "Action 2 and action 6 Java artifact files must be generated under the guarded artifact root and contain runtime-backed schema-v1 Java trace rows.",
				currentEvidence: $"runbookStatus={javaCaptureRunbook.Status}; expectedArtifacts={javaCaptureRunbook.ExpectedArtifactPaths.Count}; readyForRuntimeComparison={javaCaptureRunbook.ReadyForRuntimeComparison}",
				notes: "Command is a capture preflight target only; generated rows still need C# boundary rows and comparison.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingJavaArtifacts),
			Row(
				2,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactValidation,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows,
				status,
				runtimeComparisonHandoff,
				provider: "FindGroupMutationPostJavaTraceArtifactDirectoryReportService; FindGroupMutationPostJavaTraceArtifactValidatorService",
				command: "dotnet test dotnetConversion\\tests\\Aion.GameServer.Tests\\Aion.GameServer.Tests.csproj --filter \"FullyQualifiedName~FindGroupMutationPostJavaTraceArtifactDirectoryReportServiceTests|FullyQualifiedName~FindGroupMutationPostJavaTraceArtifactValidatorServiceTests\" --no-restore",
				artifactRoot: javaCaptureRunbook.ArtifactRoot,
				acceptanceGate: "C# directory reader must report all expected Java artifacts shape-valid while still marking runtime comparison blocked.",
				currentEvidence: "Repository artifacts may be shape-valid, but shape validity is not Java/C# runtime comparison evidence.",
				notes: "Validation checks schema and action mapping only; it does not prove live C# behavior.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingJavaArtifacts),
			Row(
				3,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.CSharpGuardedBoundaryCapture,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.CSharpBoundaryRows,
				status,
				runtimeComparisonHandoff,
				provider: guardedBoundarySkeleton.FixtureClassName,
				command: "dotnet test dotnetConversion\\tests\\Aion.GameServer.Tests\\Aion.GameServer.Tests.csproj --filter \"FullyQualifiedName~GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture\" --no-restore",
				artifactRoot: "C# in-memory trace export rows; no repository artifact root yet",
				acceptanceGate: "Rows for action 2 and action 6 must have boundaryAccepted=true, executorInvokedFromBoundary=true, registrySendsObservedInOrder=true, and zero broadcasts/invites.",
				currentEvidence: $"skeletonStatus={guardedBoundarySkeleton.Status}; traceGuard={guardedBoundarySkeleton.TraceName}; liveRows={guardedBoundarySkeleton.HasLiveCSharpRows}; productionDispatchEnabled={guardedBoundarySkeleton.IsProductionCmFindGroupDispatchEnabled}",
				notes: "Guarded fixture must not enable production ProcessPacketAsync CmFindGroup dispatch.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingCSharpLiveRows),
			Row(
				4,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.BoundaryExecutorObservation,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.BoundaryExecutorObservation,
				status,
				runtimeComparisonHandoff,
				provider: "FindGroupSideEffectDispatchExecutorService from guarded boundary",
				command: "same guarded C# boundary fixture command",
				artifactRoot: "C# trace-export row fields",
				acceptanceGate: "executorInvokedFromBoundary must be true only when the guarded boundary invoked the executor after packet acceptance.",
				currentEvidence: $"recordsMissingExecutorObservation={guardedBoundarySkeleton.RecordsMissingExecutorObservation}",
				notes: "Executor calls outside the guarded boundary remain insufficient.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingExecutorObservation),
			Row(
				5,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RegistrySendObservation,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RegistrySendObservation,
				status,
				runtimeComparisonHandoff,
				provider: "IGameClientConnectionRegistry direct-send observation",
				command: "same guarded C# boundary fixture command",
				artifactRoot: "C# trace-export row fields",
				acceptanceGate: "Posted SmSystemMessage must be observed before refreshed SmFindGroup for both actions with zero world broadcasts and zero invite dispatches.",
				currentEvidence: $"recordsMissingRegistryObservation={guardedBoundarySkeleton.RecordsMissingRegistryObservation}",
				notes: "Registry send ordering is required before side-effect parity can be compared.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingRegistryObservation),
			Row(
				6,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RowIdentityAndValueProjection,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ValueProjection,
				status,
				runtimeComparisonHandoff,
				provider: "FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractService",
				command: "future focused C# value-reader executor test after Java/C# runtime rows exist",
				artifactRoot: "validated Java artifacts plus accepted C# trace exports",
				acceptanceGate: "Rows must pair by action, mutationKind, activePlayerObjectId, and mutatedEntryObjectId, then project every equality field including ordered visibleEntryObjectIdsAfterMutation.",
				currentEvidence: "No runtime row identities or values have been read.",
				notes: "Value projection remains blocked until both Java and C# runtime row sources exist.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingValueProjection),
			Row(
				7,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ResultMaterialization,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.Materialization,
				status,
				runtimeComparisonHandoff,
				provider: "FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService",
				command: "future focused C# materialization test after value projection exists",
				artifactRoot: "comparison result rows",
				acceptanceGate: "Exactly one Matched, MissingJavaRow, MissingCSharpRow, or FieldMismatch result must materialize per paired row identity.",
				currentEvidence: "Materialization preflight rows are blockers only.",
				notes: "Ignored runtime context can attach only to a parent missing-row or field-mismatch result.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingMaterializedResults),
			Row(
				8,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ResultEmission,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ResultEmission,
				status,
				runtimeComparisonHandoff,
				provider: "FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService",
				command: "future focused C# result-emission test after materialized rows exist",
				artifactRoot: "comparison result rows",
				acceptanceGate: "Emitted result rows must follow the value-reader result schema and must not emit ignored runtime context standalone.",
				currentEvidence: "Result-emission gate rows are non-emittable.",
				notes: "Result emission remains disabled until materialized runtime-backed comparison rows exist.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingResultEmission),
			Row(
				9,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RuntimeComparisonExecution,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RuntimeComparison,
				status,
				runtimeComparisonHandoff,
				provider: RuntimeComparisonProvider,
				command: "future focused Java/C# runtime or socket comparison command after accepted Java and C# rows exist",
				artifactRoot: "validated Java artifacts plus accepted C# trace exports plus comparison result rows",
				acceptanceGate: "Runtime comparison must deterministically compare action 2/6 mutation, packet, side-effect, value projection, materialization, and result output observations.",
				currentEvidence: "Runtime comparison has not executed.",
				notes: "This is the final evidence gate before any verified parity claim.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingRuntimeComparison),
			Row(
				10,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ExecutableImplementationGate,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation,
				status,
				runtimeComparisonHandoff,
				provider: "FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditService",
				command: "no executable implementation command until every preceding acceptance gate is satisfied",
				artifactRoot: "not applicable",
				acceptanceGate: "Executable reader/comparator/result-emission implementation may start only after runtime evidence and comparison gates are satisfied.",
				currentEvidence: "Executable implementation remains deferred.",
				notes: "This runbook cannot authorize executable implementation.",
				stepStatus: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedExecutableImplementationDeferred),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract(
			status,
			rows,
			javaCaptureRunbook.ArtifactRoot,
			javaCaptureCommand,
			guardedBoundarySkeleton.FixtureClassName,
			RuntimeComparisonProvider,
			HasRuntimeComparisonHandoff: runtimeComparisonHandoff.Rows.Count > 0,
			HasJavaArtifactCaptureRunbook: javaCaptureRunbook.Steps.Count > 0,
			HasGuardedBoundarySkeleton: guardedBoundarySkeleton.Steps.Count > 0,
			HasAnyRuntimeEvidence: false,
			CanRunJavaCapture: false,
			CanRunCSharpCapture: false,
			CanRunRuntimeComparison: false,
			CanStartExecutableImplementation: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			runtimeComparisonHandoff.TraceName,
			runtimeComparisonHandoff.JavaSource,
			IsLive: false);
	}

	public static string JavaCaptureCommand(string artifactRoot = FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot) =>
		$"mvn -pl game-server -am test \"-Dtest={FindGroupMutationPostJavaArtifactCaptureRunbookService.FixtureClassName}\" \"-D{FindGroupMutationPostJavaArtifactCaptureRunbookService.CaptureFlag}=true\" \"-D{FindGroupMutationPostJavaArtifactCaptureRunbookService.ServerEpochSecondsProperty}={FindGroupMutationPostJavaArtifactCaptureRunbookService.DeterministicServerEpochSeconds}\" \"-D{JavaArtifactRootProperty}={artifactRoot}\" \"-Dmaven.test.skip=false\" \"-Dsurefire.failIfNoSpecifiedTests=false\"";

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContract runtimeComparisonHandoff)
	{
		return runtimeComparisonHandoff.Status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedImplementationAuditNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedRuntimeComparisonHandoffNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedRuntimeEvidenceMissing => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedCaptureEvidenceMissing,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedExecutableImplementationDeferred,
		};
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookRow Row(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep step,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement sourceRequirement,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus runbookStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContract runtimeComparisonHandoff,
		string provider,
		string command,
		string artifactRoot,
		string acceptanceGate,
		string currentEvidence,
		string notes,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus stepStatus)
	{
		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookRow(
			order,
			step,
			sourceRequirement,
			runbookStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedRuntimeComparisonHandoffNotReady
				? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedRuntimeComparisonHandoffNotReady
				: stepStatus,
			HasRuntimeComparisonHandoff: runtimeComparisonHandoff.Rows.Count > 0,
			HasExistingProvider: true,
			HasRuntimeEvidence: false,
			CanRunCommand: false,
			CanAcceptEvidence: false,
			provider,
			command,
			artifactRoot,
			acceptanceGate,
			$"{currentEvidence}; handoffStatus={runtimeComparisonHandoff.Status}; canStartRuntimeComparison={runtimeComparisonHandoff.CanStartRuntimeComparison}; canStartExecutableImplementation={runtimeComparisonHandoff.CanStartExecutableImplementation}; canClaimVerifiedParity={runtimeComparisonHandoff.CanClaimVerifiedParity}",
			notes);
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedRuntimeComparisonHandoffNotReady => "Value-reader executor live-capture preflight runbook is blocked until runtime comparison handoff metadata is ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedCaptureEvidenceMissing => "Value-reader executor live-capture preflight runbook is blocked because Java artifact capture, C# guarded boundary capture, value projection, result emission, and runtime comparison evidence are missing.",
			_ => "Value-reader executor live-capture preflight runbook is defined, but executable implementation remains deferred until every capture and comparison acceptance gate is satisfied.",
		};
	}
}
