namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate
{
	JavaToolingCheck,
	JavaObserverDesign,
	JavaInstrumentation,
	JavaTraceSerializer,
	TraceArtifactShapeValidation,
	JavaArtifactGeneration,
	CSharpEmitterDesign,
	CSharpEmitterImplementation,
	CSharpTraceCapture,
	KeyProjection,
	RuntimeComparisonExecution,
}

public enum PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus
{
	ReadyForDesignOnly,
	BlockedMissingTooling,
	BlockedMissingJavaArtifact,
	BlockedMissingCSharpImplementation,
	BlockedMissingRuntimeEvidence,
	BlockedComparisonNotExecuted,
}

public sealed record PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate Gate,
	PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus Status,
	bool BlocksRuntimeComparison,
	string JavaSource,
	string CSharpTarget,
	string Evidence,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanRow> Rows,
	bool HasJavaToolingGate,
	bool HasJavaArtifactGenerationGate,
	bool HasCSharpEmitterGate,
	bool HasKeyProjectionGate,
	bool HasComparisonExecutionGate,
	bool HasSerializerFieldContract,
	int SerializerFieldContractRowCount,
	bool HasSerializerTimestampNonParityPolicy,
	bool HasSerializerNestedPayloadPlaceholders,
	bool HasSerializerActionBranchNameTraceContract,
	bool HasSerializerEmotionPayloadContract,
	bool HasSerializerActionPayloadContract,
	bool HasSerializerCallerOriginPayloadContract,
	bool NeedsJavaSerializerImplementation,
	bool NeedsJavaTooling,
	bool NeedsJavaArtifacts,
	bool NeedsCSharpEmitter,
	bool NeedsRuntimeEvidence,
	bool NeedsComparisonExecution,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live execution plan for moving from protection stop-trigger metadata
/// to generated Java artifacts, live C# trace rows, key projection, and deterministic comparison.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanService
{
	public static PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport Create(
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport runtimeDesign,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport traceSchema,
		PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport csharpEmitterDesign,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport? serializerFieldContract = null)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanRow>();

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaToolingCheck,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingTooling,
			"local Maven/Java 25 runtime used by Java game-server",
			"developer workstation / CI tooling",
			"Java tooling has not produced protection stop-trigger runtime artifacts in this environment",
			"Verify Maven and Java 25 before attempting observer compilation or Java runtime capture.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaObserverDesign,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.ReadyForDesignOnly,
			traceSchema.JavaSource,
			"future Java observer design outside dotnetConversion",
			$"schemaPhases={traceSchema.Phases.Count}; schemaFields={traceSchema.Fields.Count}; returnReasons={traceSchema.PacketReturnReasons.Count}",
			"Observer design must remain read-only with respect to Java gameplay control flow.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaInstrumentation,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingJavaArtifact,
			"CM_MOVE/CM_MOVE_IN_AIR/action packets; PlayerController; CreatureController; TeleportService",
			"future Java observer hooks",
			$"runtimeScenarios={runtimeDesign.Rows.Count}; requiresJavaArtifacts={runtimeDesign.RequiresJavaTraceArtifacts}",
			"Java packet/controller/teleport code is not instrumented; production behavior must not be changed by tracing.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaTraceSerializer,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingJavaArtifact,
			"future schema-v1 Java artifact writer",
			"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService",
			CreateSerializerEvidence(traceSchema, serializerFieldContract),
			CreateSerializerNotes(serializerFieldContract));

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.TraceArtifactShapeValidation,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.ReadyForDesignOnly,
			"schema-v1 generated Java trace artifact contract",
			"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService / PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport",
			"validatorCoverage=actionBranchName/player/movement/scheduler/taskCancellation/fanout/aiNotify/emotion/actionPayload/callerOrigin",
			"Shape validation coverage is broad enough for artifact-reader gating, but generated Java artifacts, live C# trace rows, and deterministic comparison are still required before parity can be claimed.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaArtifactGeneration,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingJavaArtifact,
			"Java runtime packet/controller execution",
			"parity-artifacts/protection-stop-trigger/java",
			"no generated schema-v1 runtime artifacts exist for comparison",
			"Generated artifacts must cover movement, early action packets, composite invalid-after-stop, emotion late/no-stop, teleport animation, and controller race scenarios.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.CSharpEmitterDesign,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.ReadyForDesignOnly,
			csharpEmitterDesign.JavaSource,
			"PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReport",
			$"emitterRows={csharpEmitterDesign.Rows.Count}; packetHooks={csharpEmitterDesign.HasPacketHookSites}; controllerHooks={csharpEmitterDesign.HasControllerHookSites}; teleportHooks={csharpEmitterDesign.HasTeleportHookSites}",
			"C# emitter hook-site design exists but remains non-live.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.CSharpEmitterImplementation,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingCSharpImplementation,
			"future C# packet/controller/teleport execution",
			"future C# trace emitter implementation",
			$"requiresLiveEmitter={csharpEmitterDesign.RequiresLiveEmitter}",
			"Production packet/controller hooks must not be wired until generated Java artifacts and hook ordering are available.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.CSharpTraceCapture,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingRuntimeEvidence,
			"future C# runtime packet/controller execution",
			"PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport",
			"no live C# trace rows captured",
			"C# trace rows are synthetic only; live packet/controller execution is still absent.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.KeyProjection,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingRuntimeEvidence,
			"generated Java artifacts and live C# trace rows",
			"PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService",
			"key projection exists but has not consumed generated Java artifacts or live C# rows",
			"Projected keys are only meaningful after both artifact streams are generated from runtime execution.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.RuntimeComparisonExecution,
			PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedComparisonNotExecuted,
			"generated Java artifacts and live C# trace rows",
			"future deterministic runtime comparison suite",
			"no Java/C# runtime comparison executed",
			"Verified parity cannot be claimed until deterministic Java and C# runtime outputs are compared.");

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport(
			rowArray,
			HasJavaToolingGate: rowArray.Any(row => row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaToolingCheck),
			HasJavaArtifactGenerationGate: rowArray.Any(row => row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaArtifactGeneration),
			HasCSharpEmitterGate: rowArray.Any(row => row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.CSharpEmitterImplementation),
			HasKeyProjectionGate: rowArray.Any(row => row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.KeyProjection),
			HasComparisonExecutionGate: rowArray.Any(row => row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.RuntimeComparisonExecution),
			HasSerializerFieldContract: serializerFieldContract != null,
			SerializerFieldContractRowCount: serializerFieldContract?.Rows.Count ?? 0,
			HasSerializerTimestampNonParityPolicy: serializerFieldContract?.HasTimestampNonParityPolicy == true,
			HasSerializerNestedPayloadPlaceholders: serializerFieldContract?.HasNestedPayloadPlaceholders == true,
			HasSerializerActionBranchNameTraceContract: serializerFieldContract?.HasActionBranchNameTraceContract == true,
			HasSerializerEmotionPayloadContract: serializerFieldContract?.HasEmotionPayloadContract == true,
			HasSerializerActionPayloadContract: serializerFieldContract?.HasActionPayloadContract == true,
			HasSerializerCallerOriginPayloadContract: serializerFieldContract?.HasCallerOriginPayloadContract == true,
			NeedsJavaSerializerImplementation: traceSchema.RequiresTraceSerializer
				|| serializerFieldContract?.RequiresJavaSerializerImplementation == true,
			NeedsJavaTooling: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingTooling),
			NeedsJavaArtifacts: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingJavaArtifact),
			NeedsCSharpEmitter: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingCSharpImplementation),
			NeedsRuntimeEvidence: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingRuntimeEvidence),
			NeedsComparisonExecution: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedComparisonNotExecuted),
			ReadyForRuntimeComparison: false,
			"Protection stop-trigger generated artifact execution plan",
			IsLive: false);
	}

	private static string CreateSerializerEvidence(
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport traceSchema,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport? serializerFieldContract)
	{
		if (serializerFieldContract == null)
		{
			return $"schemaReady={traceSchema.ReadyForRuntimeComparison}; requiresSerializer={traceSchema.RequiresTraceSerializer}; serializerFieldContract=False";
		}

		return $"schemaReady={traceSchema.ReadyForRuntimeComparison}; requiresSerializer={traceSchema.RequiresTraceSerializer}; serializerFieldContract=True; contractRows={serializerFieldContract.Rows.Count}; timestampPolicy={serializerFieldContract.HasTimestampNonParityPolicy}; nestedPayloadPlaceholders={serializerFieldContract.HasNestedPayloadPlaceholders}; actionBranchNameContract={serializerFieldContract.HasActionBranchNameTraceContract}; emotionPayloadContract={serializerFieldContract.HasEmotionPayloadContract}; actionPayloadContract={serializerFieldContract.HasActionPayloadContract}; callerOriginPayloadContract={serializerFieldContract.HasCallerOriginPayloadContract}; requiresJavaSerializer={serializerFieldContract.RequiresJavaSerializerImplementation}";
	}

	private static string CreateSerializerNotes(
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport? serializerFieldContract)
	{
		if (serializerFieldContract == null)
		{
			return "Serializer must preserve event order, enum names, invariant numeric formatting, nulls, and timestamp non-parity semantics.";
		}

		return "Serializer field contract is present as non-live metadata; Java implementation still must write schema-v1 top-level/runtime/trace/player fields, preserve timestamp non-parity semantics, and fill blocked nested payloads before generated artifacts can prove parity.";
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanRow> rows,
		PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate gate,
		PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus status,
		string javaSource,
		string csharpTarget,
		string evidence,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanRow(
			rows.Count + 1,
			gate,
			status,
			BlocksRuntimeComparison: status != PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.ReadyForDesignOnly,
			javaSource,
			csharpTarget,
			evidence,
			notes));
	}
}
