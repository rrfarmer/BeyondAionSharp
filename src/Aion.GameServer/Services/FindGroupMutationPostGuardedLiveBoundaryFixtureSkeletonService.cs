namespace Aion.GameServer.Services;

public enum FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStatus
{
	BlockedPendingGuardedBoundaryFixture,
	ReadyForGuardedFixtureImplementation,
}

public enum FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind
{
	ExplicitTraceGuard,
	ProductionDispatchGuard,
	ActionTwoBoundaryScenario,
	ActionSixBoundaryScenario,
	ExecutorObservation,
	RegistryObservation,
	ArtifactPreflightHandoff,
}

public enum FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus
{
	DesignOnly,
	BlockedMissingFixture,
	BlockedMissingLiveBoundary,
	BlockedMissingExecutorObservation,
	BlockedMissingRegistryObservation,
	BlockedMissingComparisonPreflight,
}

public sealed record FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStep(
	int Order,
	FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind Kind,
	FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus Status,
	bool BlocksRuntimeComparison,
	string Target,
	string RequiredEvidence,
	string Notes);

public sealed record FindGroupMutationPostGuardedLiveBoundaryFixtureSkeleton(
	FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStatus Status,
	IReadOnlyList<int> Actions,
	IReadOnlyList<FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStep> Steps,
	string FixtureClassName,
	string TraceName,
	bool RequiresExplicitTraceGuard,
	bool IsProductionCmFindGroupDispatchEnabled,
	bool ShouldSendPackets,
	bool RecordsMissingExecutorObservation,
	bool RecordsMissingRegistryObservation,
	bool HasShapeValidJavaArtifacts,
	bool HasCSharpShapeInputs,
	bool HasLiveCSharpRows,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: guarded fixture skeleton for future CM_FIND_GROUP action 2/6
/// mutation-post live-boundary rows. This report describes the fixture gate only; it
/// does not wire ProcessPacketAsync, execute sends, or mark disabled projections live.
/// </summary>
public static class FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService
{
	public const string FixtureClassName = "GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture";
	public const string TraceGuardName = "AION_FIND_GROUP_MUTATION_POST_TRACE_GUARD";

	public static FindGroupMutationPostGuardedLiveBoundaryFixtureSkeleton Create(
		FindGroupMutationPostCSharpLiveTraceRowFixturePlan? fixturePlan = null,
		FindGroupMutationPostArtifactComparisonPreflightReport? preflight = null)
	{
		fixturePlan ??= FindGroupMutationPostCSharpLiveTraceRowFixturePlanService.Create();
		preflight ??= FindGroupMutationPostArtifactComparisonPreflightService.Create();

		var steps = new List<FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStep>();
		Add(steps,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ExplicitTraceGuard,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.DesignOnly,
			blocks: false,
			TraceGuardName,
			"Fixture must require an explicit trace-only guard before any boundary helper can be exercised.",
			"Default production dispatch remains disabled when the guard is absent.");

		Add(steps,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ProductionDispatchGuard,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.DesignOnly,
			blocks: false,
			"GameServerConnection.ProcessPacketAsync case CmFindGroup",
			"Keep the production case deferred; the skeleton must not send packets or invoke live side effects.",
			$"fixturePlanBoundaryWired={fixturePlan.IsCmFindGroupBoundaryWired}; shouldInvokeLiveSideEffects={fixturePlan.ShouldInvokeLiveSideEffects}");

		Add(steps,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ActionTwoBoundaryScenario,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingFixture,
			blocks: true,
			"CM_FIND_GROUP action 2 guarded boundary scenario",
			"Capture action 2 boundary acceptance, recruitment mutation shape, posted system message 1400392, and refreshed SmFindGroup action 0 under the guard.",
			"Scenario must use Java FindGroupService.addRecruitment ordering as the source of truth.");

		Add(steps,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ActionSixBoundaryScenario,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingFixture,
			blocks: true,
			"CM_FIND_GROUP action 6 guarded boundary scenario",
			"Capture action 6 boundary acceptance, application mutation shape, posted system message 1400393, and refreshed SmFindGroup action 4 under the guard.",
			"Scenario must use Java FindGroupService.addApplication ordering as the source of truth.");

		Add(steps,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ExecutorObservation,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingExecutorObservation,
			blocks: true,
			"FindGroupSideEffectDispatchExecutorService from guarded boundary",
			"Record executorInvokedFromBoundary=true only after the guarded boundary invokes the executor.",
			"Current skeleton records this as missing; opt-in executor calls outside the boundary remain insufficient.");

		Add(steps,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.RegistryObservation,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingRegistryObservation,
			blocks: true,
			"IGameClientConnectionRegistry direct-send observation",
			"Record posted system message before refreshed list and zero broadcast/invite counts for both actions.",
			"Current skeleton records registry observation as missing.");

		Add(steps,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind.ArtifactPreflightHandoff,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingComparisonPreflight,
			blocks: true,
			"FindGroupMutationPostArtifactComparisonPreflightService",
			"Feed live C# rows only after the guarded fixture has executor and registry observations.",
			$"preflightStatus={preflight.Status}; javaArtifacts={preflight.HasShapeValidJavaArtifacts}; csharpShapeInputs={preflight.HasCSharpTraceRowShapeInputs}; liveRows={preflight.HasLiveCSharpTraceRows}");

		var stepArray = steps.ToArray();
		return new FindGroupMutationPostGuardedLiveBoundaryFixtureSkeleton(
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStatus.BlockedPendingGuardedBoundaryFixture,
			[2, 6],
			stepArray,
			FixtureClassName,
			fixturePlan.TraceName,
			RequiresExplicitTraceGuard: true,
			IsProductionCmFindGroupDispatchEnabled: false,
			ShouldSendPackets: false,
			RecordsMissingExecutorObservation: stepArray.Any(step => step.Status == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingExecutorObservation),
			RecordsMissingRegistryObservation: stepArray.Any(step => step.Status == FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus.BlockedMissingRegistryObservation),
			HasShapeValidJavaArtifacts: preflight.HasShapeValidJavaArtifacts,
			HasCSharpShapeInputs: preflight.HasCSharpTraceRowShapeInputs,
			HasLiveCSharpRows: preflight.HasLiveCSharpTraceRows,
			ReadyForRuntimeComparison: false,
			fixturePlan.JavaSource,
			IsLive: false);
	}

	private static void Add(
		ICollection<FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStep> steps,
		FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepKind kind,
		FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStepStatus status,
		bool blocks,
		string target,
		string requiredEvidence,
		string notes)
	{
		steps.Add(new FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonStep(
			steps.Count + 1,
			kind,
			status,
			blocks,
			target,
			requiredEvidence,
			notes));
	}
}
