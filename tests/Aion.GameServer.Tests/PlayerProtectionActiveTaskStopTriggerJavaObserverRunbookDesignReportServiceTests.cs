using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReportServiceTests
{
	[Fact]
	public void Create_ListsNonLiveObserverRunbookSections()
	{
		var report = CreateReport();

		Assert.False(report.IsLive);
		Assert.True(report.HasToolingPrerequisite);
		Assert.True(report.HasPacketStopTriggerHooks);
		Assert.True(report.HasControllerHooks);
		Assert.True(report.HasTeleportHooks);
		Assert.True(report.HasSerializerPlan);
		Assert.True(report.RequiresJava25Maven);
		Assert.False(report.ReadyForArtifactGeneration);
		Assert.Equal(Enumerable.Range(1, report.Rows.Count), report.Rows.Select(row => row.Order));
	}

	[Fact]
	public void Create_ToolingRowsDocumentLocalJava25MavenBlocker()
	{
		var report = CreateReport();

		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ToolingPrerequisite
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.BlockedMissingJava25Maven
			&& row.Notes.Contains("compiler release 25", StringComparison.Ordinal)
			&& row.Notes.Contains("Java is 1.8.0_491", StringComparison.Ordinal)
			&& row.Notes.Contains("Maven is absent", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ArtifactGenerationCommand
			&& row.Status == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignStatus.BlockedMissingJava25Maven
			&& row.Notes.Contains("needsJavaTooling=True", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RecordsPacketControllerAndTeleportObserverEvents()
	{
		var report = CreateReport();

		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.PacketStopTriggerHook
			&& row.JavaSource.Contains("CM_MOVE", StringComparison.Ordinal)
			&& row.JavaSource.Contains("CM_USE_ITEM", StringComparison.Ordinal)
			&& row.ExpectedObserverEvent == "packet_stop_trigger_decision"
			&& row.ArtifactOutput.Contains("packet_stop_decision", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.ControllerProtectionHook
			&& row.JavaSource.Contains("PlayerController.stopProtectionActiveTask", StringComparison.Ordinal)
			&& row.Notes.Contains("BLINKING", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.TeleportAnimationHook
			&& row.JavaSource.Contains("CM_TELEPORT_ANIMATION_DONE.runImpl", StringComparison.Ordinal)
			&& row.Notes.Contains("RunnableFuture", StringComparison.Ordinal)
			&& row.Notes.Contains("World.spawn", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DocumentsSerializerOutputAndNoByteSerializationClaim()
	{
		var report = CreateReport();

		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.TraceSerializer
			&& row.ArtifactOutput.Contains("parity-artifacts/protection-stop-trigger/java", StringComparison.Ordinal)
			&& row.Notes.Contains("timestamp non-parity", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignArea.PacketFanoutHook
			&& row.JavaSource.Contains("AionServerPacket", StringComparison.Ordinal)
			&& row.Notes.Contains("byte-level serialization capture is still a separate blocked prerequisite", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReport CreateReport()
	{
		var runtimeDesign = CreateRuntimeDesign();
		var traceSchema = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(runtimeDesign);
		var emitterDesign = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(runtimeDesign, traceSchema);
		var executionPlan = PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanService.Create(
			runtimeDesign,
			traceSchema,
			emitterDesign);

		return PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReportService.Create(executionPlan);
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
