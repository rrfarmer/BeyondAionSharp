namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker
{
	RuntimeComparisonDesign,
	TraceArtifactSchema,
	JavaInstrumentation,
	JavaTraceSerializer,
	GeneratedJavaTraceArtifacts,
	CSharpArtifactReader,
	CSharpTraceEmitterDesign,
	GeneratedArtifactExecutionPlan,
	LiveCSharpPacketHooks,
	RuntimeComparisonEvidence,
}

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus
{
	SatisfiedByNonLiveMetadata,
	BlockedMissingPrerequisite,
	BlockedMissingJavaArtifact,
	BlockedInvalidJavaArtifact,
	BlockedMissingCSharpImplementation,
	BlockedMissingCSharpRuntimeTrace,
	BlockedMissingRuntimeEvidence,
	BlockedScenarioMismatch,
	BlockedRowCountMismatch,
	BlockedKeyMismatch,
	BlockedComparisonNotExecuted,
}

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker Blocker,
	PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus Status,
	bool BlocksRuntimeComparison,
	string JavaSource,
	string CSharpTarget,
	string Evidence,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> Rows,
	bool HasRuntimeComparisonDesign,
	bool HasTraceArtifactSchema,
	bool HasGeneratedJavaTraceArtifactDirectoryReport,
	bool HasShapeValidGeneratedJavaTraceArtifacts,
	bool HasRuntimeComparisonContractReport,
	bool HasRuntimeComparisonPreflightReport,
	bool HasRuntimeComparisonKeyProjectionReport,
	bool NeedsJavaInstrumentation,
	bool NeedsJavaTraceSerializer,
	bool NeedsGeneratedJavaTraceArtifacts,
	bool NeedsCSharpArtifactReader,
	bool HasCSharpTraceEmitterDesign,
	bool NeedsCSharpTraceEmitter,
	bool HasGeneratedArtifactExecutionPlan,
	bool NeedsGeneratedArtifactExecutionPlan,
	bool NeedsLiveCSharpPacketHooks,
	bool NeedsCSharpRuntimeTraceOutput,
	bool NeedsRuntimeComparisonPreflightAlignment,
	bool NeedsRuntimeComparisonKeyAlignment,
	bool NeedsRuntimeComparisonExecution,
	bool NeedsRuntimeComparisonEvidence,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: readiness gate between the protection stop-trigger trace artifact schema and future
/// Java/C# runtime comparison for PlayerController.stopProtectionActiveTask packet callers.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService
{
	public static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReport Create(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport? runtimeDesign,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport? traceSchema,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? artifactDirectoryReport = null,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport? comparisonContract = null,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReport? preflightReport = null,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport? keyProjectionReport = null,
		PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport? csharpTraceEmitterDesign = null,
		PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport? generatedArtifactExecutionPlan = null)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow>();

		AddRuntimeComparisonDesign(rows, runtimeDesign);
		AddTraceArtifactSchema(rows, traceSchema);
		AddJavaInstrumentation(rows, traceSchema);
		AddJavaTraceSerializer(rows, traceSchema);
		AddGeneratedJavaTraceArtifacts(rows, traceSchema, artifactDirectoryReport);
		AddCSharpArtifactReader(rows, traceSchema);
		AddCSharpTraceEmitterDesign(rows, runtimeDesign, traceSchema, csharpTraceEmitterDesign);
		AddGeneratedArtifactExecutionPlan(rows, runtimeDesign, traceSchema, csharpTraceEmitterDesign, generatedArtifactExecutionPlan);
		AddLiveCSharpPacketHooks(rows, runtimeDesign);
		AddRuntimeComparisonEvidence(rows, comparisonContract, preflightReport, keyProjectionReport);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReport(
			rowArray,
			HasRuntimeComparisonDesign: runtimeDesign != null,
			HasTraceArtifactSchema: traceSchema != null,
			HasGeneratedJavaTraceArtifactDirectoryReport: artifactDirectoryReport != null,
			HasShapeValidGeneratedJavaTraceArtifacts: artifactDirectoryReport?.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
			HasRuntimeComparisonContractReport: comparisonContract != null,
			HasRuntimeComparisonPreflightReport: preflightReport != null,
			HasRuntimeComparisonKeyProjectionReport: keyProjectionReport != null,
			NeedsJavaInstrumentation: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.JavaInstrumentation && row.BlocksRuntimeComparison),
			NeedsJavaTraceSerializer: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.JavaTraceSerializer && row.BlocksRuntimeComparison),
			NeedsGeneratedJavaTraceArtifacts: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedJavaTraceArtifacts && row.BlocksRuntimeComparison),
			NeedsCSharpArtifactReader: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpArtifactReader && row.BlocksRuntimeComparison),
			HasCSharpTraceEmitterDesign: csharpTraceEmitterDesign != null,
			NeedsCSharpTraceEmitter: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpTraceEmitterDesign && row.BlocksRuntimeComparison),
			HasGeneratedArtifactExecutionPlan: generatedArtifactExecutionPlan != null,
			NeedsGeneratedArtifactExecutionPlan: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedArtifactExecutionPlan && row.BlocksRuntimeComparison),
			NeedsLiveCSharpPacketHooks: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.LiveCSharpPacketHooks && row.BlocksRuntimeComparison),
			NeedsCSharpRuntimeTraceOutput: comparisonContract?.NeedsCSharpRuntimeTrace == true || preflightReport?.NeedsCSharpTraceRows == true || keyProjectionReport?.NeedsCSharpKeys == true,
			NeedsRuntimeComparisonPreflightAlignment: preflightReport?.NeedsScenarioAlignment == true || preflightReport?.NeedsRowCountAlignment == true,
			NeedsRuntimeComparisonKeyAlignment: keyProjectionReport?.NeedsKeyAlignment == true,
			NeedsRuntimeComparisonExecution: comparisonContract?.NeedsExecutedComparison == true || preflightReport?.NeedsComparisonExecution == true || keyProjectionReport?.NeedsComparisonExecution == true,
			NeedsRuntimeComparisonEvidence: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence && row.BlocksRuntimeComparison),
			ReadyForRuntimeComparison: rowArray.Length > 0 && rowArray.All(row => !row.BlocksRuntimeComparison),
			"Protection stop-trigger runtime comparison readiness gate",
			IsLive: false);
	}

	private static void AddRuntimeComparisonDesign(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport? runtimeDesign)
	{
		if (runtimeDesign == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonDesign,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"packet stop-trigger source review",
				"PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService",
				"missing runtime comparison design report",
				"Trace schema composition cannot advance without packet scenario and observable metadata.");
			return;
		}

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonDesign,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
			blocks: false,
			runtimeDesign.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport",
			$"rows={runtimeDesign.Rows.Count}",
			"Packet scenarios and expected observables exist as non-live metadata only.");
	}

	private static void AddTraceArtifactSchema(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport? traceSchema)
	{
		if (traceSchema == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.TraceArtifactSchema,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"packet/controller trace artifact requirements",
				"PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService",
				"missing trace artifact schema report",
				"Runtime comparison must be blocked before artifact-reader work if trace fields and return reasons are undefined.");
			return;
		}

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.TraceArtifactSchema,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
			blocks: false,
			traceSchema.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport",
			$"phases={traceSchema.Phases.Count}; fields={traceSchema.Fields.Count}; returnReasons={traceSchema.PacketReturnReasons.Count}",
			"Schema metadata exists but still requires Java instrumentation and parser implementation.");
	}

	private static void AddJavaInstrumentation(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport? traceSchema)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.JavaInstrumentation,
			traceSchema == null
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact,
			blocks: true,
			"CM_MOVE/CM_MOVE_IN_AIR/action packets; PlayerController; CreatureController",
			"future Java observer hooks outside dotnetConversion",
			traceSchema == null ? "trace schema missing" : "trace phases and fields defined but Java hooks absent",
			"Packet/controller Java code is not instrumented; production behavior must not be changed by tracing.");
	}

	private static void AddJavaTraceSerializer(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport? traceSchema)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.JavaTraceSerializer,
			traceSchema == null
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact,
			blocks: true,
			"future Java trace serialization boundary",
			"future schema-v1 Java artifact writer",
			traceSchema == null ? "trace schema missing" : "schema version and field list defined but no serializer exists",
			"Serializer must preserve invariant numeric formatting, enum names, event ordering, and optional fields.");
	}

	private static void AddGeneratedJavaTraceArtifacts(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport? traceSchema,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? artifactDirectoryReport)
	{
		if (traceSchema == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedJavaTraceArtifacts,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"Java runtime packet/controller execution",
				"future generated protection stop-trigger trace fixtures",
				"trace schema missing",
				"Need schema-v1 fields before generated Java trace artifacts can be accepted.");
			return;
		}

		if (artifactDirectoryReport == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedJavaTraceArtifacts,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact,
				blocks: true,
				"Java runtime packet/controller execution",
				"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReportService",
				"no artifact directory report supplied",
				"Need generated traces for movement thresholds, early action packets, composite invalid-after-stop, emotion late-stop/no-stop, delayed teleport branches, and controller races.");
			return;
		}

		if (artifactDirectoryReport.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedJavaTraceArtifacts,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
				blocks: false,
				"Java runtime packet/controller execution",
				"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport",
				$"status={artifactDirectoryReport.Status}; files={artifactDirectoryReport.Files.Count}; shapeValidFiles={artifactDirectoryReport.Files.Count(file => file.ValidationReport.IsValidSchemaV1)}",
				"Generated Java artifact JSON is schema-valid only; runtime comparison still needs live C# hooks and deterministic Java/C# output comparison.");
			return;
		}

		var status = artifactDirectoryReport.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.InvalidArtifacts
			? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedInvalidJavaArtifact
			: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact;

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedJavaTraceArtifacts,
			status,
			blocks: true,
			"Java runtime packet/controller execution",
			"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport",
			$"status={artifactDirectoryReport.Status}; files={artifactDirectoryReport.Files.Count}; validFiles={artifactDirectoryReport.Files.Count(file => file.ValidationReport.IsValidSchemaV1)}",
			artifactDirectoryReport.Notes);
	}

	private static void AddCSharpArtifactReader(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport? traceSchema)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpArtifactReader,
			traceSchema == null
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
			blocks: traceSchema == null,
			"schema-v1 Java trace artifact",
			"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService / PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReportService",
			traceSchema == null ? "trace schema missing" : "schema-v1 validator and artifact directory report exist",
			traceSchema == null
				? "Reader must validate schema version, phase ordering, enum return reasons, invariant floats, optional timestamps, and packet-specific payloads."
				: "C# can shape-validate schema-v1 Java artifacts, but this is not a runtime comparator and cannot prove parity.");
	}

	private static void AddLiveCSharpPacketHooks(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport? runtimeDesign)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.LiveCSharpPacketHooks,
			runtimeDesign == null
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpImplementation,
			blocks: true,
			"Java packet stop callers",
			"future C# packet handler stop hooks",
			runtimeDesign == null ? "runtime design missing" : $"requiresLiveHooks={runtimeDesign.RequiresLiveCSharpPacketHooks}",
			"Runtime comparison still needs live C# packet/controller execution surfaces; this report does not enable them.");
	}

	private static void AddCSharpTraceEmitterDesign(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport? runtimeDesign,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport? traceSchema,
		PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport? csharpTraceEmitterDesign)
	{
		if (runtimeDesign == null || traceSchema == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpTraceEmitterDesign,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"future C# stop-trigger trace emitter",
				"PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService",
				runtimeDesign == null ? "runtime design missing" : "trace schema missing",
				"Need runtime comparison design and trace schema before C# trace emitter hook sites can be planned.");
			return;
		}

		if (csharpTraceEmitterDesign == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpTraceEmitterDesign,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpImplementation,
				blocks: true,
				"future C# stop-trigger trace emitter",
				"PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService",
				"missing C# trace emitter design report",
				"Need a non-live hook-site plan before production packet/controller trace emission can be considered.");
			return;
		}

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpTraceEmitterDesign,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpImplementation,
			blocks: true,
			csharpTraceEmitterDesign.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport",
			$"rows={csharpTraceEmitterDesign.Rows.Count}; packetHooks={csharpTraceEmitterDesign.HasPacketHookSites}; controllerHooks={csharpTraceEmitterDesign.HasControllerHookSites}; teleportHooks={csharpTraceEmitterDesign.HasTeleportHookSites}; requiresLiveEmitter={csharpTraceEmitterDesign.RequiresLiveEmitter}",
			"C# trace emitter hook sites are documented as non-live design only; production packet/controller hooks remain disabled.");
	}

	private static void AddGeneratedArtifactExecutionPlan(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport? runtimeDesign,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport? traceSchema,
		PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport? csharpTraceEmitterDesign,
		PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport? generatedArtifactExecutionPlan)
	{
		if (runtimeDesign == null || traceSchema == null || csharpTraceEmitterDesign == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedArtifactExecutionPlan,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"protection stop-trigger Java runtime artifact generation prerequisites",
				"PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanService",
				runtimeDesign == null ? "runtime design missing" : traceSchema == null ? "trace schema missing" : "C# trace emitter design missing",
				"Need runtime comparison design, trace schema, and C# emitter hook-site design before execution-plan gates can be surfaced.");
			return;
		}

		if (generatedArtifactExecutionPlan == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedArtifactExecutionPlan,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite,
				blocks: true,
				"protection stop-trigger Java runtime artifact generation prerequisites",
				"PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanService",
				"missing generated-artifact execution plan report",
				"Need the non-live execution plan to sequence Java tooling, artifact generation, C# trace capture, key projection, and comparison execution gates.");
			return;
		}

		var status = GetGeneratedArtifactExecutionPlanStatus(generatedArtifactExecutionPlan);
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedArtifactExecutionPlan,
			status,
			blocks: status != PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
			generatedArtifactExecutionPlan.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport",
			$"planRows={generatedArtifactExecutionPlan.Rows.Count}; needsJavaTooling={generatedArtifactExecutionPlan.NeedsJavaTooling}; needsJavaArtifacts={generatedArtifactExecutionPlan.NeedsJavaArtifacts}; needsCSharpEmitter={generatedArtifactExecutionPlan.NeedsCSharpEmitter}; needsRuntimeEvidence={generatedArtifactExecutionPlan.NeedsRuntimeEvidence}; needsComparisonExecution={generatedArtifactExecutionPlan.NeedsComparisonExecution}; ready={generatedArtifactExecutionPlan.ReadyForRuntimeComparison}",
			GetGeneratedArtifactExecutionPlanNotes(generatedArtifactExecutionPlan));
	}

	private static void AddRuntimeComparisonEvidence(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport? comparisonContract,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReport? preflightReport,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport? keyProjectionReport)
	{
		if (keyProjectionReport != null)
		{
			var status = GetRuntimeComparisonEvidenceStatus(keyProjectionReport);
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence,
				status,
				blocks: status != PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
				keyProjectionReport.JavaSource,
				"PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport",
				$"keyRows={keyProjectionReport.Rows.Count}; javaKeys={keyProjectionReport.JavaKeys.Count}; csharpKeys={keyProjectionReport.CSharpKeys.Count}; needsJavaKeys={keyProjectionReport.NeedsJavaKeys}; needsCSharpKeys={keyProjectionReport.NeedsCSharpKeys}; needsKeyAlignment={keyProjectionReport.NeedsKeyAlignment}; needsComparisonExecution={keyProjectionReport.NeedsComparisonExecution}; preflightNeedsScenarioAlignment={preflightReport?.NeedsScenarioAlignment == true}; preflightNeedsRowCountAlignment={preflightReport?.NeedsRowCountAlignment == true}",
				GetRuntimeComparisonEvidenceNotes(keyProjectionReport));
			return;
		}

		if (preflightReport != null)
		{
			var status = GetRuntimeComparisonEvidenceStatus(preflightReport);
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence,
				status,
				blocks: status != PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
				preflightReport.JavaSource,
				"PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReport",
				$"preflightRows={preflightReport.Rows.Count}; needsJavaArtifacts={preflightReport.NeedsJavaArtifacts}; needsCSharpTraceRows={preflightReport.NeedsCSharpTraceRows}; needsScenarioAlignment={preflightReport.NeedsScenarioAlignment}; needsRowCountAlignment={preflightReport.NeedsRowCountAlignment}; needsComparisonExecution={preflightReport.NeedsComparisonExecution}",
				GetRuntimeComparisonEvidenceNotes(preflightReport));
			return;
		}

		if (comparisonContract != null)
		{
			var status = GetRuntimeComparisonEvidenceStatus(comparisonContract);
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence,
				status,
				blocks: status != PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata,
				comparisonContract.JavaSource,
				"PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport",
				$"contractRows={comparisonContract.Rows.Count}; needsJavaArtifacts={comparisonContract.NeedsJavaArtifacts}; needsCSharpRuntimeTrace={comparisonContract.NeedsCSharpRuntimeTrace}; needsExecutedComparison={comparisonContract.NeedsExecutedComparison}",
				GetRuntimeComparisonEvidenceNotes(comparisonContract));
			return;
		}

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingRuntimeEvidence,
			blocks: true,
			"Java generated artifacts and future C# execution",
			"future runtime comparison test suite",
			"no Java/C# trace comparison executed",
			"Verified parity cannot be claimed until generated Java traces and C# outputs are compared deterministically.");
	}

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus GetRuntimeComparisonEvidenceStatus(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReport preflightReport)
	{
		if (preflightReport.NeedsJavaArtifacts)
		{
			return preflightReport.Rows.Any(row =>
				row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.JavaArtifacts
				&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedInvalidJavaArtifact)
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedInvalidJavaArtifact
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact;
		}

		if (preflightReport.NeedsCSharpTraceRows)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpRuntimeTrace;

		if (preflightReport.NeedsScenarioAlignment)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedScenarioMismatch;

		if (preflightReport.NeedsRowCountAlignment)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedRowCountMismatch;

		if (preflightReport.NeedsComparisonExecution)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedComparisonNotExecuted;

		return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata;
	}

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus GetRuntimeComparisonEvidenceStatus(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport keyProjectionReport)
	{
		if (keyProjectionReport.NeedsJavaKeys)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact;

		if (keyProjectionReport.NeedsCSharpKeys)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpRuntimeTrace;

		if (keyProjectionReport.NeedsKeyAlignment)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedKeyMismatch;

		if (keyProjectionReport.NeedsComparisonExecution)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedComparisonNotExecuted;

		return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata;
	}

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus GetRuntimeComparisonEvidenceStatus(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport comparisonContract)
	{
		if (comparisonContract.NeedsJavaArtifacts)
		{
			return comparisonContract.Rows.Any(row =>
				row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.JavaTraceArtifacts
				&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedInvalidJavaArtifact)
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedInvalidJavaArtifact
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact;
		}

		if (comparisonContract.NeedsCSharpRuntimeTrace)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpRuntimeTrace;

		if (comparisonContract.NeedsExecutedComparison)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedComparisonNotExecuted;

		return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata;
	}

	private static string GetRuntimeComparisonEvidenceNotes(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport comparisonContract)
	{
		if (comparisonContract.NeedsJavaArtifacts)
			return "Runtime evidence is blocked because generated Java trace artifacts are missing or invalid.";

		if (comparisonContract.NeedsCSharpRuntimeTrace)
			return "Runtime evidence is blocked because live C# stop-trigger trace output is missing.";

		if (comparisonContract.NeedsExecutedComparison)
			return "Runtime evidence is blocked because deterministic Java/C# trace comparison has not executed.";

		return "Runtime comparison contract has no current blockers, but verified parity still requires objective comparison evidence.";
	}

	private static string GetRuntimeComparisonEvidenceNotes(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReport preflightReport)
	{
		if (preflightReport.NeedsJavaArtifacts)
			return "Runtime evidence is blocked because generated Java trace artifacts are missing or invalid.";

		if (preflightReport.NeedsCSharpTraceRows)
			return "Runtime evidence is blocked because C# runtime trace rows are missing or invalid.";

		if (preflightReport.NeedsScenarioAlignment)
			return "Runtime evidence is blocked because Java artifact scenarios and C# trace scenarios do not align.";

		if (preflightReport.NeedsRowCountAlignment)
			return "Runtime evidence is blocked because Java artifact and C# trace row counts do not align.";

		if (preflightReport.NeedsComparisonExecution)
			return "Runtime evidence is blocked because preflight alignment has not executed deterministic Java/C# trace comparison.";

		return "Runtime comparison preflight has no current blockers, but verified parity still requires objective comparison evidence.";
	}

	private static string GetRuntimeComparisonEvidenceNotes(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport keyProjectionReport)
	{
		if (keyProjectionReport.NeedsJavaKeys)
			return "Runtime evidence is blocked because parsed Java comparison keys are missing or invalid.";

		if (keyProjectionReport.NeedsCSharpKeys)
			return "Runtime evidence is blocked because C# comparison keys are missing or invalid.";

		if (keyProjectionReport.NeedsKeyAlignment)
			return "Runtime evidence is blocked because projected Java and C# comparison keys do not align.";

		if (keyProjectionReport.NeedsComparisonExecution)
			return "Runtime evidence is blocked because key projection has not executed deterministic Java/C# runtime comparison.";

		return "Runtime comparison key projection has no current blockers, but verified parity still requires objective comparison evidence.";
	}

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus GetGeneratedArtifactExecutionPlanStatus(
		PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport generatedArtifactExecutionPlan)
	{
		if (generatedArtifactExecutionPlan.NeedsJavaTooling)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite;

		if (generatedArtifactExecutionPlan.NeedsJavaArtifacts)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact;

		if (generatedArtifactExecutionPlan.NeedsCSharpEmitter)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpImplementation;

		if (generatedArtifactExecutionPlan.NeedsRuntimeEvidence)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingRuntimeEvidence;

		if (generatedArtifactExecutionPlan.NeedsComparisonExecution)
			return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedComparisonNotExecuted;

		return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata;
	}

	private static string GetGeneratedArtifactExecutionPlanNotes(
		PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport generatedArtifactExecutionPlan)
	{
		if (generatedArtifactExecutionPlan.NeedsJavaTooling)
			return "Execution plan is blocked before Java observer/artifact work because local Maven/Java tooling readiness has not been proven.";

		if (generatedArtifactExecutionPlan.NeedsJavaArtifacts)
			return "Execution plan is blocked because Java instrumentation, trace serialization, or generated runtime artifacts are missing.";

		if (generatedArtifactExecutionPlan.NeedsCSharpEmitter)
			return "Execution plan is blocked because live C# trace emitter implementation is missing.";

		if (generatedArtifactExecutionPlan.NeedsRuntimeEvidence)
			return "Execution plan is blocked because generated Java artifacts and live C# trace rows have not both been captured.";

		if (generatedArtifactExecutionPlan.NeedsComparisonExecution)
			return "Execution plan is blocked because deterministic Java/C# runtime comparison has not executed.";

		return "Execution plan currently has no blocking gates, but verified parity still requires objective Java/C# runtime comparison evidence.";
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker blocker,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus status,
		bool blocks,
		string javaSource,
		string csharpTarget,
		string evidence,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow(
			rows.Count + 1,
			blocker,
			status,
			blocks,
			javaSource,
			csharpTarget,
			evidence,
			notes));
	}
}
