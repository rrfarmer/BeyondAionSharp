using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanServiceTests
{
	[Fact]
	public void Create_SequencesAllRuntimeComparisonExecutionGates()
	{
		var report = CreateReport();

		Assert.False(report.IsLive);
		Assert.False(report.HasSerializerFieldContract);
		Assert.Equal(0, report.SerializerFieldContractRowCount);
		Assert.False(report.HasSerializerTimestampNonParityPolicy);
		Assert.False(report.HasSerializerNestedPayloadPlaceholders);
		Assert.True(report.NeedsJavaSerializerImplementation);
		Assert.True(report.HasJavaToolingGate);
		Assert.True(report.HasJavaArtifactGenerationGate);
		Assert.True(report.HasCSharpEmitterGate);
		Assert.True(report.HasKeyProjectionGate);
		Assert.True(report.HasComparisonExecutionGate);
		Assert.Equal(
			Enumerable.Range(1, report.Rows.Count),
			report.Rows.Select(row => row.Order));
	}

	[Fact]
	public void Create_KeepsRuntimeComparisonBlockedAtEveryLivePrerequisite()
	{
		var report = CreateReport();

		Assert.True(report.NeedsJavaTooling);
		Assert.True(report.NeedsJavaArtifacts);
		Assert.True(report.NeedsCSharpEmitter);
		Assert.True(report.NeedsRuntimeEvidence);
		Assert.True(report.NeedsComparisonExecution);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.RuntimeComparisonExecution
			&& row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedComparisonNotExecuted
			&& row.Notes.Contains("Verified parity cannot be claimed", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DocumentsJavaToolingObserverSerializerAndArtifactGeneration()
	{
		var report = CreateReport();

		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaToolingCheck
			&& row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingTooling);
		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaObserverDesign
			&& row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.ReadyForDesignOnly);
		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaTraceSerializer
			&& row.Notes.Contains("timestamp non-parity", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaArtifactGeneration
			&& row.Notes.Contains("teleport animation", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithSerializerFieldContractSurfacesTimestampAndNestedPayloadBlockers()
	{
		var report = CreateReport(withSerializerFieldContract: true);

		Assert.True(report.HasSerializerFieldContract);
		Assert.True(report.SerializerFieldContractRowCount > 0);
		Assert.True(report.HasSerializerTimestampNonParityPolicy);
		Assert.True(report.HasSerializerNestedPayloadPlaceholders);
		Assert.True(report.NeedsJavaSerializerImplementation);
		Assert.True(report.NeedsJavaArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.JavaTraceSerializer
			&& row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingJavaArtifact
			&& row.Evidence.Contains("serializerFieldContract=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("timestampPolicy=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("nestedPayloadPlaceholders=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("requiresJavaSerializer=True", StringComparison.Ordinal)
			&& row.Notes.Contains("blocked nested payloads", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DocumentsCSharpEmitterTraceCaptureAndKeyProjectionGates()
	{
		var report = CreateReport();

		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.CSharpEmitterDesign
			&& row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.ReadyForDesignOnly
			&& row.Evidence.Contains("packetHooks=True", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.CSharpEmitterImplementation
			&& row.Status == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionStatus.BlockedMissingCSharpImplementation);
		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.CSharpTraceCapture
			&& row.Evidence.Contains("no live C# trace rows", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Gate == PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionGate.KeyProjection
			&& row.CSharpTarget.Contains("KeyProjection", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanReport CreateReport(
		bool withSerializerFieldContract = false)
	{
		var runtimeDesign = CreateRuntimeDesign();
		var traceSchema = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(runtimeDesign);
		var emitterDesign = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(runtimeDesign, traceSchema);
		var serializerFieldContract = withSerializerFieldContract
			? PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractService.Create(traceSchema)
			: null;

		return PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanService.Create(
			runtimeDesign,
			traceSchema,
			emitterDesign,
			serializerFieldContract);
	}

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
