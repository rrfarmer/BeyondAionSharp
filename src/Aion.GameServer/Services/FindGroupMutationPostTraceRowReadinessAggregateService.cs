namespace Aion.GameServer.Services;

public enum FindGroupMutationPostTraceRowReadinessStatus
{
	BlockedMissingJavaCapture,
	BlockedMissingCSharpLiveRows,
	BlockedMissingRegistryObservation,
	BlockedMissingArtifactComparison,
	Ready,
}

public enum FindGroupMutationPostTraceRowReadinessBlocker
{
	JavaCaptureRunbook,
	CSharpLiveTraceRowFixturePlan,
	RegistryObservationContract,
	ArtifactComparisonPreflight,
}

public enum FindGroupMutationPostTraceRowReadinessRowStatus
{
	SatisfiedByNonLiveMetadata,
	BlockedMissingJavaFixture,
	BlockedMissingJavaInstrumentation,
	BlockedMissingJavaArtifacts,
	BlockedMissingCSharpLiveFixture,
	BlockedMissingLiveEmitter,
	BlockedMissingRegistryObservation,
	BlockedComparisonNotReady,
}

public sealed record FindGroupMutationPostTraceRowReadinessRow(
	int Order,
	FindGroupMutationPostTraceRowReadinessBlocker Blocker,
	FindGroupMutationPostTraceRowReadinessRowStatus Status,
	bool BlocksRuntimeComparison,
	string Evidence,
	string JavaSource,
	string CSharpTarget,
	string Notes);

public sealed record FindGroupMutationPostTraceRowReadinessAggregate(
	FindGroupMutationPostTraceRowReadinessStatus Status,
	IReadOnlyList<FindGroupMutationPostTraceRowReadinessRow> Rows,
	bool HasJavaCaptureRunbook,
	bool HasCSharpLiveTraceRowFixturePlan,
	bool HasRegistryObservationContract,
	bool HasArtifactComparisonPreflight,
	bool NeedsJavaFixture,
	bool NeedsJavaInstrumentation,
	bool NeedsGeneratedJavaArtifacts,
	bool NeedsCSharpLiveRows,
	bool NeedsRegistryObservation,
	bool NeedsComparisonExecution,
	bool ReadyForRuntimeComparison,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: conservative aggregate for CM_FIND_GROUP action 2/6
/// mutation-post trace row readiness. This report only summarizes prerequisite
/// metadata and blockers; it does not capture or compare runtime rows.
/// </summary>
public static class FindGroupMutationPostTraceRowReadinessAggregateService
{
	public static FindGroupMutationPostTraceRowReadinessAggregate Create(
		FindGroupMutationPostJavaArtifactCaptureRunbook? javaRunbook = null,
		FindGroupMutationPostCSharpLiveTraceRowFixturePlan? csharpFixturePlan = null,
		FindGroupMutationPostRegistryObservationTraceContract? registryContract = null,
		FindGroupMutationPostArtifactComparisonPreflightReport? comparisonPreflight = null)
	{
		javaRunbook ??= FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();
		csharpFixturePlan ??= FindGroupMutationPostCSharpLiveTraceRowFixturePlanService.Create();
		registryContract ??= FindGroupMutationPostRegistryObservationTraceContractService.Create();
		comparisonPreflight ??= FindGroupMutationPostArtifactComparisonPreflightService.Create();

		var rows = new List<FindGroupMutationPostTraceRowReadinessRow>();
		AddJavaRunbook(rows, javaRunbook);
		AddCSharpFixturePlan(rows, csharpFixturePlan);
		AddRegistryContract(rows, registryContract);
		AddComparisonPreflight(rows, comparisonPreflight);

		var rowArray = rows.ToArray();
		var status = DetermineStatus(rowArray);

		return new FindGroupMutationPostTraceRowReadinessAggregate(
			status,
			rowArray,
			HasJavaCaptureRunbook: javaRunbook.Steps.Count > 0,
			HasCSharpLiveTraceRowFixturePlan: csharpFixturePlan.Steps.Count > 0,
			HasRegistryObservationContract: registryContract.Requirements.Count > 0,
			HasArtifactComparisonPreflight: comparisonPreflight.Rows.Count > 0,
			NeedsJavaFixture: javaRunbook.RequiresJavaFixture,
			NeedsJavaInstrumentation: javaRunbook.RequiresJavaInstrumentation,
			NeedsGeneratedJavaArtifacts: javaRunbook.RequiresGeneratedArtifacts || comparisonPreflight.NeedsGeneratedJavaArtifacts,
			NeedsCSharpLiveRows: csharpFixturePlan.RequiresLiveBoundaryFixture || comparisonPreflight.NeedsLiveCSharpTraceRows,
			NeedsRegistryObservation: registryContract.RequiresRegistrySendsObservedInOrder || comparisonPreflight.NeedsRegistryObservation,
			NeedsComparisonExecution: comparisonPreflight.NeedsComparisonExecution,
			ReadyForRuntimeComparison: status == FindGroupMutationPostTraceRowReadinessStatus.Ready,
			javaRunbook.TraceName,
			javaRunbook.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostTraceRowReadinessStatus DetermineStatus(
		IReadOnlyList<FindGroupMutationPostTraceRowReadinessRow> rows)
	{
		if (rows.Any(row => row.Status is FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingJavaFixture
			or FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingJavaInstrumentation
			or FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingJavaArtifacts))
		{
			return FindGroupMutationPostTraceRowReadinessStatus.BlockedMissingJavaCapture;
		}

		if (rows.Any(row => row.Status is FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingCSharpLiveFixture
			or FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingLiveEmitter))
		{
			return FindGroupMutationPostTraceRowReadinessStatus.BlockedMissingCSharpLiveRows;
		}

		if (rows.Any(row => row.Status == FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingRegistryObservation))
			return FindGroupMutationPostTraceRowReadinessStatus.BlockedMissingRegistryObservation;

		if (rows.Any(row => row.Status == FindGroupMutationPostTraceRowReadinessRowStatus.BlockedComparisonNotReady))
			return FindGroupMutationPostTraceRowReadinessStatus.BlockedMissingArtifactComparison;

		return FindGroupMutationPostTraceRowReadinessStatus.Ready;
	}

	private static void AddJavaRunbook(
		ICollection<FindGroupMutationPostTraceRowReadinessRow> rows,
		FindGroupMutationPostJavaArtifactCaptureRunbook runbook)
	{
		var status = runbook.Status switch
		{
			FindGroupMutationPostJavaArtifactCaptureRunbookStatus.BlockedMissingJavaFixture => FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingJavaFixture,
			FindGroupMutationPostJavaArtifactCaptureRunbookStatus.BlockedMissingJavaInstrumentation => FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingJavaInstrumentation,
			_ => FindGroupMutationPostTraceRowReadinessRowStatus.SatisfiedByNonLiveMetadata,
		};

		Add(rows,
			FindGroupMutationPostTraceRowReadinessBlocker.JavaCaptureRunbook,
			status,
			blocks: runbook.RequiresJavaFixture || runbook.RequiresJavaInstrumentation || runbook.RequiresTraceSerializer || runbook.RequiresGeneratedArtifacts,
			$"fixture={runbook.FixtureClassName}; captureFlag={runbook.CaptureFlag}; steps={runbook.Steps.Count}; artifacts={runbook.ExpectedArtifactPaths.Count}; focusedMavenCommand={runbook.FocusedMavenCommand}",
			runbook.JavaSource,
			"FindGroupMutationPostJavaArtifactCaptureRunbook",
			"Java fixture, instrumentation, serializer, and generated artifacts are required before Java trace rows can feed comparison.");
	}

	private static void AddCSharpFixturePlan(
		ICollection<FindGroupMutationPostTraceRowReadinessRow> rows,
		FindGroupMutationPostCSharpLiveTraceRowFixturePlan plan)
	{
		var status = plan.Status == FindGroupMutationPostCSharpLiveTraceRowFixturePlanStatus.BlockedPendingLiveBoundaryFixture
			? FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingCSharpLiveFixture
			: FindGroupMutationPostTraceRowReadinessRowStatus.SatisfiedByNonLiveMetadata;

		Add(rows,
			FindGroupMutationPostTraceRowReadinessBlocker.CSharpLiveTraceRowFixturePlan,
			status,
			blocks: plan.RequiresLiveBoundaryFixture || plan.RequiresLiveEmitter || plan.RequiresGeneratedJavaArtifacts,
			$"fixture={plan.FixtureClassName}; actions={string.Join("/", plan.Actions)}; boundaryWired={plan.IsCmFindGroupBoundaryWired}; invokeLiveSideEffects={plan.ShouldInvokeLiveSideEffects}; steps={plan.Steps.Count}",
			plan.JavaSource,
			"FindGroupMutationPostCSharpLiveTraceRowFixturePlan",
			"C# live trace rows must come from a guarded real connection boundary fixture before comparison can proceed.");
	}

	private static void AddRegistryContract(
		ICollection<FindGroupMutationPostTraceRowReadinessRow> rows,
		FindGroupMutationPostRegistryObservationTraceContract contract)
	{
		var status = contract.Status == FindGroupMutationPostRegistryObservationTraceContractStatus.BlockedPendingLiveBoundaryTrace
			? FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingRegistryObservation
			: FindGroupMutationPostTraceRowReadinessRowStatus.SatisfiedByNonLiveMetadata;

		Add(rows,
			FindGroupMutationPostTraceRowReadinessBlocker.RegistryObservationContract,
			status,
			blocks: contract.RequiresExecutorInvokedFromBoundary
				|| contract.RequiresRegistrySendsObservedInOrder
				|| contract.RequiresTwoDirectSendsPerAction
				|| contract.RequiresZeroWorldBroadcasts
				|| contract.RequiresZeroInviteDispatches,
			$"requirements={contract.Requirements.Count}; orderedSends={contract.RequiresRegistrySendsObservedInOrder}; twoDirectSends={contract.RequiresTwoDirectSendsPerAction}; zeroBroadcasts={contract.RequiresZeroWorldBroadcasts}; zeroInvites={contract.RequiresZeroInviteDispatches}",
			contract.JavaSource,
			"FindGroupMutationPostRegistryObservationTraceContract",
			"Live registry observation must prove posted system message before refreshed list and no broadcast/invite side effects.");
	}

	private static void AddComparisonPreflight(
		ICollection<FindGroupMutationPostTraceRowReadinessRow> rows,
		FindGroupMutationPostArtifactComparisonPreflightReport preflight)
	{
		var status = preflight.Status switch
		{
			FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingJavaArtifacts => FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingJavaArtifacts,
			FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingLiveCSharpRows => FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingCSharpLiveFixture,
			FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingRegistryObservation => FindGroupMutationPostTraceRowReadinessRowStatus.BlockedMissingRegistryObservation,
			FindGroupMutationPostArtifactComparisonPreflightStatus.Ready => FindGroupMutationPostTraceRowReadinessRowStatus.SatisfiedByNonLiveMetadata,
			_ => FindGroupMutationPostTraceRowReadinessRowStatus.BlockedComparisonNotReady,
		};

		Add(rows,
			FindGroupMutationPostTraceRowReadinessBlocker.ArtifactComparisonPreflight,
			status,
			blocks: !preflight.ReadyForRuntimeComparison,
			$"status={preflight.Status}; javaArtifacts={preflight.HasShapeValidJavaArtifacts}; liveCSharpRows={preflight.HasLiveCSharpTraceRows}; registryObservation={preflight.HasRegistryObservation}; comparisonExecuted={preflight.HasComparisonExecution}; matching={preflight.HasMatchingComparisonResult}",
			preflight.JavaSource,
			"FindGroupMutationPostArtifactComparisonPreflightReport",
			"Artifact comparison preflight must reach ready before any verified parity claim can be considered.");
	}

	private static void Add(
		ICollection<FindGroupMutationPostTraceRowReadinessRow> rows,
		FindGroupMutationPostTraceRowReadinessBlocker blocker,
		FindGroupMutationPostTraceRowReadinessRowStatus status,
		bool blocks,
		string evidence,
		string javaSource,
		string csharpTarget,
		string notes)
	{
		rows.Add(new FindGroupMutationPostTraceRowReadinessRow(
			rows.Count + 1,
			blocker,
			status,
			blocks,
			evidence,
			javaSource,
			csharpTarget,
			notes));
	}
}
