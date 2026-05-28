namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope
{
	TopLevel,
	RuntimeFacts,
	TraceRow,
	PlayerSnapshot,
	NestedPayload,
}

public enum PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus
{
	RequiredSchemaV1,
	OptionalSchemaV1,
	BlockedUntilJavaSerializer,
	DiagnosticOnly,
}

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope Scope,
	string JsonPath,
	string FieldName,
	PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus Status,
	PlayerProtectionActiveTaskStopTriggerTraceArtifactField? SourceSchemaField,
	string RequiredFor,
	string SerializationRule,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow> Rows,
	int SourceSchemaFieldCount,
	bool HasTopLevelContract,
	bool HasRuntimeFactsContract,
	bool HasTraceRowContract,
	bool HasPlayerSnapshotContract,
	bool HasTimestampNonParityPolicy,
	bool HasNestedPayloadPlaceholders,
	bool HasActionBranchNameTraceContract,
	bool HasEmotionPayloadContract,
	bool HasActionPayloadContract,
	bool HasCallerOriginPayloadContract,
	bool RequiresJavaSerializerImplementation,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: schema-v1 JSON field contract for future protection stop-trigger Java trace serialization.
/// This is design metadata only; it does not write artifacts, run Java, or compare C# runtime traces.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractService
{
	public static PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport Create(
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport traceSchema)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow>();

		AddTopLevelRows(rows);
		AddRuntimeFactsRows(rows);
		AddTraceRows(rows);
		AddPlayerSnapshotRows(rows);
		AddNestedPayloadRows(rows);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport(
			rowArray,
			SourceSchemaFieldCount: traceSchema.Fields.Count,
			HasTopLevelContract: HasScope(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel),
			HasRuntimeFactsContract: HasScope(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.RuntimeFacts),
			HasTraceRowContract: HasScope(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow),
			HasPlayerSnapshotContract: HasScope(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot),
			HasTimestampNonParityPolicy: rowArray.Any(row =>
				row.FieldName == "timestampIsParityKey"
				&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.DiagnosticOnly
				&& row.SerializationRule.Contains("false", StringComparison.Ordinal)),
			HasNestedPayloadPlaceholders: rowArray.Any(row => row.Scope == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload),
			HasActionBranchNameTraceContract: HasJsonPath(rowArray, "$.traces[*].actionBranchName"),
			HasEmotionPayloadContract: HasJsonPath(rowArray, "$.traces[*].emotion"),
			HasActionPayloadContract: HasJsonPath(rowArray, "$.traces[*].actionPayload"),
			HasCallerOriginPayloadContract: HasJsonPath(rowArray, "$.traces[*].callerOrigin"),
			RequiresJavaSerializerImplementation: true,
			ReadyForRuntimeComparison: false,
			$"Schema-v1 serializer field contract; traceSchemaFields={traceSchema.Fields.Count}; requiresTraceSerializer={traceSchema.RequiresTraceSerializer}",
			IsLive: false);
	}

	private static void AddTopLevelRows(ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow> rows)
	{
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel, "$.schemaVersion", "schemaVersion", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TraceSchemaVersion, "artifact compatibility", "integer literal 1", "Validator rejects unsupported schema versions.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel, "$.javaCommit", "javaCommit", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "artifact provenance", "short or full Java commit string", "Required before comparing artifacts across source revisions.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel, "$.scenario", "scenario", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "artifact identity", "stable lowercase scenario name", "Used by directory and key-projection reports.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel, "$.runtimeFacts", "runtimeFacts", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "packet-level comparison keys", "object", "Contains packet, player, world, and expected return-reason keys.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel, "$.javaSources", "javaSources", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "source breadcrumb list", "string array", "Must list Java hook sites used by the artifact.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel, "$.traces", "traces", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "ordered runtime observations", "non-empty array", "Trace rows must have strictly increasing eventSeq values.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel, "$.notes", "notes", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "human audit notes", "string array", "Must not be used as parity keys.");
	}

	private static void AddRuntimeFactsRows(ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow> rows)
	{
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.RuntimeFacts, "$.runtimeFacts.serverFlavor", "serverFlavor", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ServerFlavor, "artifact source identity", "string literal java", "Prevents mixing Java and future C# artifacts.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.RuntimeFacts, "$.runtimeFacts.packetName", "packetName", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PacketName, "scenario key", "Java packet class simple name", "Example: CM_TELEPORT_ANIMATION_DONE.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.RuntimeFacts, "$.runtimeFacts.playerObjectId", "playerObjectId", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerObjectId, "scenario key", "integer", "Correlates packet, controller, fanout, and AI observations.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.RuntimeFacts, "$.runtimeFacts.worldId", "worldId", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.WorldId, "world context", "integer or null", "Allows teleport and movement scenarios to remain comparable.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.RuntimeFacts, "$.runtimeFacts.expectedReturnReason", "expectedReturnReason", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "scenario key", "known return-reason string", "Validator rejects unknown return reasons.");
	}

	private static void AddTraceRows(ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow> rows)
	{
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].schemaVersion", "schemaVersion", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TraceSchemaVersion, "row compatibility", "integer literal 1", "Duplicates top-level schema for standalone row inspection.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].traceId", "traceId", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TraceId, "row correlation", "stable string", "Correlates all rows for one packet execution.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].eventSeq", "eventSeq", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.EventSeq, "deterministic ordering", "strictly increasing integer", "Primary ordering key; do not rely on timestamps.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].phase", "phase", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.Phase, "branch comparison", "known phase string", "Validator rejects unknown phases.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].packetName", "packetName", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PacketName, "row packet identity", "Java packet class simple name", "Needed when one artifact contains multiple packet-origin rows.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].returnReason", "returnReason", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "branch result", "known return-reason string", "Validator rejects unknown return reasons.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].actionBranchName", "actionBranchName", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ActionBranchName, "packet branch identity", "stable string; no allow-list until generated Java artifacts exist", "Required on every trace row so packet-specific no-stop/stop branches do not collapse into generic return reasons.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].stopCalled", "stopCalled", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "stop-call parity", "boolean", "Records whether the row observed a stopProtectionActiveTask call.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].expectsStopProtectionCall", "expectsStopProtectionCall", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "source-reviewed expectation", "boolean", "Separates expected no-stop branches from missing instrumentation.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].wallTimeEpochMillis", "wallTimeEpochMillis", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.DiagnosticOnly, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.WallTimeEpochMillis, "diagnostics", "integer; never a parity key", "Date/time handling is explicitly non-parity evidence.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].monotonicNanos", "monotonicNanos", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.DiagnosticOnly, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TimestampNanos, "diagnostics", "integer; never a parity key", "Use eventSeq for ordering instead.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].timestampIsParityKey", "timestampIsParityKey", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.DiagnosticOnly, null, "timestamp policy", "must be false", "Validator rejects true because Java/C# clocks are not parity evidence.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].javaSourceFile", "javaSourceFile", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.JavaSourceFile, "source breadcrumb", "Java file name", "Keeps generated rows traceable to Java source.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].javaLine", "javaLine", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.JavaLine, "source breadcrumb", "integer", "Line numbers may drift but still help audit generated artifacts.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow, "$.traces[*].player", "player", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, null, "player snapshot", "object", "Nested player snapshot is required on every trace row.");
	}

	private static void AddPlayerSnapshotRows(ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow> rows)
	{
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot, "$.traces[*].player.objectId", "objectId", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerObjectId, "player identity", "integer", "Must match runtimeFacts.playerObjectId for single-player scenarios.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot, "$.traces[*].player.spawned", "spawned", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerSpawned, "spawned gating", "boolean", "Required for stopProtectionActiveTask spawned-gated fanout.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot, "$.traces[*].player.flying", "flying", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerFlying, "movement branch comparison", "boolean", "Required for CM_MOVE_IN_AIR branches.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot, "$.traces[*].player.dead", "dead", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.PlayerDead, "guard comparison", "boolean", "Required for dead-player guard and teleport fallback branches.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot, "$.traces[*].player.protectionActiveBefore", "protectionActiveBefore", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ProtectionActiveBefore, "protection state comparison", "boolean", "Captures BLINKING-derived protection state before the phase.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot, "$.traces[*].player.protectionActiveAfter", "protectionActiveAfter", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ProtectionActiveAfter, "protection state comparison", "boolean", "Captures state after task cancel, visual mutation, or no-op branch.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot, "$.traces[*].player.visualStateBefore", "visualStateBefore", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.VisualStateBefore, "visual-state comparison", "string array", "Must preserve BLINKING exactly as an enum/string token.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot, "$.traces[*].player.visualStateAfter", "visualStateAfter", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1, PlayerProtectionActiveTaskStopTriggerTraceArtifactField.VisualStateAfter, "visual-state comparison", "string array", "Must preserve BLINKING removal/no-op behavior.");
	}

	private static void AddNestedPayloadRows(ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow> rows)
	{
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload, "$.traces[*].movement", "movement", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer, null, "movement packet branches", "object or null", "Precise x/y/z and anti-hack fields are defined in the trace schema but not serialized by any Java writer yet.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload, "$.traces[*].taskCancellation", "taskCancellation", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer, null, "CreatureController task-map branches", "object or null", "Must preserve remove-before-cancel, Future.cancel(false), and threading caveats once implemented.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload, "$.traces[*].fanout", "fanout", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer, null, "SM_PLAYER_STATE fanout", "object or null", "Needed for recipient count and include-self behavior.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload, "$.traces[*].aiNotify", "aiNotify", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer, null, "AI move notification", "object or null", "Needed before AI notification parity can be claimed.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload, "$.traces[*].scheduler", "scheduler", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer, null, "ThreadPoolManager and RunnableFuture branches", "object or null", "Needed for delayed stop and teleport animation-done RunnableFuture behavior.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload, "$.traces[*].emotion", "emotion", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer, null, "CM_EMOTION branches", "object or null; when object include emotionType, emotionId, emotionStance, emotionCanUse, emotionBroadcasted", "Must preserve CM_EMOTION cancellation/state-validation ordering, optional SM_EMOTION broadcast evidence, and late stop behavior once implemented.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload, "$.traces[*].actionPayload", "actionPayload", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer, null, "CM_USE_ITEM and CM_COMPOSITE_STONES action branches", "object or null; when object include itemObjectId, itemLookupResult, restrictionResult, itemActionResult, compositeToolObjectId, compositeFirstObjectId, compositeSecondObjectId, compositeCanActResult", "Must preserve item lookup/restriction/action dispatch and composite canAct decisions without executing Java item behavior in the serializer.");
		Add(rows, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload, "$.traces[*].callerOrigin", "callerOrigin", PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer, null, "CM_LEVEL_READY, TeleportService, BeritraPortalAI, and CM_TELEPORT_ANIMATION_DONE caller-origin branches", "object or null; when object include callerName, callerClass, callerMethod, callerSourceFile, callerLine, startProtectionLine, startsProtectionBeforeWorldSpawn, worldSpawnLine, spawnedBeforeStart, ordering", "Must preserve source-reviewed start-protection and world-spawn ordering without treating source line numbers as deterministic runtime parity keys.");
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope scope,
		string jsonPath,
		string fieldName,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus status,
		PlayerProtectionActiveTaskStopTriggerTraceArtifactField? sourceSchemaField,
		string requiredFor,
		string serializationRule,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow(
			rows.Count + 1,
			scope,
			jsonPath,
			fieldName,
			status,
			sourceSchemaField,
			requiredFor,
			serializationRule,
			notes));
	}

	private static bool HasScope(
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope scope) =>
		rows.Any(row => row.Scope == scope);

	private static bool HasJsonPath(
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractRow> rows,
		string jsonPath) =>
		rows.Any(row => row.JsonPath == jsonPath);
}
