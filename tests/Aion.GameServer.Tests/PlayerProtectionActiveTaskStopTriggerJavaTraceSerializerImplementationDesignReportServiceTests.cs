using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignReportServiceTests
{
	[Fact]
	public void Create_MapsSerializerFieldContractToNonLiveWriterResponsibilities()
	{
		var report = CreateReport();

		Assert.False(report.IsLive);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.True(report.RequiresJavaSerializerImplementation);
		Assert.True(report.SerializerFieldContractRowCount > 0);
		Assert.True(report.HasTopLevelWriterPlan);
		Assert.True(report.HasRuntimeFactsWriterPlan);
		Assert.True(report.HasTraceRowCoreWriterPlan);
		Assert.True(report.HasPlayerSnapshotWriterPlan);
		Assert.True(report.HasNestedPayloadWriterPlan);
		Assert.True(report.HasTimestampPolicyWriterPlan);
		Assert.True(report.HasSourceBreadcrumbWriterPlan);
		Assert.True(report.HasArtifactFileWriterPlan);
		Assert.Equal(Enumerable.Range(1, report.Rows.Count), report.Rows.Select(row => row.Order));
		Assert.All(report.Rows, row => Assert.Equal(
			PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationStatus.BlockedMissingJavaSerializer,
			row.Status));
	}

	[Fact]
	public void Create_RequiresActionBranchNameInCoreTraceRowWriter()
	{
		var report = CreateReport();

		Assert.True(report.HasActionBranchNameWriterPlan);
		Assert.Contains(report.Rows, row =>
			row.Responsibility == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.TraceRowCoreWriter
			&& row.JavaTarget.Contains("writeTraceRow", StringComparison.Ordinal)
			&& row.ContractFields.Contains("actionBranchName", StringComparison.Ordinal)
			&& row.WriterRule.Contains("eventSeq order", StringComparison.Ordinal)
			&& row.Notes.Contains("generated Java rows are still missing", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DocumentsNestedPayloadWritersWithoutExecutingJavaBehavior()
	{
		var report = CreateReport();

		Assert.True(report.HasEmotionPayloadWriterPlan);
		Assert.True(report.HasActionPayloadWriterPlan);
		Assert.True(report.HasCallerOriginPayloadWriterPlan);
		Assert.Contains(report.Rows, row =>
			row.Responsibility == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.NestedPayloadWriter
			&& row.ContractFields.Contains("emotion", StringComparison.Ordinal)
			&& row.ContractFields.Contains("actionPayload", StringComparison.Ordinal)
			&& row.ContractFields.Contains("callerOrigin", StringComparison.Ordinal)
			&& row.WriterRule.Contains("explicit nulls", StringComparison.Ordinal)
			&& row.Notes.Contains("must not execute Java item/emotion/teleport behavior", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DocumentsTimestampAndSourceBreadcrumbNonParityPolicies()
	{
		var report = CreateReport();

		Assert.Contains(report.Rows, row =>
			row.Responsibility == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.TimestampPolicyWriter
			&& row.ContractFields.Contains("timestampIsParityKey", StringComparison.Ordinal)
			&& row.WriterRule.Contains("timestampIsParityKey=false", StringComparison.Ordinal)
			&& row.Notes.Contains("never parity keys", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Responsibility == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.SourceBreadcrumbWriter
			&& row.ContractFields.Contains("javaSourceFile", StringComparison.Ordinal)
			&& row.ContractFields.Contains("javaLine", StringComparison.Ordinal)
			&& row.Notes.Contains("not deterministic parity keys", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DocumentsArtifactWriterBlocker()
	{
		var report = CreateReport();

		Assert.Contains(report.Rows, row =>
			row.Responsibility == PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationResponsibility.ArtifactFileWriter
			&& row.ContractFields.Contains("parity-artifacts/protection-stop-trigger/java", StringComparison.Ordinal)
			&& row.WriterRule.Contains("scenario-named JSON", StringComparison.Ordinal)
			&& row.Notes.Contains("blocked until Java serializer/tooling exists", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignReport CreateReport()
	{
		var runtimeDesign = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());
		var traceSchema = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(runtimeDesign);
		var fieldContract = PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractService.Create(traceSchema);

		return PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignReportService.Create(fieldContract);
	}

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
