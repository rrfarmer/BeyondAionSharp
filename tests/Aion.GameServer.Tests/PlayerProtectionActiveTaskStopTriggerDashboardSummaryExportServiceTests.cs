using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportServiceTests
{
	[Fact]
	public void Create_SummarizesDashboardAsNonLiveBlockedExport()
	{
		var report = CreateSummaryExport();

		Assert.False(report.IsLive);
		Assert.Equal(PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportStatus.Blocked, report.Status);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal(6, report.DashboardRowCount);
		Assert.Equal(report.Blockers.Count, report.BlockingRowCount);
		Assert.Contains("Blocked:", report.Summary, StringComparison.Ordinal);
		Assert.Contains("dashboardRows=6", report.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ListsTopBlockersInStableOrder()
	{
		var report = CreateSummaryExport();

		Assert.Equal(Enumerable.Range(1, report.Blockers.Count), report.Blockers.Select(row => row.Order));
		Assert.Equal(
			[
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaObserverCoverage,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaToolingAndArtifacts,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.CSharpEmitterCoverage,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.RuntimeEvidence,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.KeyProjection,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.RuntimeComparisonReadiness
			],
			report.Blockers.Select(row => row.Area));
		Assert.All(report.Blockers, row => Assert.False(string.IsNullOrWhiteSpace(row.Evidence)));
		Assert.All(report.Blockers, row => Assert.False(string.IsNullOrWhiteSpace(row.Notes)));
	}

	[Fact]
	public void Create_PreservesJavaToolingEmitterRuntimeEvidenceAndComparisonFlags()
	{
		var report = CreateSummaryExport();

		Assert.True(report.HasJavaToolingBlocker);
		Assert.True(report.HasJavaArtifactBlocker);
		Assert.True(report.HasCSharpEmitterBlocker);
		Assert.True(report.HasRuntimeEvidenceBlocker);
		Assert.True(report.HasComparisonExecutionBlocker);
		Assert.True(report.HasKeyProjectionEvidence);
		Assert.Contains("Java tooling", report.Summary, StringComparison.Ordinal);
		Assert.Contains("Java artifacts", report.Summary, StringComparison.Ordinal);
		Assert.Contains("C# emitter", report.Summary, StringComparison.Ordinal);
		Assert.Contains("runtime evidence", report.Summary, StringComparison.Ordinal);
		Assert.Contains("comparison execution", report.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DocumentsNoParityClaim()
	{
		var report = CreateSummaryExport();

		Assert.Equal("Protection stop-trigger prerequisite dashboard", report.JavaSource);
		Assert.Contains(report.Blockers, row =>
			row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaTooling
			&& row.Notes.Contains("artifact generation remains blocked", StringComparison.Ordinal));
		Assert.Contains(report.Blockers, row =>
			row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingRuntimeEvidence
			&& row.Notes.Contains("both exist", StringComparison.Ordinal));
		Assert.Contains(report.Blockers, row =>
			row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedComparisonNotExecuted
			&& row.Notes.Contains("do not execute comparison", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithJavaHookDetailSurfacesHookRowsAndSerializerBlockers()
	{
		var report = PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportService.Create(
			CreateDashboard(),
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailReportService.Create());

		Assert.True(report.HasJavaHookDetailEvidence);
		Assert.Equal(19, report.JavaHookDetailRowCount);
		Assert.True(report.NeedsProtectionArtifactSerializer);
		Assert.True(report.NeedsJavaObserverImplementation);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains("javaHookRows=19", report.Summary, StringComparison.Ordinal);
		Assert.Contains("protection artifact serializer", report.Summary, StringComparison.Ordinal);
		Assert.Contains("Java observer implementation", report.Summary, StringComparison.Ordinal);
	}

	private static PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportReport CreateSummaryExport() =>
		PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportService.Create(CreateDashboard());

	private static PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReport CreateDashboard()
	{
		var runtimeDesign = CreateRuntimeDesign();
		var traceSchema = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(runtimeDesign);
		var emitterDesign = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(runtimeDesign, traceSchema);
		var executionPlan = PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanService.Create(
			runtimeDesign,
			traceSchema,
			emitterDesign);
		var observerRunbook = PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReportService.Create(executionPlan);
		var artifactReport = CreateShapeValidArtifactDirectoryReportWithMetadata("cm-teleport-animation-done");
		var csharpTrace = CreateSyntheticCSharpRuntimeTraceReport("cm-teleport-animation-done");
		var keyProjection = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(artifactReport, csharpTrace);
		var readiness = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			runtimeDesign,
			traceSchema,
			artifactReport,
			comparisonContract: null,
			preflightReport: null,
			keyProjection,
			emitterDesign,
			executionPlan,
			observerRunbook);

		return PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReportService.Create(
			observerRunbook,
			executionPlan,
			emitterDesign,
			readiness,
			keyProjection);
	}

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport CreateRuntimeDesign() =>
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport CreateSyntheticCSharpRuntimeTraceReport(string scenario) =>
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
			[CreateSyntheticCSharpTraceRow(scenario)],
			hasLivePacketHooks: true,
			"synthetic C# trace report only");

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceRow CreateSyntheticCSharpTraceRow(string scenario) =>
		new(
			EventSeq: 0,
			Scenario: scenario,
			Phase: "teleport_task_remove",
			PacketName: "CM_TELEPORT_ANIMATION_DONE",
			ReturnReason: "animation_done_no_pending_runnable_teleport_task",
			StopCalled: false,
			ExpectsStopProtectionCall: false,
			TimestampIsParityKey: false,
			new PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTracePlayerSnapshot(
				ObjectId: 1001,
				Spawned: false,
				Flying: false,
				Dead: false,
				ProtectionActiveBefore: true,
				ProtectionActiveAfter: true,
				VisualStateBefore: ["BLINKING"],
				VisualStateAfter: ["BLINKING"]));

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport CreateShapeValidArtifactDirectoryReportWithMetadata(
		string scenarioName) =>
		new(
			PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
			[
				new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow(
					$"{scenarioName}.json",
					new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport(
						[],
						IsValidSchemaV1: true,
						ReadyForRuntimeComparison: false,
						"shape-valid metadata",
						new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactMetadata(
							SchemaVersion: 1,
							JavaCommit: "abcdef1",
							Scenario: scenarioName,
							RuntimePacketName: "CM_TELEPORT_ANIMATION_DONE",
							RuntimeExpectedReturnReason: "animation_done_no_pending_runnable_teleport_task",
							TraceRows:
							[
								new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactTraceRow(
									EventSeq: 0,
									Phase: "teleport_task_remove",
									PacketName: "CM_TELEPORT_ANIMATION_DONE",
									ReturnReason: "animation_done_no_pending_runnable_teleport_task",
									StopCalled: false,
									ExpectsStopProtectionCall: false,
									TimestampIsParityKey: false,
									Player: new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactPlayerSnapshot(
										ObjectId: 1001,
										Spawned: false,
										Flying: false,
										Dead: false,
										ProtectionActiveBefore: true,
										ProtectionActiveAfter: true,
										VisualStateBefore: ["BLINKING"],
										VisualStateAfter: ["BLINKING"]))
							])))
			],
			HasGeneratedJavaArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid generated Java artifact metadata only");

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
