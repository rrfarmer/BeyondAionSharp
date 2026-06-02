namespace Aion.GameServer.Services;

public enum FindGroupMutationPostJavaArtifactCaptureImplementationReadinessStatus
{
	BlockedMissingJavaFixture,
	ReadyForJavaImplementation,
}

public enum FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind
{
	FixtureClass,
	FixtureScenarios,
	InstrumentationHooks,
	TraceSerializer,
	ArtifactFiles,
	ArtifactValidation,
	FocusedMavenCommand,
	ComparisonHandOff,
}

public enum FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus
{
	BlockedMissingJavaFixture,
	BlockedMissingJavaInstrumentation,
	BlockedMissingTraceSerializer,
	BlockedMissingGeneratedArtifacts,
	DesignOnly,
}

public sealed record FindGroupMutationPostJavaArtifactCaptureImplementationTask(
	int Order,
	FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind Kind,
	FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus Status,
	string Target,
	string RequiredWork,
	string JavaSource,
	string AcceptanceEvidence,
	string Notes);

public sealed record FindGroupMutationPostJavaArtifactCaptureImplementationReadiness(
	FindGroupMutationPostJavaArtifactCaptureImplementationReadinessStatus Status,
	IReadOnlyList<FindGroupMutationPostJavaArtifactCaptureImplementationTask> Tasks,
	string FixtureClassName,
	string CaptureFlag,
	string FocusedMavenCommand,
	string ArtifactRoot,
	IReadOnlyList<string> ExpectedArtifactPaths,
	bool HasFixtureTask,
	bool HasActionTwoScenario,
	bool HasActionSixScenario,
	bool HasInstrumentationHooks,
	bool HasTraceSerializerTask,
	bool HasArtifactValidationTask,
	bool HasFocusedMavenCommand,
	bool RequiresJavaFixture,
	bool RequiresJavaInstrumentation,
	bool RequiresTraceSerializer,
	bool RequiresGeneratedArtifacts,
	bool ReadyForRuntimeComparison,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: implementation-readiness checklist for the future Java
/// CM_FIND_GROUP action 2/6 mutation-post artifact capture. This is still non-live
/// metadata; it does not create the Java fixture, instrumentation, serializer, or artifacts.
/// </summary>
public static class FindGroupMutationPostJavaArtifactCaptureImplementationReadinessService
{
	public static FindGroupMutationPostJavaArtifactCaptureImplementationReadiness Create()
	{
		var runbook = FindGroupMutationPostJavaArtifactCaptureRunbookService.Create();
		var instrumentation = FindGroupMutationPostJavaInstrumentationDesignReportService.Create();
		var schema = FindGroupMutationPostJavaTraceArtifactSchemaReportService.Create();
		var files = FindGroupMutationPostJavaTraceArtifactFileReportService.Create(runbook.ArtifactRoot);
		var tasks = new List<FindGroupMutationPostJavaArtifactCaptureImplementationTask>();

		Add(tasks,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FixtureClass,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.BlockedMissingJavaFixture,
			$"game-server/src/test/java/.../{runbook.FixtureClassName}.java",
			"Create the gated Java fixture class and keep capture disabled unless the system property flag is true.",
			"CM_FIND_GROUP.readImpl/runImpl; FindGroupService.addRecruitment/addApplication",
			$"Focused Maven command discovers and runs {runbook.FixtureClassName} without broad test selection.",
			"Do not change Java gameplay behavior while adding the test fixture.");

		Add(tasks,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FixtureScenarios,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.BlockedMissingJavaFixture,
			runbook.FixtureClassName,
			"Build deterministic action 2 recruitment and action 6 application scenarios with stable active player ids, race, message, group type, class id, and level inputs.",
			"CM_FIND_GROUP.readImpl actions 2 and 6",
			"Generated rows include exactly actions 2 and 6 and preserve Java action-specific posted message/refreshed list mappings.",
			"Scenario setup must be narrow enough for a targeted Maven test and must not require a full game-server startup.");

		Add(tasks,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.InstrumentationHooks,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.BlockedMissingJavaInstrumentation,
			"CM_FIND_GROUP and FindGroupService trace hooks",
			$"Implement hook points for {string.Join(", ", instrumentation.Points.Select(point => point.ExpectedTraceEvent))}.",
			"CM_FIND_GROUP.readImpl/runImpl; FindGroupService.addRecruitment/addApplication/showRecruitments/showApplications",
			"Trace emission records mutation before posted system message before refreshed list for both action 2 and action 6.",
			"Hooks must not add synchronization, blocking IO, or PacketSendUtility ordering changes.");

		Add(tasks,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.TraceSerializer,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.BlockedMissingTraceSerializer,
			"future Java mutation-post trace serializer",
			$"Serialize schemaVersion={schema.SchemaVersion}, traceName={schema.TraceName}, traceSource=Java, and all {schema.Fields.Count} schema fields in schema order.",
			"future Java serializer",
			"JSON passes FindGroupMutationPostJavaTraceArtifactValidatorService with no missing fields or action mapping mismatches.",
			"Use stable field names and integer/string/boolean/array shapes from the schema report.");

		Add(tasks,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ArtifactFiles,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.BlockedMissingGeneratedArtifacts,
			files.ArtifactRoot,
			$"Write one generated Java artifact per action under {files.ArtifactRoot}.",
			"future Java fixture artifact writer",
			$"Expected files exist: {string.Join(", ", files.Files.Select(file => file.ArtifactPath))}.",
			"Do not treat files as parity evidence until validation and Java/C# row comparison pass.");

		Add(tasks,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ArtifactValidation,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.DesignOnly,
			"FindGroupMutationPostJavaTraceArtifactValidatorService",
			"Validate schema version/name, traceSource=Java, action 2/6 mappings, posted system message ids, refreshed list actions, and zero broadcast/invite counts.",
			"FindGroupMutationPostJavaTraceArtifactValidatorService",
			"Both expected action files are shape-valid before comparison preflight consumes them.",
			"Shape-valid Java artifacts still need live C# rows and deterministic comparison.");

		Add(tasks,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FocusedMavenCommand,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.DesignOnly,
			runbook.FocusedMavenCommand,
			"Run only after the Java fixture, instrumentation, serializer, and capture-gated artifact writer exist.",
			$"{runbook.FixtureClassName}.java",
			"Maven output shows the focused fixture ran and generated the expected action 2/6 artifact files.",
			"This command is the planned Java parity command; it is not runnable evidence yet.");

		Add(tasks,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ComparisonHandOff,
			FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus.DesignOnly,
			"FindGroupMutationPostArtifactComparisonPreflightService",
			"Hand validated Java rows to comparison preflight together with live C# rows, registry observation, and projected-row comparison execution.",
			"FindGroupService.addRecruitment/addApplication generated traces",
			"Preflight can distinguish missing Java rows, missing live C# rows, registry gaps, and comparison mismatches.",
			"Do not claim verified parity from this checklist alone.");

		var taskArray = tasks.ToArray();

		return new FindGroupMutationPostJavaArtifactCaptureImplementationReadiness(
			FindGroupMutationPostJavaArtifactCaptureImplementationReadinessStatus.BlockedMissingJavaFixture,
			taskArray,
			runbook.FixtureClassName,
			runbook.CaptureFlag,
			runbook.FocusedMavenCommand,
			files.ArtifactRoot,
			files.Files.Select(file => file.ArtifactPath).ToArray(),
			HasFixtureTask: taskArray.Any(task => task.Kind == FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FixtureClass),
			HasActionTwoScenario: schema.Actions.Any(action => action.Action == 2),
			HasActionSixScenario: schema.Actions.Any(action => action.Action == 6),
			HasInstrumentationHooks: instrumentation.Points.Count > 0,
			HasTraceSerializerTask: taskArray.Any(task => task.Kind == FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.TraceSerializer),
			HasArtifactValidationTask: taskArray.Any(task => task.Kind == FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.ArtifactValidation),
			HasFocusedMavenCommand: taskArray.Any(task => task.Kind == FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind.FocusedMavenCommand),
			RequiresJavaFixture: runbook.RequiresJavaFixture,
			RequiresJavaInstrumentation: runbook.RequiresJavaInstrumentation,
			RequiresTraceSerializer: runbook.RequiresTraceSerializer,
			RequiresGeneratedArtifacts: runbook.RequiresGeneratedArtifacts,
			ReadyForRuntimeComparison: false,
			schema.TraceName,
			"Java sources reviewed: CM_FIND_GROUP.readImpl/runImpl actions 2 and 6; FindGroupService.addRecruitment/addApplication/showRecruitments/showApplications.",
			IsLive: false);
	}

	private static void Add(
		ICollection<FindGroupMutationPostJavaArtifactCaptureImplementationTask> tasks,
		FindGroupMutationPostJavaArtifactCaptureImplementationTaskKind kind,
		FindGroupMutationPostJavaArtifactCaptureImplementationTaskStatus status,
		string target,
		string requiredWork,
		string javaSource,
		string acceptanceEvidence,
		string notes)
	{
		tasks.Add(new FindGroupMutationPostJavaArtifactCaptureImplementationTask(
			tasks.Count + 1,
			kind,
			status,
			target,
			requiredWork,
			javaSource,
			acceptanceEvidence,
			notes));
	}
}
