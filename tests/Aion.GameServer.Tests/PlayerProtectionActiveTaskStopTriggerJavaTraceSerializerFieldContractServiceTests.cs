using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractServiceTests
{
	[Fact]
	public void Create_MapsTopLevelRuntimeTraceAndPlayerSnapshotContracts()
	{
		var report = CreateReport();

		Assert.False(report.IsLive);
		Assert.True(report.HasTopLevelContract);
		Assert.True(report.HasRuntimeFactsContract);
		Assert.True(report.HasTraceRowContract);
		Assert.True(report.HasPlayerSnapshotContract);
		Assert.True(report.HasNestedPayloadPlaceholders);
		Assert.True(report.HasActionBranchNameTraceContract);
		Assert.True(report.HasEmotionPayloadContract);
		Assert.True(report.HasActionPayloadContract);
		Assert.True(report.HasCallerOriginPayloadContract);
		Assert.True(report.SourceSchemaFieldCount > report.Rows.Count);
		Assert.Equal(Enumerable.Range(1, report.Rows.Count), report.Rows.Select(row => row.Order));
		Assert.Contains(report.Rows, row =>
			row.Scope == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TopLevel
			&& row.JsonPath == "$.schemaVersion"
			&& row.SourceSchemaField == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.TraceSchemaVersion
			&& row.SerializationRule == "integer literal 1");
		Assert.Contains(report.Rows, row =>
			row.Scope == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.RuntimeFacts
			&& row.JsonPath == "$.runtimeFacts.expectedReturnReason"
			&& row.Notes.Contains("unknown return reasons", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Scope == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow
			&& row.JsonPath == "$.traces[*].player"
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.RequiredSchemaV1);
		Assert.Contains(report.Rows, row =>
			row.Scope == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.TraceRow
			&& row.JsonPath == "$.traces[*].actionBranchName"
			&& row.SourceSchemaField == PlayerProtectionActiveTaskStopTriggerTraceArtifactField.ActionBranchName
			&& row.Notes.Contains("generic return reasons", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Scope == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldScope.PlayerSnapshot
			&& row.JsonPath == "$.traces[*].player.visualStateBefore"
			&& row.Notes.Contains("BLINKING", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DocumentsTimestampPolicyAsDiagnosticOnly()
	{
		var report = CreateReport();

		Assert.True(report.HasTimestampNonParityPolicy);
		Assert.Contains(report.Rows, row =>
			row.JsonPath == "$.traces[*].timestampIsParityKey"
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.DiagnosticOnly
			&& row.SerializationRule == "must be false"
			&& row.Notes.Contains("Java/C# clocks are not parity evidence", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.JsonPath == "$.traces[*].wallTimeEpochMillis"
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.DiagnosticOnly
			&& row.Notes.Contains("Date/time handling", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.JsonPath == "$.traces[*].monotonicNanos"
			&& row.Notes.Contains("eventSeq", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_KeepsNestedPayloadsBlockedUntilJavaSerializerExists()
	{
		var report = CreateReport();

		Assert.True(report.RequiresJavaSerializerImplementation);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains("requiresTraceSerializer=True", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains(report.Rows, row =>
			row.JsonPath == "$.traces[*].taskCancellation"
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer
			&& row.Notes.Contains("Future.cancel(false)", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.JsonPath == "$.traces[*].fanout"
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer
			&& row.Notes.Contains("include-self", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.JsonPath == "$.traces[*].scheduler"
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer
			&& row.Notes.Contains("RunnableFuture", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DocumentsActionEmotionAndCallerOriginNestedPayloadContracts()
	{
		var report = CreateReport();

		Assert.Contains(report.Rows, row =>
			row.JsonPath == "$.traces[*].emotion"
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer
			&& row.SerializationRule.Contains("emotionBroadcasted", StringComparison.Ordinal)
			&& row.Notes.Contains("SM_EMOTION", StringComparison.Ordinal)
			&& row.Notes.Contains("late stop", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.JsonPath == "$.traces[*].actionPayload"
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer
			&& row.SerializationRule.Contains("compositeCanActResult", StringComparison.Ordinal)
			&& row.Notes.Contains("item lookup", StringComparison.Ordinal)
			&& row.Notes.Contains("composite canAct", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.JsonPath == "$.traces[*].callerOrigin"
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldStatus.BlockedUntilJavaSerializer
			&& row.SerializationRule.Contains("startsProtectionBeforeWorldSpawn", StringComparison.Ordinal)
			&& row.SerializationRule.Contains("ordering", StringComparison.Ordinal)
			&& row.Notes.Contains("world-spawn ordering", StringComparison.Ordinal)
			&& row.Notes.Contains("source line numbers", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractReport CreateReport() =>
		PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractService.Create(
			PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign()));

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport CreateRuntimeDesign() =>
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

	private static PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport CreateDetailedSummary() =>
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(
			PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(new PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest(
				PlayerSpawned: true,
				AntiHackAccepted: true,
				TeleportationModeAbsoluteMove: false,
				PlayerProtectionActive: true,
				CurrentX: 100f,
				CurrentY: 200f,
				CurrentZ: 50f,
				PacketX: 101f,
				PacketY: 200f,
				PacketZ: 50f,
				EvaluateCmMoveInAir: true,
				EvaluateCmAttack: true,
				EvaluateCmCastSpell: true,
				EvaluateCmUseItem: true,
				EvaluateCmShowDialog: true,
				EvaluateCmDialogSelect: true,
				EvaluateCmCompositeStones: true,
				EvaluateCmEmotion: true)));
}
