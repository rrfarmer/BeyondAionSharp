using System.Text.Json;

namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode
{
	InvalidJson,
	MissingTopLevelField,
	UnsupportedSchemaVersion,
	MissingTraceRows,
	OutOfOrderEventSequence,
	UnknownPhase,
	UnknownReturnReason,
	TimestampMarkedAsParityKey,
}

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue(
	PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode Code,
	string Path,
	string Message);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> Issues,
	bool IsValidSchemaV1,
	bool ReadyForRuntimeComparison,
	string Notes);

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
		}

		return CreateReport(issues);
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

			index++;
		}
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

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode code,
		string path,
		string message)
	{
		issues.Add(new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue(code, path, message));
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport CreateReport(
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue> issues) =>
		new(
			issues,
			IsValidSchemaV1: issues.Count == 0,
			ReadyForRuntimeComparison: false,
			"Validation covers JSON schema shape only; generated Java artifacts and C# runtime comparison remain required.");

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
