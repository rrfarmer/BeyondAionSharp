namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker
{
	RuntimeComparisonDesign,
	TraceArtifactSchema,
	JavaInstrumentation,
	JavaTraceSerializer,
	GeneratedJavaTraceArtifacts,
	CSharpArtifactReader,
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
	bool NeedsJavaInstrumentation,
	bool NeedsJavaTraceSerializer,
	bool NeedsGeneratedJavaTraceArtifacts,
	bool NeedsCSharpArtifactReader,
	bool NeedsLiveCSharpPacketHooks,
	bool NeedsCSharpRuntimeTraceOutput,
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
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport? comparisonContract = null)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow>();

		AddRuntimeComparisonDesign(rows, runtimeDesign);
		AddTraceArtifactSchema(rows, traceSchema);
		AddJavaInstrumentation(rows, traceSchema);
		AddJavaTraceSerializer(rows, traceSchema);
		AddGeneratedJavaTraceArtifacts(rows, traceSchema, artifactDirectoryReport);
		AddCSharpArtifactReader(rows, traceSchema);
		AddLiveCSharpPacketHooks(rows, runtimeDesign);
		AddRuntimeComparisonEvidence(rows, comparisonContract);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReport(
			rowArray,
			HasRuntimeComparisonDesign: runtimeDesign != null,
			HasTraceArtifactSchema: traceSchema != null,
			HasGeneratedJavaTraceArtifactDirectoryReport: artifactDirectoryReport != null,
			HasShapeValidGeneratedJavaTraceArtifacts: artifactDirectoryReport?.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
			HasRuntimeComparisonContractReport: comparisonContract != null,
			NeedsJavaInstrumentation: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.JavaInstrumentation && row.BlocksRuntimeComparison),
			NeedsJavaTraceSerializer: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.JavaTraceSerializer && row.BlocksRuntimeComparison),
			NeedsGeneratedJavaTraceArtifacts: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedJavaTraceArtifacts && row.BlocksRuntimeComparison),
			NeedsCSharpArtifactReader: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpArtifactReader && row.BlocksRuntimeComparison),
			NeedsLiveCSharpPacketHooks: rowArray.Any(row => row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.LiveCSharpPacketHooks && row.BlocksRuntimeComparison),
			NeedsCSharpRuntimeTraceOutput: comparisonContract?.NeedsCSharpRuntimeTrace == true,
			NeedsRuntimeComparisonExecution: comparisonContract?.NeedsExecutedComparison == true,
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

	private static void AddRuntimeComparisonEvidence(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport? comparisonContract)
	{
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
