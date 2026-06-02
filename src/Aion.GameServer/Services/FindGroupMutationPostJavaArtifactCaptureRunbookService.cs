namespace Aion.GameServer.Services;

public enum FindGroupMutationPostJavaArtifactCaptureRunbookStatus
{
	BlockedMissingJavaFixture,
	BlockedMissingJavaInstrumentation,
	ReadyForImplementationDesignOnly,
}

public enum FindGroupMutationPostJavaArtifactCaptureRunbookStepKind
{
	JavaFixtureClass,
	ClientPacketPayloadHook,
	ClientPacketRunImplHook,
	RecruitmentMutationHooks,
	ApplicationMutationHooks,
	TraceSerializer,
	ArtifactPaths,
	FocusedMavenCommand,
	ArtifactValidation,
	ComparisonPreflight,
}

public enum FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus
{
	DesignOnly,
	BlockedMissingJavaFixture,
	BlockedMissingJavaInstrumentation,
	BlockedMissingTraceSerializer,
	BlockedMissingGeneratedArtifacts,
}

public sealed record FindGroupMutationPostJavaArtifactCaptureRunbookStep(
	int Order,
	FindGroupMutationPostJavaArtifactCaptureRunbookStepKind Kind,
	FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus Status,
	string Target,
	string Requirement,
	string JavaSource,
	string Notes);

public sealed record FindGroupMutationPostJavaArtifactCaptureRunbook(
	FindGroupMutationPostJavaArtifactCaptureRunbookStatus Status,
	IReadOnlyList<FindGroupMutationPostJavaArtifactCaptureRunbookStep> Steps,
	string FixtureClassName,
	string CaptureFlag,
	string FocusedMavenCommand,
	string ArtifactRoot,
	IReadOnlyList<string> ExpectedArtifactPaths,
	bool HasActionTwoArtifactPath,
	bool HasActionSixArtifactPath,
	bool ReusesMutationPostSchema,
	bool ReusesArtifactValidator,
	bool FeedsComparisonPreflight,
	bool RequiresJavaFixture,
	bool RequiresJavaInstrumentation,
	bool RequiresTraceSerializer,
	bool RequiresGeneratedArtifacts,
	bool ReadyForRuntimeComparison,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live runbook metadata for future generated Java
/// CM_FIND_GROUP action 2/6 mutation-post trace artifacts. This does not modify
/// Java source and the named Maven command is not runnable until the fixture exists.
/// </summary>
public static class FindGroupMutationPostJavaArtifactCaptureRunbookService
{
	public const string FixtureClassName = "FindGroupMutationPostTraceCaptureTest";
	public const string CaptureFlag = "aion.findGroupMutationPost.capture";
	public const string ServerEpochSecondsProperty = "aion.findGroupMutationPost.serverEpochSeconds";
	public const int DeterministicServerEpochSeconds = 1700000000;

	public static FindGroupMutationPostJavaArtifactCaptureRunbook Create()
	{
		var instrumentation = FindGroupMutationPostJavaInstrumentationDesignReportService.Create();
		var schema = FindGroupMutationPostJavaTraceArtifactSchemaReportService.Create();
		var files = FindGroupMutationPostJavaTraceArtifactFileReportService.Create();
		var artifactPaths = files.Files.Select(file => file.ArtifactPath).ToArray();
		var steps = new List<FindGroupMutationPostJavaArtifactCaptureRunbookStep>();

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.JavaFixtureClass,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.DesignOnly,
			$"game-server/test/com/aionemu/gameserver/services/findgroup/{FixtureClassName}.java",
			"Create a narrow Java fixture that exercises CM_FIND_GROUP action 2 and 6 against deterministic players/state and writes artifacts only when the capture flag is enabled.",
			"CM_FIND_GROUP.readImpl/runImpl; FindGroupService.addRecruitment/addApplication",
			"Fixture scaffold exists and is Maven-runnable; it does not capture runtime behavior or write artifacts yet.");

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ClientPacketPayloadHook,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.BlockedMissingJavaInstrumentation,
			"CM_FIND_GROUP.readImpl action 2/6 payload capture",
			"Capture parsed action, playerOrTeamId, message, groupType, and action 6 classId/level after Java consumes packet fields.",
			"CM_FIND_GROUP.readImpl",
			"Payload fields are diagnostics for trace reproducibility; comparison keys remain schema-driven.");

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ClientPacketRunImplHook,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.BlockedMissingJavaInstrumentation,
			"CM_FIND_GROUP.runImpl active-player capture",
			"Capture activePlayerObjectId, activePlayerRace, action, and boundaryAccepted before dispatching to FindGroupService.",
			"CM_FIND_GROUP.runImpl",
			"This is the Java source-of-truth boundary for action 2/6 service dispatch.");

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.RecruitmentMutationHooks,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.BlockedMissingJavaInstrumentation,
			"FindGroupService.addRecruitment action 2 hooks",
			"Record recruitments.put before direct packets, SmSystemMessage id 1400392, refreshed SmFindGroup action 0, and visible recruitment ids after mutation.",
			"FindGroupService.addRecruitment; FindGroupService.showRecruitments",
			"Must preserve Java mutation-before-posted-message-before-refreshed-list ordering.");

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ApplicationMutationHooks,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.BlockedMissingJavaInstrumentation,
			"FindGroupService.addApplication action 6 hooks",
			"Record applications.put before direct packets, SmSystemMessage id 1400393, refreshed SmFindGroup action 4, and visible application ids after mutation.",
			"FindGroupService.addApplication; FindGroupService.showApplications",
			"Must preserve Java mutation-before-posted-message-before-refreshed-list ordering.");

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.TraceSerializer,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.BlockedMissingTraceSerializer,
			"future Java trace serializer",
			$"Serialize schemaVersion={schema.SchemaVersion}, traceName={schema.TraceName}, traceSource=Java, and all {schema.Fields.Count} schema fields in stable order.",
			"future Java serializer; FindGroupMutationPostJavaTraceArtifactSchemaReportService",
			"Serializer output must pass the existing C# artifact validator before comparison preflight.");

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ArtifactPaths,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.BlockedMissingGeneratedArtifacts,
			files.ArtifactRoot,
			$"Write exactly {string.Join(", ", artifactPaths)}.",
			"FindGroupMutationPostJavaTraceArtifactFileReportService",
			"Paths and file names are stable metadata; no files exist by default.");

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.FocusedMavenCommand,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.DesignOnly,
			FocusedMavenCommand(),
			"Run only after the Java fixture and gated serializer exist; this is the planned narrow Java parity command.",
			$"{FixtureClassName}.java",
			"The command is intentionally focused and capture-gated; it should not become a broad Maven run.");

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ArtifactValidation,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.DesignOnly,
			"FindGroupMutationPostJavaTraceArtifactValidatorService",
			"Validate generated action 2 and 6 artifacts for schema version/name, traceSource=Java, action mappings, packet ids/actions, and zero broadcast/invite counts.",
			"FindGroupMutationPostJavaTraceArtifactValidatorService",
			"Shape-valid artifacts still do not prove runtime parity.");

		Add(steps,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepKind.ComparisonPreflight,
			FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus.DesignOnly,
			"FindGroupMutationPostArtifactComparisonPreflightService",
			"Feed validated Java artifacts into the guarded preflight together with live C# rows, registry observation, key projection, and comparison execution result.",
			"FindGroupService.addRecruitment/addApplication generated traces",
			"Preflight remains blocked until projected Java/C# rows are actually compared and match.");

		var stepArray = steps.ToArray();

		return new FindGroupMutationPostJavaArtifactCaptureRunbook(
			FindGroupMutationPostJavaArtifactCaptureRunbookStatus.BlockedMissingJavaInstrumentation,
			stepArray,
			FixtureClassName,
			CaptureFlag,
			FocusedMavenCommand(),
			files.ArtifactRoot,
			artifactPaths,
			HasActionTwoArtifactPath: files.Files.Any(file => file.Action == 2),
			HasActionSixArtifactPath: files.Files.Any(file => file.Action == 6),
			ReusesMutationPostSchema: schema.ReusesMutationPostBoundaryTraceSchema,
			ReusesArtifactValidator: instrumentation.ReusesTraceArtifactValidator,
			FeedsComparisonPreflight: true,
			RequiresJavaFixture: true,
			RequiresJavaInstrumentation: true,
			RequiresTraceSerializer: true,
			RequiresGeneratedArtifacts: true,
			ReadyForRuntimeComparison: false,
			schema.TraceName,
			instrumentation.JavaSource,
			IsLive: false);
	}

	public static string FocusedMavenCommand() =>
		$"mvn -pl game-server -am test \"-Dtest={FixtureClassName}\" \"-D{CaptureFlag}=true\" \"-D{ServerEpochSecondsProperty}={DeterministicServerEpochSeconds}\" \"-Dmaven.test.skip=false\" \"-Dsurefire.failIfNoSpecifiedTests=false\"";

	private static void Add(
		ICollection<FindGroupMutationPostJavaArtifactCaptureRunbookStep> steps,
		FindGroupMutationPostJavaArtifactCaptureRunbookStepKind kind,
		FindGroupMutationPostJavaArtifactCaptureRunbookStepStatus status,
		string target,
		string requirement,
		string javaSource,
		string notes)
	{
		steps.Add(new FindGroupMutationPostJavaArtifactCaptureRunbookStep(
			steps.Count + 1,
			kind,
			status,
			target,
			requirement,
			javaSource,
			notes));
	}
}
