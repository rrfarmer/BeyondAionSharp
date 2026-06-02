namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus
{
	BlockedSummaryNotReady,
	BlockedRuntimeEvidenceMissing,
}

public enum FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus
{
	ExistingNonLiveMetadata,
	ExistingNonLiveScaffold,
	FutureRuntimeEvidenceRequired,
	LiveDispatchDisabled,
	ComparisonNotExecuted,
}

public sealed record FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonLiveInputRequirement Requirement,
	FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus ProviderStatus,
	bool HasExistingProvider,
	bool HasRuntimeEvidence,
	bool BlocksVerifiedParity,
	string ExistingProvider,
	string RequiredNextEvidence,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist(
	FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistRow> Rows,
	bool HasLiveInputHandoff,
	bool HasExistingNonLiveProviders,
	bool HasAnyRuntimeEvidence,
	bool CanStartProjectedComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live evidence checklist for CM_FIND_GROUP action
/// 2/6 projected-row comparison. It maps live-input requirements to existing
/// scaffolds or future evidence producers, but it never captures runtime rows.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService
{
	public static FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist Create(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract? liveInputHandoff = null)
	{
		liveInputHandoff ??= FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create();
		var rows = liveInputHandoff.Requirements
			.Select(CreateRow)
			.ToArray();
		var status = liveInputHandoff.Status == FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedSummaryNotReady
			? FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedSummaryNotReady
			: FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedRuntimeEvidenceMissing;

		return new FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist(
			status,
			rows,
			HasLiveInputHandoff: liveInputHandoff.Requirements.Count > 0,
			HasExistingNonLiveProviders: rows.Any(row => row.HasExistingProvider),
			HasAnyRuntimeEvidence: false,
			CanStartProjectedComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			liveInputHandoff.TraceName,
			liveInputHandoff.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistRow CreateRow(
		FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow requirement)
	{
		var mapping = MappingFor(requirement.Requirement);
		return new FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistRow(
			requirement.Order,
			requirement.Requirement,
			StatusFor(requirement, mapping),
			mapping.HasExistingProvider,
			HasRuntimeEvidence: false,
			BlocksVerifiedParity: true,
			mapping.ExistingProvider,
			mapping.RequiredNextEvidence,
			$"{requirement.Evidence}; handoffStatus={requirement.Status}; handoffRuntimeEvidence={requirement.IsRuntimeEvidence}",
			mapping.Notes);
	}

	private static FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow requirement,
		RuntimeEvidenceMapping mapping)
	{
		if (requirement.Status == FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedSummaryNotReady)
			return mapping.HasExistingProvider
				? FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveScaffold
				: FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.FutureRuntimeEvidenceRequired;

		return requirement.Requirement switch
		{
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ProjectedRowReadinessSummary => FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveMetadata,
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueReaderReadinessSummary => FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveMetadata,
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.LiveDispatchGuard => FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.LiveDispatchDisabled,
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RuntimeSocketComparison => FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ComparisonNotExecuted,
			_ => mapping.HasExistingProvider
				? FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveScaffold
				: FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.FutureRuntimeEvidenceRequired,
		};
	}

	private static RuntimeEvidenceMapping MappingFor(
		FindGroupMutationPostProjectedRowComparisonLiveInputRequirement requirement)
	{
		return requirement switch
		{
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ProjectedRowReadinessSummary => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupMutationPostProjectedRowComparisonReadinessSummaryService",
				"Keep readiness summary current while runtime evidence is collected.",
				"Summary metadata exists, but it is not runtime evidence."),
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueReaderReadinessSummary => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService, FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService, FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService, FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistService, FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService",
				"Keep value-reader readiness summary, typed-reader preflight, mismatch-context preflight, implementation readiness checklist, and implementation runbook current while runtime value-reader evidence is collected.",
				"Value-reader summary, typed-reader preflight, mismatch-context, implementation checklist, and implementation runbook metadata exist, but this is not runtime evidence and reads no values."),
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupMutationPostTraceCaptureTest, FindGroupMutationPostTraceCaptureHooks, FindGroupMutationPostTraceCaptureInMemoryArtifactBridge, FindGroupMutationPostJavaTraceArtifactDirectoryReportService",
				"Run a capture-enabled Java fixture or runtime capture that writes action 2/6 artifacts from real hook rows, then validate them through the C# artifact reader.",
				"Existing Java artifact files and hook scaffolds are shape evidence only until generated from runtime capture."),
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.CSharpLiveBoundaryRow => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupMutationPostCSharpTraceRowFixtureReportService, FindGroupMutationPostGuardedFixtureResultContractService, GameServerConnection.CreateDisabledFindGroupBoundaryPlan",
				"Capture action 2/6 rows from the guarded live C# CM_FIND_GROUP boundary with boundary acceptance, executor invocation, and registry observation true.",
				"Disabled boundary projections exercise shape but are rejected as non-live."),
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.BoundaryExecutorInvocation => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService, FindGroupMutationPostComparisonInputEnvelopeService",
				"Prove the comparison input envelope is created by the CM_FIND_GROUP boundary executor after packet acceptance.",
				"Executor skeletons and envelopes are metadata until boundary-driven execution is observed."),
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RegistrySendObservation => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupMutationPostRegistryObservationTraceContractService, FindGroupSideEffectDispatchExecutorService",
				"Observe posted system-message and refreshed-list registry sends in Java order for actions 2 and 6, with zero broadcasts and zero invite dispatches.",
				"Registry observation contract names required sends but does not observe live sends."),
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueProjection => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupMutationPostProjectedRowComparisonValueContractService, FindGroupMutationPostComparisonKeyProjectionMetadataService",
				"Read projected Java and C# values for every equality field after accepted live Java/C# rows are paired.",
				"Value contracts name fields but deliberately do not read values."),
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RowIdentityMatching => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupMutationPostProjectedRowComparisonDryRunContractService, FindGroupMutationPostProjectedRowComparisonExecutorSkeletonService",
				"Match action, mutationKind, activePlayerObjectId, and mutatedEntryObjectId across runtime Java and C# rows.",
				"Paired readiness remains metadata until runtime row keys are inspected."),
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ResultEmission => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupMutationPostProjectedRowComparisonResultSkeletonService, FindGroupMutationPostProjectedRowComparisonBlockedResultReportService, FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService, FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateService, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService",
				"Emit real Matched, MissingJavaRow, MissingCSharpRow, FieldMismatch, and IgnoredRuntimeContext rows from projected comparison using the value-reader result schema, comparator preflight, executor readiness gate, executor implementation plan, blocked-output preview, runtime-evidence intake, and materialization preflight.",
				"Result skeletons, value-reader result schema, comparator preflight, executor readiness gate, executor implementation plan, blocked-output preview, runtime-evidence intake, and materialization preflight describe output and blockers but cannot materialize them."),
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.LiveDispatchGuard => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"GameServerConnection.ProcessPacketAsync CmFindGroup guard, FindGroupLiveDispatchGoNoGoChecklistService",
				"Keep live dispatch disabled until runtime evidence and comparison output are ready, then document the broad-validation trigger before enabling it.",
				"Live dispatch remains disabled by design."),
			_ => new RuntimeEvidenceMapping(
				HasExistingProvider: true,
				"FindGroupRuntimeComparisonPreflightContractService, FindGroupMutationPostRuntimeComparisonReadinessReportService",
				"Run deterministic Java/C# runtime or socket comparison for action 2/6 mutation, packet, and side-effect observations.",
				"Runtime comparison remains unexecuted."),
		};
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedSummaryNotReady => "Runtime evidence checklist is blocked until the live-input handoff reaches runtime-artifact readiness.",
			_ => "Runtime evidence checklist is blocked because mapped providers are non-live scaffolds or future evidence requirements, not verified runtime parity.",
		};
	}

	private sealed record RuntimeEvidenceMapping(
		bool HasExistingProvider,
		string ExistingProvider,
		string RequiredNextEvidence,
		string Notes);
}
