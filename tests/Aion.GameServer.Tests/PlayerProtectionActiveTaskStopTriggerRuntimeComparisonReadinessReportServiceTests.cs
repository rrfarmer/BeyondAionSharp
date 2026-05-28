using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportServiceTests
{
	[Fact]
	public void Create_WithRuntimeDesignAndTraceSchemaKeepsMissingGeneratedArtifactBlockerExplicit()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.False(report.IsLive);
		Assert.True(report.HasRuntimeComparisonDesign);
		Assert.True(report.HasTraceArtifactSchema);
		Assert.False(report.HasGeneratedJavaTraceArtifactDirectoryReport);
		Assert.False(report.HasShapeValidGeneratedJavaTraceArtifacts);
		Assert.False(report.HasRuntimeComparisonContractReport);
		Assert.False(report.HasRuntimeComparisonPreflightReport);
		Assert.False(report.HasRuntimeComparisonKeyProjectionReport);
		Assert.False(report.HasJavaObserverRunbookDesign);
		Assert.True(report.NeedsJavaInstrumentation);
		Assert.True(report.NeedsJavaObserverRunbookDesign);
		Assert.True(report.NeedsJavaTraceSerializer);
		Assert.True(report.NeedsGeneratedJavaTraceArtifacts);
		Assert.False(report.NeedsCSharpArtifactReader);
		Assert.False(report.HasCSharpTraceEmitterDesign);
		Assert.True(report.NeedsCSharpTraceEmitter);
		Assert.False(report.HasGeneratedArtifactExecutionPlan);
		Assert.True(report.NeedsGeneratedArtifactExecutionPlan);
		Assert.True(report.NeedsLiveCSharpPacketHooks);
		Assert.False(report.NeedsCSharpRuntimeTraceOutput);
		Assert.False(report.NeedsRuntimeComparisonPreflightAlignment);
		Assert.False(report.NeedsRuntimeComparisonKeyAlignment);
		Assert.False(report.NeedsRuntimeComparisonExecution);
		Assert.True(report.NeedsRuntimeComparisonEvidence);
		Assert.False(report.ReadyForRuntimeComparison);
	}

	[Fact]
	public void Create_MissingTraceSchemaBlocksBeforeArtifactReaderWork()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			traceSchema: null);

		Assert.False(report.HasTraceArtifactSchema);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.TraceArtifactSchema
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
			&& row.BlocksRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpArtifactReader
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
			&& row.Evidence.Contains("trace schema missing", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingRuntimeDesignBlocksLiveHookAndScenarioReadiness()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			runtimeDesign: null,
			CreateTraceSchema());

		Assert.False(report.HasRuntimeComparisonDesign);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonDesign
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.LiveCSharpPacketHooks
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite);
	}

	[Fact]
	public void Create_TraceSchemaRowSummarizesPhaseFieldAndReturnReasonCounts()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.TraceArtifactSchema
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata
			&& row.Evidence.Contains("phases=", StringComparison.Ordinal)
			&& row.Evidence.Contains("fields=", StringComparison.Ordinal)
			&& row.Evidence.Contains("returnReasons=", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CSharpArtifactReaderDocumentsExistingParserValidationContract()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpArtifactReader
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata
			&& !row.BlocksRuntimeComparison
			&& row.CSharpTarget.Contains("PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService", StringComparison.Ordinal)
			&& row.Notes.Contains("not a runtime comparator", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithCSharpTraceEmitterDesignSurfacesDistinctEmitterBlocker()
	{
		var runtimeDesign = CreateRuntimeDesign();
		var traceSchema = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(runtimeDesign);
		var emitterDesign = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(runtimeDesign, traceSchema);

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			runtimeDesign,
			traceSchema,
			csharpTraceEmitterDesign: emitterDesign);

		Assert.True(report.HasCSharpTraceEmitterDesign);
		Assert.True(report.NeedsCSharpTraceEmitter);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.CSharpTraceEmitterDesign
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpImplementation
			&& row.Evidence.Contains("packetHooks=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("controllerHooks=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("teleportHooks=True", StringComparison.Ordinal)
			&& row.Notes.Contains("non-live design only", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithJavaObserverRunbookDesignSurfacesToolingBlocker()
	{
		var runtimeDesign = CreateRuntimeDesign();
		var traceSchema = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(runtimeDesign);
		var emitterDesign = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(runtimeDesign, traceSchema);
		var executionPlan = PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanService.Create(
			runtimeDesign,
			traceSchema,
			emitterDesign);
		var observerRunbook = PlayerProtectionActiveTaskStopTriggerJavaObserverRunbookDesignReportService.Create(executionPlan);

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			runtimeDesign,
			traceSchema,
			javaObserverRunbookDesign: observerRunbook);

		Assert.True(report.HasJavaObserverRunbookDesign);
		Assert.True(report.NeedsJavaObserverRunbookDesign);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.JavaObserverRunbookDesign
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
			&& row.Evidence.Contains("packetHooks=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("serializerPlan=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("requiresJava25Maven=True", StringComparison.Ordinal)
			&& row.Notes.Contains("missing Java 25/Maven tooling", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithGeneratedArtifactExecutionPlanSurfacesExecutionPlanGate()
	{
		var runtimeDesign = CreateRuntimeDesign();
		var traceSchema = PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(runtimeDesign);
		var emitterDesign = PlayerProtectionActiveTaskStopTriggerCSharpTraceEmitterDesignReportService.Create(runtimeDesign, traceSchema);
		var executionPlan = PlayerProtectionActiveTaskStopTriggerGeneratedArtifactExecutionPlanService.Create(
			runtimeDesign,
			traceSchema,
			emitterDesign);

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			runtimeDesign,
			traceSchema,
			csharpTraceEmitterDesign: emitterDesign,
			generatedArtifactExecutionPlan: executionPlan);

		Assert.True(report.HasGeneratedArtifactExecutionPlan);
		Assert.True(report.NeedsGeneratedArtifactExecutionPlan);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedArtifactExecutionPlan
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingPrerequisite
			&& row.Evidence.Contains("needsJavaTooling=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("needsComparisonExecution=True", StringComparison.Ordinal)
			&& row.Notes.Contains("Maven/Java tooling readiness", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithShapeValidGeneratedArtifactsClearsArtifactBlockerButKeepsRuntimeEvidenceBlocked()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			CreateShapeValidArtifactDirectoryReport());

		Assert.True(report.HasGeneratedJavaTraceArtifactDirectoryReport);
		Assert.True(report.HasShapeValidGeneratedJavaTraceArtifacts);
		Assert.False(report.NeedsGeneratedJavaTraceArtifacts);
		Assert.False(report.NeedsCSharpArtifactReader);
		Assert.True(report.NeedsRuntimeComparisonEvidence);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedJavaTraceArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata
			&& !row.BlocksRuntimeComparison
			&& row.Evidence.Contains("shapeValidFiles=1", StringComparison.Ordinal)
			&& row.Notes.Contains("runtime comparison still needs live C# hooks", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithMissingArtifactDirectoryReportKeepsGeneratedArtifactBlocker()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport(
				PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.MissingDirectory,
				[],
				HasGeneratedJavaArtifacts: false,
				ReadyForRuntimeComparison: false,
				"missing generated Java artifacts"));

		Assert.True(report.HasGeneratedJavaTraceArtifactDirectoryReport);
		Assert.False(report.HasShapeValidGeneratedJavaTraceArtifacts);
		Assert.True(report.NeedsGeneratedJavaTraceArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedJavaTraceArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact
			&& row.Evidence.Contains("status=MissingDirectory", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithInvalidArtifactDirectoryReportSurfacesInvalidJavaArtifactStatus()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			CreateInvalidArtifactDirectoryReport());

		Assert.True(report.HasGeneratedJavaTraceArtifactDirectoryReport);
		Assert.False(report.HasShapeValidGeneratedJavaTraceArtifacts);
		Assert.True(report.NeedsGeneratedJavaTraceArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.GeneratedJavaTraceArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedInvalidJavaArtifact
			&& row.BlocksRuntimeComparison
			&& row.Evidence.Contains("validFiles=0", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeEvidenceBlockerPreventsVerifiedParityClaim()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema());

		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingRuntimeEvidence
			&& row.Notes.Contains("Verified parity cannot be claimed", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithComparisonContractMissingCSharpTraceUpdatesRuntimeEvidenceRow()
	{
		var artifactReport = CreateShapeValidArtifactDirectoryReport();
		var comparisonContract = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.Create(artifactReport);

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			artifactReport,
			comparisonContract);

		Assert.True(report.HasRuntimeComparisonContractReport);
		Assert.True(report.NeedsCSharpRuntimeTraceOutput);
		Assert.True(report.NeedsRuntimeComparisonExecution);
		Assert.True(report.NeedsRuntimeComparisonEvidence);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedMissingCSharpRuntimeTrace
			&& row.Evidence.Contains("needsCSharpRuntimeTrace=True", StringComparison.Ordinal)
			&& row.Notes.Contains("live C# stop-trigger trace output is missing", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithComparisonContractComparisonNotExecutedUpdatesRuntimeEvidenceRow()
	{
		var artifactReport = CreateShapeValidArtifactDirectoryReport();
		var comparisonContract = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.Create(
			artifactReport,
			CreateSyntheticCSharpRuntimeTraceReport(hasLivePacketHooks: true));

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			artifactReport,
			comparisonContract);

		Assert.True(report.HasRuntimeComparisonContractReport);
		Assert.False(report.NeedsCSharpRuntimeTraceOutput);
		Assert.True(report.NeedsRuntimeComparisonExecution);
		Assert.True(report.NeedsRuntimeComparisonEvidence);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedComparisonNotExecuted
			&& row.Evidence.Contains("needsExecutedComparison=True", StringComparison.Ordinal)
			&& row.Notes.Contains("deterministic Java/C# trace comparison has not executed", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithComparisonContractInvalidJavaArtifactsKeepsRuntimeEvidenceSpecific()
	{
		var artifactReport = CreateInvalidArtifactDirectoryReport();
		var comparisonContract = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.Create(artifactReport);

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			artifactReport,
			comparisonContract);

		Assert.True(report.HasRuntimeComparisonContractReport);
		Assert.True(report.NeedsGeneratedJavaTraceArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedInvalidJavaArtifact
			&& row.Evidence.Contains("needsJavaArtifacts=True", StringComparison.Ordinal)
			&& row.Notes.Contains("generated Java trace artifacts are missing or invalid", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithPreflightScenarioMismatchUpdatesRuntimeEvidenceRow()
	{
		var artifactReport = CreateShapeValidArtifactDirectoryReport("cm-move-threshold");
		var preflight = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			artifactReport,
			CreateSyntheticCSharpRuntimeTraceReport(hasLivePacketHooks: true, scenario: "cm-teleport-animation-done"));

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			artifactReport,
			comparisonContract: null,
			preflightReport: preflight);

		Assert.True(report.HasRuntimeComparisonPreflightReport);
		Assert.True(report.NeedsRuntimeComparisonPreflightAlignment);
		Assert.True(report.NeedsRuntimeComparisonEvidence);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedScenarioMismatch
			&& row.Evidence.Contains("needsScenarioAlignment=True", StringComparison.Ordinal)
			&& row.Notes.Contains("scenarios do not align", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithPreflightRowCountMismatchUpdatesRuntimeEvidenceRow()
	{
		var artifactReport = CreateShapeValidArtifactDirectoryReport("cm-move-threshold");
		var preflight = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			artifactReport,
			CreateSyntheticCSharpRuntimeTraceReport(
				true,
				"cm-move-threshold",
				"cm-move-threshold"));

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			artifactReport,
			comparisonContract: null,
			preflightReport: preflight);

		Assert.True(report.HasRuntimeComparisonPreflightReport);
		Assert.True(report.NeedsRuntimeComparisonPreflightAlignment);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedRowCountMismatch
			&& row.Evidence.Contains("needsRowCountAlignment=True", StringComparison.Ordinal)
			&& row.Notes.Contains("row counts do not align", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithAlignedPreflightStillBlocksComparisonExecution()
	{
		var artifactReport = CreateShapeValidArtifactDirectoryReport("cm-teleport-animation-done");
		var preflight = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			artifactReport,
			CreateSyntheticCSharpRuntimeTraceReport(hasLivePacketHooks: true, scenario: "cm-teleport-animation-done"));

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			artifactReport,
			comparisonContract: null,
			preflightReport: preflight);

		Assert.True(report.HasRuntimeComparisonPreflightReport);
		Assert.False(report.NeedsRuntimeComparisonPreflightAlignment);
		Assert.True(report.NeedsRuntimeComparisonExecution);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedComparisonNotExecuted
			&& row.Evidence.Contains("needsComparisonExecution=True", StringComparison.Ordinal)
			&& row.Notes.Contains("preflight alignment has not executed deterministic", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithKeyProjectionMismatchUpdatesRuntimeEvidenceRow()
	{
		var artifactReport = CreateShapeValidArtifactDirectoryReportWithMetadata("cm-move-threshold");
		var csharpTrace = CreateSyntheticCSharpRuntimeTraceReport(hasLivePacketHooks: true, scenario: "cm-teleport-animation-done");
		var preflight = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			artifactReport,
			csharpTrace);
		var keyProjection = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(
			artifactReport,
			csharpTrace);

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			artifactReport,
			comparisonContract: null,
			preflight,
			keyProjection);

		Assert.True(report.HasRuntimeComparisonPreflightReport);
		Assert.True(report.HasRuntimeComparisonKeyProjectionReport);
		Assert.True(report.NeedsRuntimeComparisonPreflightAlignment);
		Assert.True(report.NeedsRuntimeComparisonKeyAlignment);
		Assert.True(report.NeedsRuntimeComparisonEvidence);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedKeyMismatch
			&& row.Evidence.Contains("needsKeyAlignment=True", StringComparison.Ordinal)
			&& row.Evidence.Contains("preflightNeedsScenarioAlignment=True", StringComparison.Ordinal)
			&& row.Notes.Contains("comparison keys do not align", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithAlignedKeyProjectionStillBlocksComparisonExecution()
	{
		var artifactReport = CreateShapeValidArtifactDirectoryReportWithMetadata("cm-teleport-animation-done");
		var csharpTrace = CreateSyntheticCSharpRuntimeTraceReport(hasLivePacketHooks: true, scenario: "cm-teleport-animation-done");
		var preflight = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			artifactReport,
			csharpTrace);
		var keyProjection = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(
			artifactReport,
			csharpTrace);

		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessReportService.Create(
			CreateRuntimeDesign(),
			CreateTraceSchema(),
			artifactReport,
			comparisonContract: null,
			preflight,
			keyProjection);

		Assert.True(report.HasRuntimeComparisonKeyProjectionReport);
		Assert.False(report.NeedsRuntimeComparisonPreflightAlignment);
		Assert.False(report.NeedsRuntimeComparisonKeyAlignment);
		Assert.True(report.NeedsRuntimeComparisonExecution);
		Assert.Contains(report.Rows, row =>
			row.Blocker == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessBlocker.RuntimeComparisonEvidence
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonReadinessStatus.BlockedComparisonNotExecuted
			&& row.Evidence.Contains("needsComparisonExecution=True", StringComparison.Ordinal)
			&& row.Notes.Contains("key projection has not executed deterministic", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReport CreateTraceSchema() =>
		PlayerProtectionActiveTaskStopTriggerTraceArtifactSchemaReportService.Create(CreateRuntimeDesign());

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReport CreateRuntimeDesign() =>
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonDesignReportService.Create(CreateDetailedSummary());

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport CreateSyntheticCSharpRuntimeTraceReport(
		bool hasLivePacketHooks,
		params string[] scenarios)
	{
		var scenarioRows = scenarios.Length == 0
			? ["cm-teleport-animation-done"]
			: scenarios;

		return PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
			scenarioRows.Select((scenario, index) => CreateSyntheticCSharpTraceRow(index, scenario)).ToArray(),
			hasLivePacketHooks,
			"synthetic C# trace report only");
	}

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport CreateSyntheticCSharpRuntimeTraceReport(
		bool hasLivePacketHooks,
		string scenario) =>
		CreateSyntheticCSharpRuntimeTraceReport(hasLivePacketHooks, [scenario]);

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceRow CreateSyntheticCSharpTraceRow(int eventSeq, string scenario) =>
		new(
			EventSeq: eventSeq,
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

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport CreateShapeValidArtifactDirectoryReport(
		string scenarioName = "teleport-animation-done-no-op") =>
		new(
			PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
			[
				new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow(
					$"{scenarioName}.json",
					new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport(
						[],
						IsValidSchemaV1: true,
						ReadyForRuntimeComparison: false,
						"shape-valid only",
						Metadata: null))
			],
			HasGeneratedJavaArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid generated Java artifact JSON only");

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
							TraceRows: new[]
							{
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
							})))
			],
			HasGeneratedJavaArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid generated Java artifact metadata only");

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport CreateInvalidArtifactDirectoryReport() =>
		new(
			PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.InvalidArtifacts,
			[
				new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow(
					"teleport-animation-done-invalid.json",
					new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport(
						[
							new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue(
								PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingTraceRows,
								"$.traces",
								"Expected at least one trace row.")
						],
						IsValidSchemaV1: false,
						ReadyForRuntimeComparison: false,
						"invalid schema-v1 artifact",
						Metadata: null))
			],
			HasGeneratedJavaArtifacts: true,
			ReadyForRuntimeComparison: false,
			"invalid generated Java artifact JSON");

	private static PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReport CreateDetailedSummary() =>
		PlayerProtectionActiveTaskFirstActionStopTriggerSummaryReportService.Create(
			PlayerProtectionActiveTaskFirstActionStopTriggerAuditService.Create(CreateBaseRequest(
				packetX: 101f,
				evaluateCmMoveInAir: true,
				evaluateCmAttack: true,
				evaluateCmCastSpell: true,
				evaluateCmUseItem: true,
				evaluateCmShowDialog: true,
				evaluateCmDialogSelect: true,
				evaluateCmCompositeStones: true,
				evaluateCmEmotion: true)));

	private static PlayerProtectionActiveTaskFirstActionStopTriggerAuditRequest CreateBaseRequest(
		float packetX = CurrentX,
		bool evaluateCmMoveInAir = false,
		bool evaluateCmAttack = false,
		bool evaluateCmCastSpell = false,
		bool evaluateCmUseItem = false,
		bool evaluateCmShowDialog = false,
		bool evaluateCmDialogSelect = false,
		bool evaluateCmCompositeStones = false,
		bool evaluateCmEmotion = false) =>
		new(
			PlayerSpawned: true,
			AntiHackAccepted: true,
			TeleportationModeAbsoluteMove: false,
			PlayerProtectionActive: true,
			CurrentX,
			CurrentY,
			CurrentZ,
			packetX,
			CurrentY,
			CurrentZ,
			EvaluateCmMoveInAir: evaluateCmMoveInAir,
			EvaluateCmAttack: evaluateCmAttack,
			EvaluateCmCastSpell: evaluateCmCastSpell,
			EvaluateCmUseItem: evaluateCmUseItem,
			EvaluateCmShowDialog: evaluateCmShowDialog,
			EvaluateCmDialogSelect: evaluateCmDialogSelect,
			EvaluateCmCompositeStones: evaluateCmCompositeStones,
			EvaluateCmEmotion: evaluateCmEmotion);

	private const float CurrentX = 100f;
	private const float CurrentY = 200f;
	private const float CurrentZ = 50f;
}
