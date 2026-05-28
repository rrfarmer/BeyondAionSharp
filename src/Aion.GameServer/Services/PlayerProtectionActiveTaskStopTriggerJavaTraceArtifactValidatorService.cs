using System.Text.Json;

namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode
{
	InvalidJson,
	MissingTopLevelField,
	UnsupportedSchemaVersion,
	MissingTraceRows,
	MissingNestedPayloadField,
	OutOfOrderEventSequence,
	UnknownPhase,
	UnknownReturnReason,
	TimestampMarkedAsParityKey,
}

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue(
	PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode Code,
	string Path,
	string Message);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactPlayerSnapshot(
	int? ObjectId,
	bool? Spawned,
	bool? Flying,
	bool? Dead,
	bool? ProtectionActiveBefore,
	bool? ProtectionActiveAfter,
	IReadOnlyList<string> VisualStateBefore,
	IReadOnlyList<string> VisualStateAfter);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactTraceRow(
	int? EventSeq,
	string Phase,
	string PacketName,
	string ReturnReason,
	bool? StopCalled,
	bool? ExpectsStopProtectionCall,
	bool? TimestampIsParityKey,
	PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactPlayerSnapshot Player);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactMetadata(
	int SchemaVersion,
	string JavaCommit,
	string Scenario,
	string RuntimePacketName,
	string RuntimeExpectedReturnReason,
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactTraceRow> TraceRows);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> Issues,
	bool IsValidSchemaV1,
	bool ReadyForRuntimeComparison,
	string Notes,
	PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactMetadata? Metadata);

/// <summary>
/// Java parity breadcrumb: guarded schema-v1 validator for future generated Java trace artifacts
/// representing PlayerController protection start/stop packet callers. This validates artifact shape only;
/// it does not execute Java or compare C# runtime behavior.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService
{
	public static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport Validate(string json)
	{
		var issues = new List<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue>();
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json);
		}
		catch (JsonException ex)
		{
			Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.InvalidJson, "$", ex.Message);
			return CreateReport(issues);
		}

		using (document)
		{
			var root = document.RootElement;
			Require(root, issues, "schemaVersion");
			Require(root, issues, "javaCommit");
			Require(root, issues, "scenario");
			Require(root, issues, "runtimeFacts");
			Require(root, issues, "javaSources");
			Require(root, issues, "traces");
			Require(root, issues, "notes");

			if (root.TryGetProperty("schemaVersion", out var schemaVersion)
				&& (!schemaVersion.TryGetInt32(out var version) || version != 1))
			{
				Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.UnsupportedSchemaVersion, "$.schemaVersion", "Expected schemaVersion 1.");
			}

			ValidateRuntimeFacts(root, issues);
			ValidateTraces(root, issues);

			return CreateReport(issues, issues.Count == 0 ? ParseMetadata(root) : null);
		}
	}

	private static void ValidateRuntimeFacts(
		JsonElement root,
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues)
	{
		if (!root.TryGetProperty("runtimeFacts", out var runtimeFacts) || runtimeFacts.ValueKind != JsonValueKind.Object)
			return;

		Require(runtimeFacts, issues, "packetName", "$.runtimeFacts");
		Require(runtimeFacts, issues, "playerObjectId", "$.runtimeFacts");
		Require(runtimeFacts, issues, "worldId", "$.runtimeFacts");
		Require(runtimeFacts, issues, "expectedReturnReason", "$.runtimeFacts");

		if (runtimeFacts.TryGetProperty("expectedReturnReason", out var reason)
			&& reason.ValueKind == JsonValueKind.String
			&& !KnownReturnReasons.Contains(reason.GetString() ?? string.Empty))
		{
			Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.UnknownReturnReason, "$.runtimeFacts.expectedReturnReason", $"Unknown return reason '{reason.GetString()}'.");
		}
	}

	private static void ValidateTraces(
		JsonElement root,
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues)
	{
		if (!root.TryGetProperty("traces", out var traces) || traces.ValueKind != JsonValueKind.Array)
			return;

		if (traces.GetArrayLength() == 0)
		{
			Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingTraceRows, "$.traces", "At least one trace row is required.");
			return;
		}

		var previousEventSeq = -1;
		var index = 0;
		foreach (var trace in traces.EnumerateArray())
		{
			var path = $"$.traces[{index}]";
			Require(trace, issues, "eventSeq", path);
			Require(trace, issues, "phase", path);
			Require(trace, issues, "returnReason", path);
			Require(trace, issues, "timestampIsParityKey", path);
			Require(trace, issues, "javaSourceFile", path);
			Require(trace, issues, "javaLine", path);
			Require(trace, issues, "player", path);

			if (trace.TryGetProperty("eventSeq", out var eventSeqElement) && eventSeqElement.TryGetInt32(out var eventSeq))
			{
				if (eventSeq <= previousEventSeq)
					Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.OutOfOrderEventSequence, $"{path}.eventSeq", "Trace eventSeq values must be strictly increasing.");
				previousEventSeq = eventSeq;
			}

			if (trace.TryGetProperty("phase", out var phase)
				&& phase.ValueKind == JsonValueKind.String
				&& !KnownPhases.Contains(phase.GetString() ?? string.Empty))
			{
				Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.UnknownPhase, $"{path}.phase", $"Unknown phase '{phase.GetString()}'.");
			}

			if (trace.TryGetProperty("returnReason", out var returnReason)
				&& returnReason.ValueKind == JsonValueKind.String
				&& !KnownReturnReasons.Contains(returnReason.GetString() ?? string.Empty))
			{
				Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.UnknownReturnReason, $"{path}.returnReason", $"Unknown return reason '{returnReason.GetString()}'.");
			}

			if (trace.TryGetProperty("timestampIsParityKey", out var timestampIsParityKey)
				&& timestampIsParityKey.ValueKind == JsonValueKind.True)
			{
				Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.TimestampMarkedAsParityKey, $"{path}.timestampIsParityKey", "Timestamps are diagnostics only and must not be parity keys.");
			}

			ValidatePlayerSnapshot(trace, issues, path);
			ValidateSchedulerPayload(trace, issues, path);
			ValidateTaskCancellationPayload(trace, issues, path);

			index++;
		}
	}

	private static void ValidatePlayerSnapshot(
		JsonElement trace,
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues,
		string tracePath)
	{
		if (!trace.TryGetProperty("player", out var player) || player.ValueKind != JsonValueKind.Object)
			return;

		RequireNested(player, issues, "objectId", $"{tracePath}.player");
		RequireNested(player, issues, "spawned", $"{tracePath}.player");
		RequireNested(player, issues, "flying", $"{tracePath}.player");
		RequireNested(player, issues, "dead", $"{tracePath}.player");
		RequireNested(player, issues, "protectionActiveBefore", $"{tracePath}.player");
		RequireNested(player, issues, "protectionActiveAfter", $"{tracePath}.player");
		RequireNested(player, issues, "visualStateBefore", $"{tracePath}.player");
		RequireNested(player, issues, "visualStateAfter", $"{tracePath}.player");
	}

	private static void ValidateSchedulerPayload(
		JsonElement trace,
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues,
		string tracePath)
	{
		if (!trace.TryGetProperty("scheduler", out var scheduler) || scheduler.ValueKind != JsonValueKind.Object)
			return;

		RequireNested(scheduler, issues, "delayMillis", $"{tracePath}.scheduler");
		RequireNested(scheduler, issues, "timeUnit", $"{tracePath}.scheduler");
		RequireNested(scheduler, issues, "runnableWrapperApplied", $"{tracePath}.scheduler");
		RequireNested(scheduler, issues, "callbackMethod", $"{tracePath}.scheduler");
		RequireNested(scheduler, issues, "oldFuturePresent", $"{tracePath}.scheduler");
		RequireNested(scheduler, issues, "oldFutureCancelArgument", $"{tracePath}.scheduler");
		RequireNested(scheduler, issues, "oldFutureCancelResult", $"{tracePath}.scheduler");
		RequireNested(scheduler, issues, "newFutureStored", $"{tracePath}.scheduler");
	}

	private static void ValidateTaskCancellationPayload(
		JsonElement trace,
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues,
		string tracePath)
	{
		if (!trace.TryGetProperty("taskCancellation", out var taskCancellation) || taskCancellation.ValueKind != JsonValueKind.Object)
			return;

		RequireNested(taskCancellation, issues, "taskIdName", $"{tracePath}.taskCancellation");
		RequireNested(taskCancellation, issues, "taskIdOrdinal", $"{tracePath}.taskCancellation");
		RequireNested(taskCancellation, issues, "taskPresentBeforeCancel", $"{tracePath}.taskCancellation");
		RequireNested(taskCancellation, issues, "taskRemovedBeforeCancel", $"{tracePath}.taskCancellation");
		RequireNested(taskCancellation, issues, "futureCancelArgument", $"{tracePath}.taskCancellation");
		RequireNested(taskCancellation, issues, "futureCancelResult", $"{tracePath}.taskCancellation");
		RequireNested(taskCancellation, issues, "scheduledDelayMillis", $"{tracePath}.taskCancellation");
		RequireNested(taskCancellation, issues, "stopOrigin", $"{tracePath}.taskCancellation");
	}

	private static void Require(
		JsonElement element,
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues,
		string propertyName,
		string path = "$")
	{
		if (!element.TryGetProperty(propertyName, out _))
			Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingTopLevelField, $"{path}.{propertyName}", "Required schema-v1 field is missing.");
	}

	private static void RequireNested(
		JsonElement element,
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues,
		string propertyName,
		string path)
	{
		if (!element.TryGetProperty(propertyName, out _))
			Add(issues, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField, $"{path}.{propertyName}", "Required schema-v1 nested payload field is missing.");
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode code,
		string path,
		string message)
	{
		issues.Add(new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue(code, path, message));
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport CreateReport(
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactMetadata? metadata = null) =>
		new(
			issues,
			IsValidSchemaV1: issues.Count == 0,
			ReadyForRuntimeComparison: false,
			"Validation covers JSON schema shape only; generated Java artifacts and C# runtime comparison remain required.",
			metadata);

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactMetadata ParseMetadata(JsonElement root)
	{
		var runtimeFacts = root.GetProperty("runtimeFacts");
		return new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactMetadata(
			SchemaVersion: GetInt(root, "schemaVersion") ?? 1,
			JavaCommit: GetString(root, "javaCommit"),
			Scenario: GetString(root, "scenario"),
			RuntimePacketName: GetString(runtimeFacts, "packetName"),
			RuntimeExpectedReturnReason: GetString(runtimeFacts, "expectedReturnReason"),
			TraceRows: root.GetProperty("traces")
				.EnumerateArray()
				.Select(ParseTraceRow)
				.ToArray());
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactTraceRow ParseTraceRow(JsonElement trace)
	{
		return new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactTraceRow(
			EventSeq: GetInt(trace, "eventSeq"),
			Phase: GetString(trace, "phase"),
			PacketName: GetString(trace, "packetName"),
			ReturnReason: GetString(trace, "returnReason"),
			StopCalled: GetBool(trace, "stopCalled"),
			ExpectsStopProtectionCall: GetBool(trace, "expectsStopProtectionCall"),
			TimestampIsParityKey: GetBool(trace, "timestampIsParityKey"),
			Player: trace.TryGetProperty("player", out var player) && player.ValueKind == JsonValueKind.Object
				? ParsePlayerSnapshot(player)
				: EmptyPlayerSnapshot);
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactPlayerSnapshot ParsePlayerSnapshot(JsonElement player)
	{
		return new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactPlayerSnapshot(
			ObjectId: GetInt(player, "objectId"),
			Spawned: GetBool(player, "spawned"),
			Flying: GetBool(player, "flying"),
			Dead: GetBool(player, "dead"),
			ProtectionActiveBefore: GetBool(player, "protectionActiveBefore"),
			ProtectionActiveAfter: GetBool(player, "protectionActiveAfter"),
			VisualStateBefore: GetStringArray(player, "visualStateBefore"),
			VisualStateAfter: GetStringArray(player, "visualStateAfter"));
	}

	private static string GetString(JsonElement element, string propertyName) =>
		element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	private static int? GetInt(JsonElement element, string propertyName) =>
		element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
			? result
			: null;

	private static bool? GetBool(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var value))
			return null;

		return value.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => null,
		};
	}

	private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
			return [];

		return value
			.EnumerateArray()
			.Where(item => item.ValueKind == JsonValueKind.String)
			.Select(item => item.GetString() ?? string.Empty)
			.ToArray();
	}

	private static readonly PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactPlayerSnapshot EmptyPlayerSnapshot = new(
		ObjectId: null,
		Spawned: null,
		Flying: null,
		Dead: null,
		ProtectionActiveBefore: null,
		ProtectionActiveAfter: null,
		VisualStateBefore: [],
		VisualStateAfter: []);

	private static readonly ISet<string> KnownPhases = new HashSet<string>(StringComparer.Ordinal)
	{
		"animation_done_enter",
		"caller_enter",
		"completed_runnable_task_noop",
		"exception_fallback_player_info_packet",
		"exception_fallback_world_spawn",
		"exception_logged",
		"exception_spawned_guard_noop",
		"fallback_guard",
		"fallback_player_info_packet",
		"fallback_world_spawn",
		"guard_return",
		"missing_teleport_task_noop",
		"non_runnable_task_noop",
		"packet_enter",
		"packet_exit",
		"pet_position_set",
		"pet_spawn_completed",
		"protection_start_skip",
		"same_map_spawn_packets",
		"spawn_task_get_exception",
		"spawn_task_run",
		"teleport_task_remove",
		"world_position_set",
		"world_spawn_completed",
	};

	private static readonly ISet<string> KnownReturnReasons = new HashSet<string>(
		Enum.GetNames<PlayerProtectionActiveTaskStopTriggerTraceArtifactPacketReturnReason>()
			.Select(ToSnakeCase)
			.Concat([
				"animation_done_no_pending_runnable_teleport_task",
				"animation_done_spawn_task_exception_fallback",
				"animation_done_spawn_task_exception_fallback_spawned_guard",
				"beritra_animation_done_same_map_spawn_after_protection_start",
				"delayed_teleport_missing_instance_fallback_spawn",
			]),
		StringComparer.Ordinal);

	private static string ToSnakeCase(string value)
	{
		var chars = new List<char>(value.Length * 2);
		for (var i = 0; i < value.Length; i++)
		{
			var c = value[i];
			if (char.IsUpper(c) && i > 0)
				chars.Add('_');
			chars.Add(char.ToLowerInvariant(c));
		}

		return new string(chars.ToArray());
	}
}
