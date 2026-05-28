using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReportServiceTests
{
	[Fact]
	public void Create_ComposesObserverEmitterExecutionKeyAndReadinessRows()
	{
		var report = CreateDashboard(withKeyProjection: true);

		Assert.False(report.IsLive);
		Assert.True(report.HasJavaObserverCoverage);
		Assert.False(report.HasJavaHookDetailEvidence);
		Assert.Equal(0, report.JavaHookDetailRowCount);
		Assert.False(report.HasSerializerFieldContract);
		Assert.Equal(0, report.SerializerFieldContractRowCount);
		Assert.False(report.HasSerializerTimestampNonParityPolicy);
		Assert.False(report.HasSerializerNestedPayloadPlaceholders);
		Assert.False(report.HasSerializerActionBranchNameTraceContract);
		Assert.False(report.HasSerializerEmotionPayloadContract);
		Assert.False(report.HasSerializerActionPayloadContract);
		Assert.False(report.HasSerializerCallerOriginPayloadContract);
		Assert.False(report.HasSerializerImplementationDesign);
		Assert.Equal(0, report.SerializerImplementationDesignRowCount);
		Assert.False(report.HasSerializerTopLevelWriterPlan);
		Assert.False(report.HasSerializerRuntimeFactsWriterPlan);
		Assert.False(report.HasSerializerTraceRowCoreWriterPlan);
		Assert.False(report.HasSerializerPlayerSnapshotWriterPlan);
		Assert.False(report.HasSerializerNestedPayloadWriterPlan);
		Assert.False(report.HasSerializerTimestampPolicyWriterPlan);
		Assert.False(report.HasSerializerSourceBreadcrumbWriterPlan);
		Assert.False(report.HasSerializerArtifactFileWriterPlan);
		Assert.False(report.HasSerializerActionBranchNameWriterPlan);
		Assert.False(report.HasSerializerEmotionPayloadWriterPlan);
		Assert.False(report.HasSerializerActionPayloadWriterPlan);
		Assert.False(report.HasSerializerCallerOriginPayloadWriterPlan);
		Assert.False(report.NeedsProtectionArtifactSerializer);
		Assert.False(report.NeedsJavaObserverImplementation);
		Assert.True(report.NeedsJavaSerializerImplementation);
		Assert.True(report.HasJavaToolingBlocker);
		Assert.True(report.HasCSharpEmitterCoverage);
		Assert.True(report.HasRuntimeEvidenceBlocker);
		Assert.True(report.HasKeyProjectionEvidence);
		Assert.True(report.HasReadinessEvidence);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal(Enumerable.Range(1, report.Rows.Count), report.Rows.Select(row => row.Order));
		Assert.Contains(report.Rows, row => row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaObserverCoverage);
		Assert.Contains(report.Rows, row => row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.CSharpEmitterCoverage);
		Assert.Contains(report.Rows, row => row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.RuntimeComparisonReadiness);
	}

	[Fact]
	public void Create_SurfacesJavaToolingAndArtifactBlockers()
	{
		var report = CreateDashboard(withKeyProjection: false);

		Assert.True(report.NeedsJavaArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaObserverCoverage
			&& row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaTooling
			&& row.Evidence.Contains("requiresJava25Maven=True", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaToolingAndArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaTooling
			&& row.Evidence.Contains("needsJavaTooling=True", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_SurfacesCSharpEmitterRuntimeEvidenceAndReadinessBlockers()
	{
		var report = CreateDashboard(withKeyProjection: true);

		Assert.True(report.NeedsCSharpEmitter);
		Assert.True(report.NeedsRuntimeEvidence);
		Assert.True(report.NeedsComparisonExecution);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.CSharpEmitterCoverage
			&& row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingCSharpEmitter
			&& row.Evidence.Contains("requiresLiveEmitter=True", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.RuntimeEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingRuntimeEvidence
			&& row.Evidence.Contains("needsRuntimeEvidence=True", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.RuntimeComparisonReadiness
			&& row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedComparisonNotExecuted
			&& row.Evidence.Contains("needsExecution=True", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithAlignedKeyProjectionStillBlocksComparisonExecution()
	{
		var report = CreateDashboard(withKeyProjection: true);

		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.KeyProjection
			&& row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedComparisonNotExecuted
			&& row.Evidence.Contains("javaKeys=1", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharpKeys=1", StringComparison.Ordinal)
			&& row.Evidence.Contains("needsComparisonExecution=True", StringComparison.Ordinal)
			&& row.Notes.Contains("verified parity still requires deterministic", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithJavaHookDetailAddsSerializerAndObserverBlockerRow()
	{
		var report = CreateDashboard(withKeyProjection: true, withJavaHookDetail: true);

		Assert.True(report.HasJavaHookDetailEvidence);
		Assert.Equal(19, report.JavaHookDetailRowCount);
		Assert.True(report.NeedsProtectionArtifactSerializer);
		Assert.True(report.NeedsJavaObserverImplementation);
		Assert.True(report.NeedsJavaArtifacts);
		Assert.True(report.NeedsComparisonExecution);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaHookDetailCoverage
			&& row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaArtifacts
			&& row.BlocksRuntimeComparison
			&& row.Evidence.Contains("hookRows=19", StringComparison.Ordinal)
			&& row.Evidence.Contains("directStopCallers=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("needsSerializer=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("needsJavaObserver=True", StringComparison.Ordinal)
			&& row.Notes.Contains("schema-v1 artifact serialization", StringComparison.Ordinal));
		Assert.Equal(
			[
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaObserverCoverage,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaHookDetailCoverage,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaToolingAndArtifacts,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.CSharpEmitterCoverage,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.RuntimeEvidence,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.KeyProjection,
				PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.RuntimeComparisonReadiness
			],
			report.Rows.Select(row => row.Area));
	}

	[Fact]
	public void Create_WithSerializerFieldContractSurfacesSerializerPolicyOnJavaArtifactRow()
	{
		var report = CreateDashboard(withKeyProjection: true, withSerializerFieldContract: true);

		Assert.True(report.HasSerializerFieldContract);
		Assert.True(report.SerializerFieldContractRowCount > 0);
		Assert.True(report.HasSerializerTimestampNonParityPolicy);
		Assert.True(report.HasSerializerNestedPayloadPlaceholders);
		Assert.True(report.HasSerializerActionBranchNameTraceContract);
		Assert.True(report.HasSerializerEmotionPayloadContract);
		Assert.True(report.HasSerializerActionPayloadContract);
		Assert.True(report.HasSerializerCallerOriginPayloadContract);
		Assert.True(report.NeedsJavaSerializerImplementation);
		Assert.True(report.NeedsJavaArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaToolingAndArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaTooling
			&& row.Evidence.Contains("serializerFieldContract=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("timestampPolicy=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("nestedPayloadPlaceholders=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("actionBranchNameContract=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("emotionPayloadContract=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("actionPayloadContract=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("callerOriginPayloadContract=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("needsJavaSerializer=True", StringComparison.Ordinal)
			&& row.Notes.Contains("serializer field contract is metadata only", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithSerializerImplementationDesignSurfacesWriterPlanOnJavaArtifactRow()
	{
		var report = CreateDashboard(
			withKeyProjection: true,
			withSerializerFieldContract: true,
			withSerializerImplementationDesign: true);

		Assert.True(report.HasSerializerImplementationDesign);
		Assert.True(report.SerializerImplementationDesignRowCount > 0);
		Assert.True(report.HasSerializerTopLevelWriterPlan);
		Assert.True(report.HasSerializerRuntimeFactsWriterPlan);
		Assert.True(report.HasSerializerTraceRowCoreWriterPlan);
		Assert.True(report.HasSerializerPlayerSnapshotWriterPlan);
		Assert.True(report.HasSerializerNestedPayloadWriterPlan);
		Assert.True(report.HasSerializerTimestampPolicyWriterPlan);
		Assert.True(report.HasSerializerSourceBreadcrumbWriterPlan);
		Assert.True(report.HasSerializerArtifactFileWriterPlan);
		Assert.True(report.HasSerializerActionBranchNameWriterPlan);
		Assert.True(report.HasSerializerEmotionPayloadWriterPlan);
		Assert.True(report.HasSerializerActionPayloadWriterPlan);
		Assert.True(report.HasSerializerCallerOriginPayloadWriterPlan);
		Assert.True(report.NeedsJavaSerializerImplementation);
		Assert.True(report.NeedsJavaArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea.JavaToolingAndArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaTooling
			&& row.Evidence.Contains("serializerImplementationDesign=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("topLevelWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("runtimeFactsWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("traceRowCoreWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("playerSnapshotWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("nestedPayloadWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("timestampPolicyWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("sourceBreadcrumbWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("artifactFileWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("actionBranchNameWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("emotionPayloadWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("actionPayloadWriter=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("callerOriginPayloadWriter=True", StringComparison.Ordinal)
			&& row.Notes.Contains("writer responsibility design are metadata only", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReport CreateDashboard(
		bool withKeyProjection,
		bool withJavaHookDetail = false,
		bool withSerializerFieldContract = false,
		bool withSerializerImplementationDesign = false)
	{
		var runtimeDesign = CreateRuntimeDesign();
		var traceSchema = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(runtimeDesign);
		var emitterDesign = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(runtimeDesign, traceSchema);
		var serializerFieldContract = withSerializerFieldContract
			? PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerFieldContractService.Create(traceSchema)
			: null;
		var serializerImplementationDesign = withSerializerImplementationDesign && serializerFieldContract != null
			? PlayerProtectionActiveTaskStopTriggerJavaTraceSerializerImplementationDesignReportService.Create(serializerFieldContract)
			: null;
		var executionPlan = PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanService.Create(
			runtimeDesign,
			traceSchema,
			emitterDesign,
			serializerFieldContract,
			serializerImplementationDesign);
		var observerRunbook = PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReportService.Create(executionPlan);
		var artifactReport = CreateShapeValidArtifactDirectoryReportWithMetadata("cm-teleport-animation-done");
		var csharpTrace = CreateSyntheticCSharpRuntimeTraceReport("cm-teleport-animation-done");
		var keyProjection = withKeyProjection
			? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(artifactReport, csharpTrace)
			: null;
		var readiness = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			runtimeDesign,
			traceSchema,
			artifactReport,
			comparisonContract: null,
			preflightReport: null,
			keyProjection,
			emitterDesign,
			executionPlan,
			observerRunbook,
			serializerFieldContract: serializerFieldContract,
			serializerImplementationDesign: serializerImplementationDesign);

		return PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReportService.Create(
			observerRunbook,
			executionPlan,
			emitterDesign,
			readiness,
			keyProjection,
			withJavaHookDetail ? PlayerProtectionActiveTaskStopTriggerJavaHookDetailReportService.Create() : null,
			serializerFieldContract,
			serializerImplementationDesign);
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
