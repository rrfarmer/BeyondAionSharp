using System.Text.Json;

namespace Aion.GameServer.Services;

public static class FindGroupMutationPostJavaTraceArtifactValidatorService
{
	public static FindGroupMutationPostJavaTraceArtifactValidationReport Validate(string json)
	{
		var issues = new List<FindGroupMutationPostJavaTraceArtifactValidationIssue>();
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json);
		}
		catch (JsonException ex)
		{
			return CreateReport(
				[new FindGroupMutationPostJavaTraceArtifactValidationIssue(FindGroupMutationPostJavaTraceArtifactValidationIssueCode.InvalidJson, "$", ex.Message)]);
		}

		using (document)
		{
			var root = document.RootElement;
			var schemaReport = FindGroupMutationPostJavaTraceArtifactSchemaReportService.Create();
			var schemaVersion = GetInt(root, "schemaVersion");
			var traceName = GetString(root, "traceName");

			if (schemaVersion != schemaReport.SchemaVersion)
			{
				Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnsupportedSchemaVersion, "$.schemaVersion", $"Expected schemaVersion {schemaReport.SchemaVersion}.");
			}

			if (!string.Equals(traceName, schemaReport.TraceName, StringComparison.Ordinal))
			{
				Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnexpectedTraceName, "$.traceName", $"Expected traceName '{schemaReport.TraceName}'.");
			}

			if (!root.TryGetProperty("traces", out var traces) || traces.ValueKind != JsonValueKind.Array || traces.GetArrayLength() == 0)
			{
				Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.MissingTraceRows, "$.traces", "At least one mutation-post trace row is required.");
				return CreateReport(issues);
			}

			var rows = new List<FindGroupMutationPostJavaTraceArtifactValidationTraceRow>();
			var index = 0;
			foreach (var trace in traces.EnumerateArray())
			{
				var path = $"$.traces[{index}]";
				ValidateRequiredFields(trace, schemaReport, issues, path);
				ValidateJavaRow(trace, schemaReport, issues, path);
				rows.Add(ParseRow(trace));
				index++;
			}

			return CreateReport(
				issues,
				new FindGroupMutationPostJavaTraceArtifactMetadata(
					schemaVersion ?? 0,
					traceName ?? string.Empty,
					rows));
		}
	}

	private static void ValidateRequiredFields(
		JsonElement trace,
		FindGroupMutationPostJavaTraceArtifactSchemaReport schemaReport,
		ICollection<FindGroupMutationPostJavaTraceArtifactValidationIssue> issues,
		string path)
	{
		foreach (var field in schemaReport.Fields)
		{
			if (!trace.TryGetProperty(field.Name, out var value))
			{
				Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.MissingField, $"{path}.{field.Name}", "Required schema-v1 mutation-post field is missing.");
				continue;
			}

			if (!MatchesFieldType(value, field.FieldType))
			{
				Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.InvalidFieldType, $"{path}.{field.Name}", $"Expected {field.FieldType}.");
			}
		}
	}

	private static void ValidateJavaRow(
		JsonElement trace,
		FindGroupMutationPostJavaTraceArtifactSchemaReport schemaReport,
		ICollection<FindGroupMutationPostJavaTraceArtifactValidationIssue> issues,
		string path)
	{
		if (!string.Equals(GetString(trace, "traceSource"), "Java", StringComparison.Ordinal))
		{
			Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnexpectedTraceSource, $"{path}.traceSource", "Java trace artifacts must use traceSource 'Java'.");
		}

		var action = GetInt(trace, "action");
		var actionRow = schemaReport.Actions.SingleOrDefault(row => row.Action == action);
		if (actionRow == null)
		{
			Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnsupportedAction, $"{path}.action", "Only mutation-post actions 2 and 6 are supported.");
			return;
		}

		if (!string.Equals(GetString(trace, "mutationKind"), actionRow.MutationKind.ToString(), StringComparison.Ordinal)
			|| GetInt(trace, "postedSystemMessageId") != actionRow.PostedSystemMessageId
			|| GetInt(trace, "refreshedListAction") != actionRow.RefreshedShowListAction)
		{
			Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.ActionMappingMismatch, path, "Action row does not match the Java mutation kind, posted message id, and refreshed show-list action mapping.");
		}

		if (!string.Equals(GetString(trace, "postedSystemMessageType"), "SmSystemMessage", StringComparison.Ordinal)
			|| !string.Equals(GetString(trace, "refreshedListPacketType"), "SmFindGroup", StringComparison.Ordinal))
		{
			Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.ActionMappingMismatch, path, "Action row must use SmSystemMessage followed by SmFindGroup.");
		}

		if (GetInt(trace, "worldBroadcastCount") != 0 || GetInt(trace, "inviteDispatchCount") != 0)
		{
			Add(issues, FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnexpectedSideEffectCount, path, "Mutation-post direct traces must keep worldBroadcastCount and inviteDispatchCount at 0.");
		}
	}

	private static bool MatchesFieldType(JsonElement value, string fieldType)
	{
		return fieldType switch
		{
			"string" => value.ValueKind == JsonValueKind.String,
			"boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
			"integer array" => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out _)),
			_ => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
		};
	}

	private static FindGroupMutationPostJavaTraceArtifactValidationTraceRow ParseRow(JsonElement trace)
	{
		return new FindGroupMutationPostJavaTraceArtifactValidationTraceRow(
			GetInt(trace, "schemaVersion") ?? 0,
			GetString(trace, "traceName") ?? string.Empty,
			GetString(trace, "traceSource") ?? string.Empty,
			GetInt(trace, "action") ?? 0,
			GetString(trace, "mutationKind") ?? string.Empty,
			GetInt(trace, "postedSystemMessageId") ?? 0,
			GetInt(trace, "refreshedListAction") ?? 0);
	}

	private static int? GetInt(JsonElement element, string propertyName)
	{
		return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
			? value
			: null;
	}

	private static string? GetString(JsonElement element, string propertyName)
	{
		return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
			? property.GetString()
			: null;
	}

	private static void Add(
		ICollection<FindGroupMutationPostJavaTraceArtifactValidationIssue> issues,
		FindGroupMutationPostJavaTraceArtifactValidationIssueCode code,
		string path,
		string message)
	{
		issues.Add(new FindGroupMutationPostJavaTraceArtifactValidationIssue(code, path, message));
	}

	private static FindGroupMutationPostJavaTraceArtifactValidationReport CreateReport(
		IReadOnlyList<FindGroupMutationPostJavaTraceArtifactValidationIssue> issues,
		FindGroupMutationPostJavaTraceArtifactMetadata? metadata = null)
	{
		return new FindGroupMutationPostJavaTraceArtifactValidationReport(issues, issues.Count == 0, metadata);
	}
}

public enum FindGroupMutationPostJavaTraceArtifactValidationIssueCode
{
	InvalidJson,
	UnsupportedSchemaVersion,
	UnexpectedTraceName,
	MissingTraceRows,
	MissingField,
	InvalidFieldType,
	UnexpectedTraceSource,
	UnsupportedAction,
	ActionMappingMismatch,
	UnexpectedSideEffectCount,
}

public sealed record FindGroupMutationPostJavaTraceArtifactValidationIssue(
	FindGroupMutationPostJavaTraceArtifactValidationIssueCode Code,
	string Path,
	string Message);

public sealed record FindGroupMutationPostJavaTraceArtifactValidationTraceRow(
	int SchemaVersion,
	string TraceName,
	string TraceSource,
	int Action,
	string MutationKind,
	int PostedSystemMessageId,
	int RefreshedListAction);

public sealed record FindGroupMutationPostJavaTraceArtifactMetadata(
	int SchemaVersion,
	string TraceName,
	IReadOnlyList<FindGroupMutationPostJavaTraceArtifactValidationTraceRow> TraceRows);

public sealed record FindGroupMutationPostJavaTraceArtifactValidationReport(
	IReadOnlyList<FindGroupMutationPostJavaTraceArtifactValidationIssue> Issues,
	bool IsValid,
	FindGroupMutationPostJavaTraceArtifactMetadata? Metadata);
