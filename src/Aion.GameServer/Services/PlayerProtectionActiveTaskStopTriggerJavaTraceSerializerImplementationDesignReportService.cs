namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility
{
	TopLevelArtifactWriter,
	RuntimeFactsWriter,
	TraceRowCoreWriter,
	PlayerSnapshotWriter,
	NestedPayloadWriter,
	TimestampPolicyWriter,
	SourceBreadcrumbWriter,
	ArtifactFileWriter,
}

public enum PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationStatus
{
	ReadyForDesignOnly,
	BlockedMissingJavaSerializer,
}

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility Responsibility,
	PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationStatus Status,
	string JavaTarget,
	string ContractFields,
	string WriterRule,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> Rows,
	int SerializerFieldContractRowCount,
	bool HasTopLevelWriterPlan,
	bool HasRuntimeFactsWriterPlan,
	bool HasTraceRowCoreWriterPlan,
	bool HasPlayerSnapshotWriterPlan,
	bool HasNestedPayloadWriterPlan,
	bool HasTimestampPolicyWriterPlan,
	bool HasSourceBreadcrumbWriterPlan,
	bool HasArtifactFileWriterPlan,
	bool HasActionBranchNameWriterPlan,
	bool HasEmotionPayloadWriterPlan,
	bool HasActionPayloadWriterPlan,
	bool HasCallerOriginPayloadWriterPlan,
	bool RequiresJavaSerializerImplementation,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live implementation design for the future Java schema-v1 protection
/// stop-trigger trace serializer. This maps the field contract to writer responsibilities only.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignReportService
{
	public static PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignReport Create(
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow>();

		AddTopLevelArtifactWriter(rows, fieldContract);
		AddRuntimeFactsWriter(rows, fieldContract);
		AddTraceRowCoreWriter(rows, fieldContract);
		AddPlayerSnapshotWriter(rows, fieldContract);
		AddNestedPayloadWriter(rows, fieldContract);
		AddTimestampPolicyWriter(rows, fieldContract);
		AddSourceBreadcrumbWriter(rows, fieldContract);
		AddArtifactFileWriter(rows, fieldContract);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignReport(
			rowArray,
			SerializerFieldContractRowCount: fieldContract.Rows.Count,
			HasTopLevelWriterPlan: HasResponsibility(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.TopLevelArtifactWriter),
			HasRuntimeFactsWriterPlan: HasResponsibility(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.RuntimeFactsWriter),
			HasTraceRowCoreWriterPlan: HasResponsibility(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.TraceRowCoreWriter),
			HasPlayerSnapshotWriterPlan: HasResponsibility(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.PlayerSnapshotWriter),
			HasNestedPayloadWriterPlan: HasResponsibility(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.NestedPayloadWriter),
			HasTimestampPolicyWriterPlan: HasResponsibility(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.TimestampPolicyWriter),
			HasSourceBreadcrumbWriterPlan: HasResponsibility(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.SourceBreadcrumbWriter),
			HasArtifactFileWriterPlan: HasResponsibility(rowArray, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.ArtifactFileWriter),
			HasActionBranchNameWriterPlan: fieldContract.HasActionBranchNameTraceContract
				&& rowArray.Any(row => row.ContractFields.Contains("actionBranchName", StringComparison.Ordinal)),
			HasEmotionPayloadWriterPlan: fieldContract.HasEmotionPayloadContract
				&& rowArray.Any(row => row.ContractFields.Contains("emotion", StringComparison.Ordinal)),
			HasActionPayloadWriterPlan: fieldContract.HasActionPayloadContract
				&& rowArray.Any(row => row.ContractFields.Contains("actionPayload", StringComparison.Ordinal)),
			HasCallerOriginPayloadWriterPlan: fieldContract.HasCallerOriginPayloadContract
				&& rowArray.Any(row => row.ContractFields.Contains("callerOrigin", StringComparison.Ordinal)),
			RequiresJavaSerializerImplementation: true,
			ReadyForRuntimeComparison: false,
			$"Future Java schema-v1 serializer implementation design; contractRows={fieldContract.Rows.Count}; fieldContractReady={fieldContract.ReadyForRuntimeComparison}",
			IsLive: false);
	}

	private static void AddTopLevelArtifactWriter(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.TopLevelArtifactWriter,
			"future ProtectionStopTriggerTraceSerializer.writeArtifact",
			ContractFieldNames(fieldContract, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel),
			"Emit schemaVersion, javaCommit, scenario, runtimeFacts, javaSources, traces, and notes exactly once per artifact.",
			"Top-level writer must preserve schema-v1 property names and required arrays; no live Java artifact writer exists yet.");
	}

	private static void AddRuntimeFactsWriter(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.RuntimeFactsWriter,
			"future ProtectionStopTriggerTraceSerializer.writeRuntimeFacts",
			ContractFieldNames(fieldContract, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.RuntimeFacts),
			"Emit serverFlavor=java, packetName, playerObjectId, worldId, and expectedReturnReason before trace rows are compared.",
			"Runtime facts are deterministic comparison keys; missing worldId must stay null rather than omitted.");
	}

	private static void AddTraceRowCoreWriter(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.TraceRowCoreWriter,
			"future ProtectionStopTriggerTraceSerializer.writeTraceRow",
			"schemaVersion, traceId, eventSeq, phase, packetName, returnReason, actionBranchName, stopCalled, expectsStopProtectionCall",
			"Emit every core trace row field in eventSeq order, including actionBranchName for packet branch identity.",
			$"Core row writer depends on trace-row contract present={fieldContract.HasTraceRowContract}; generated Java rows are still missing.");
	}

	private static void AddPlayerSnapshotWriter(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.PlayerSnapshotWriter,
			"future ProtectionStopTriggerTraceSerializer.writePlayerSnapshot",
			ContractFieldNames(fieldContract, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot),
			"Emit player objectId, spawned/flying/dead flags, protection before/after, and visual-state token arrays on every row.",
			"Visual state tokens must preserve Java enum/string names such as BLINKING; collection ordering remains a runtime verification risk.");
	}

	private static void AddNestedPayloadWriter(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.NestedPayloadWriter,
			"future ProtectionStopTriggerTraceSerializer.writeOptionalPayloads",
			ContractFieldNames(fieldContract, PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.NestedPayload),
			"Emit movement, taskCancellation, fanout, aiNotify, scheduler, emotion, actionPayload, and callerOrigin as objects or explicit nulls.",
			"Nested payload writer must not execute Java item/emotion/teleport behavior; it only serializes already-observed facts, preserving nulls for absent payloads.");
	}

	private static void AddTimestampPolicyWriter(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.TimestampPolicyWriter,
			"future ProtectionStopTriggerTraceSerializer.writeDiagnosticTimestamps",
			"wallTimeEpochMillis, monotonicNanos, timestampIsParityKey",
			"Emit diagnostic timestamps when available and always write timestampIsParityKey=false.",
			$"Date/time fields are never parity keys; fieldContractHasPolicy={fieldContract.HasTimestampNonParityPolicy}.");
	}

	private static void AddSourceBreadcrumbWriter(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.SourceBreadcrumbWriter,
			"future ProtectionStopTriggerTraceSerializer.writeSourceBreadcrumbs",
			"javaSources, javaSourceFile, javaLine",
			"Emit artifact-level source list plus row-level source file and line breadcrumbs.",
			$"Source line numbers are audit breadcrumbs, not deterministic parity keys; sourceSchemaFieldCount={fieldContract.SourceSchemaFieldCount}.");
	}

	private static void AddArtifactFileWriter(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.ArtifactFileWriter,
			"future protection-stop-trigger Java artifact generation command",
			"parity-artifacts/protection-stop-trigger/java/*.json",
			"Write stable scenario-named JSON files for the validator and runtime-comparison key projection.",
			$"Artifact generation remains blocked until Java serializer/tooling exists; requiresJavaSerializer={fieldContract.RequiresJavaSerializerImplementation}.");
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility responsibility,
		string javaTarget,
		string contractFields,
		string writerRule,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow(
			rows.Count + 1,
			responsibility,
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationStatus.BlockedMissingJavaSerializer,
			javaTarget,
			contractFields,
			writerRule,
			notes));
	}

	private static string ContractFieldNames(
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport fieldContract,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope scope) =>
		string.Join(", ", fieldContract.Rows
			.Where(row => row.Scope == scope)
			.Select(row => row.FieldName));

	private static bool HasResponsibility(
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility responsibility) =>
		rows.Any(row => row.Responsibility == responsibility);
}
