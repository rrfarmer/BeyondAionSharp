namespace Aion.GameServer.Services;

public enum FindGroupMutationPostCSharpLiveTraceRowFixturePlanStatus
{
	BlockedPendingLiveBoundaryFixture,
	ReadyForImplementationDesignOnly,
}

public enum FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind
{
	FixtureHarness,
	LiveDispatchGuard,
	BoundaryAcceptanceTrace,
	SharedSingletonMutationTrace,
	DirectPacketIntentTrace,
	BoundaryExecutorTrace,
	RegistrySendObservationTrace,
	RuntimeRowSerialization,
	ComparisonPreflightInput,
}

public enum FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus
{
	DesignOnly,
	BlockedPendingLiveBoundaryFixture,
	BlockedPendingLiveEmitter,
	BlockedPendingRegistryObservation,
}

public sealed record FindGroupMutationPostCSharpLiveTraceRowFixtureStep(
	int Order,
	FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind Kind,
	FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus Status,
	string Target,
	string RequiredEvidence,
	string TraceFields,
	string Notes);

public sealed record FindGroupMutationPostCSharpLiveTraceRowFixturePlan(
	FindGroupMutationPostCSharpLiveTraceRowFixturePlanStatus Status,
	IReadOnlyList<int> Actions,
	IReadOnlyList<FindGroupMutationPostCSharpLiveTraceRowFixtureStep> Steps,
	string FixtureClassName,
	string TraceName,
	bool IsCmFindGroupBoundaryWired,
	bool ShouldInvokeLiveSideEffects,
	bool RequiresLiveBoundaryFixture,
	bool RequiresLiveEmitter,
	bool RequiresRegistryObservation,
	bool RequiresGeneratedJavaArtifacts,
	bool FeedsArtifactComparisonPreflight,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live C# fixture plan for future CM_FIND_GROUP action 2/6
/// mutation-post live trace rows. It names the required harness and row evidence only;
/// it does not wire live ProcessPacketAsync dispatch or emit runtime rows.
/// </summary>
public static class FindGroupMutationPostCSharpLiveTraceRowFixturePlanService
{
	public const string FixtureClassName = "GameServerConnectionFindGroupMutationPostLiveTraceRowFixture";

	public static FindGroupMutationPostCSharpLiveTraceRowFixturePlan Create()
	{
		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var emitter = FindGroupMutationPostCSharpTraceEmitterDesignReportService.Create();
		var registryContract = FindGroupMutationPostRegistryObservationTraceContractService.Create();
		var preflight = FindGroupMutationPostArtifactComparisonPreflightService.Create();
		var steps = new List<FindGroupMutationPostCSharpLiveTraceRowFixtureStep>();

		Add(steps,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.FixtureHarness,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.BlockedPendingLiveBoundaryFixture,
			FixtureClassName,
			"Build a focused C# fixture that can exercise CM_FIND_GROUP action 2 and 6 through the real connection boundary under an explicit trace-only guard.",
			"action, traceName, traceSource=CSharp",
			"The fixture does not exist yet and must not turn ordinary CmFindGroup dispatch on by default.");

		Add(steps,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.LiveDispatchGuard,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.DesignOnly,
			"GameServerConnection.ProcessPacketAsync case CmFindGroup",
			"Keep production live dispatch disabled until the guarded fixture, registry observation, Java artifacts, and comparison preflight all pass.",
			"boundaryAccepted=false outside guarded fixture",
			"Trace planning must not weaken the existing deferred live-dispatch gate.");

		Add(steps,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.BoundaryAcceptanceTrace,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.BlockedPendingLiveBoundaryFixture,
			"GameServerConnection.ProcessPacketAsync live CmFindGroup branch",
			"Capture accepted triggering client packet, active player object id/race, schema version, trace name, and action before invoking FindGroup planning/execution.",
			"schemaVersion, traceName, traceSource=CSharp, action, boundaryAccepted, activePlayerObjectId, activePlayerRace",
			"Must come from the connection boundary, not from CreateDisabledFindGroupBoundaryPlan.");

		Add(steps,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.SharedSingletonMutationTrace,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.BlockedPendingLiveEmitter,
			"FindGroupRecruitmentPlanService action 2/6 mutation plans",
			"Capture recruitment/application singleton mutation after state update and before posted message/refreshed list direct-packet evidence.",
			"serverEpochSeconds, mutationKind, mutatedEntryObjectId, stateMutationRecordedBeforeDirectPackets, visibleEntryObjectIdsAfterMutation",
			"Action 2 must represent recruitment state; Action 6 must represent application state.");

		Add(steps,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.DirectPacketIntentTrace,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.BlockedPendingLiveEmitter,
			"FindGroupConnectionBoundarySideEffectCompositionEvidenceService direct packet intents",
			"Capture Java-shaped posted system message and refreshed list intent fields before executor dispatch.",
			"postedSystemMessageRecipientObjectId, postedSystemMessageType, postedSystemMessageId, refreshedListRecipientObjectId, refreshedListPacketType, refreshedListAction",
			"Action 2 requires SmSystemMessage id 1400392 then SmFindGroup action 0; action 6 requires id 1400393 then action 4.");

		Add(steps,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.BoundaryExecutorTrace,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.BlockedPendingLiveBoundaryFixture,
			"FindGroupSideEffectDispatchExecutorService invoked from GameServerConnection.ProcessPacketAsync",
			"Capture executorInvokedFromBoundary=true only when the live CmFindGroup boundary invokes the executor.",
			"executorInvokedFromBoundary=true",
			"Opt-in executor calls from tests remain insufficient for live boundary parity.");

		Add(steps,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.RegistrySendObservationTrace,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.BlockedPendingRegistryObservation,
			"IGameClientConnectionRegistry direct-send observation",
			"Observe exactly two direct sends per action to the active player, posted system message before refreshed list, with no broadcasts and no invite dispatches.",
			"registrySendsObservedInOrder=true, worldBroadcastCount=0, inviteDispatchCount=0",
			$"Registry contract requirements={registryContract.Requirements.Count}; this observation must be live.");

		Add(steps,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.RuntimeRowSerialization,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.BlockedPendingLiveEmitter,
			"future C# mutation-post runtime trace row serializer",
			$"Serialize one schema-v{schema.SchemaVersion} C# row for action 2 and one for action 6 using the {schema.TraceName} field set.",
			string.Join(", ", schema.RequiredFields.Select(field => field.Name)),
			$"Emitter design rows={emitter.Rows.Count}; generated rows must validate against the same shape as Java artifacts.");

		Add(steps,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind.ComparisonPreflightInput,
			FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus.DesignOnly,
			"FindGroupMutationPostArtifactComparisonPreflightService",
			"Feed live C# rows into artifact comparison preflight only after generated Java artifacts and registry observation exist.",
			"hasLiveCSharpTraceRows=true, hasRegistryObservation=true",
			$"Current default preflight status remains {preflight.Status}; this plan does not execute comparison.");

		var stepArray = steps.ToArray();

		return new FindGroupMutationPostCSharpLiveTraceRowFixturePlan(
			FindGroupMutationPostCSharpLiveTraceRowFixturePlanStatus.BlockedPendingLiveBoundaryFixture,
			schema.SupportedActions.Select(action => action.Action).ToArray(),
			stepArray,
			FixtureClassName,
			schema.TraceName,
			IsCmFindGroupBoundaryWired: false,
			ShouldInvokeLiveSideEffects: false,
			RequiresLiveBoundaryFixture: true,
			RequiresLiveEmitter: true,
			RequiresRegistryObservation: true,
			RequiresGeneratedJavaArtifacts: true,
			FeedsArtifactComparisonPreflight: true,
			ReadyForRuntimeComparison: false,
			schema.JavaSource,
			IsLive: false);
	}

	private static void Add(
		ICollection<FindGroupMutationPostCSharpLiveTraceRowFixtureStep> steps,
		FindGroupMutationPostCSharpLiveTraceRowFixtureStepKind kind,
		FindGroupMutationPostCSharpLiveTraceRowFixtureStepStatus status,
		string target,
		string requiredEvidence,
		string traceFields,
		string notes)
	{
		steps.Add(new FindGroupMutationPostCSharpLiveTraceRowFixtureStep(
			steps.Count + 1,
			kind,
			status,
			target,
			requiredEvidence,
			traceFields,
			notes));
	}
}
