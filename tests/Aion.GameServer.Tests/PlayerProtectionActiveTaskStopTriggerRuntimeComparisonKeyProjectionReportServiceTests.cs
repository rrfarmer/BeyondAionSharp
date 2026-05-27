using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportServiceTests
{
	[Fact]
	public void Create_AlignedParsedKeysStillBlocksComparisonExecution()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(
			CreateJavaArtifacts(("cm-teleport-animation-done", CreateJavaTraceRow())),
			CreateCSharpTraceReport(CreateCSharpTraceRow("cm-teleport-animation-done")));

		Assert.True(report.HasJavaKeys);
		Assert.True(report.HasCSharpKeys);
		Assert.True(report.HasKeyAlignment);
		Assert.False(report.NeedsJavaKeys);
		Assert.False(report.NeedsCSharpKeys);
		Assert.False(report.NeedsKeyAlignment);
		Assert.True(report.NeedsComparisonExecution);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal("java", report.JavaKeys[0].Source);
		Assert.Equal("csharp", report.CSharpKeys[0].Source);
		Assert.Equal(report.JavaKeys[0].Fingerprint, report.CSharpKeys[0].Fingerprint);
	}

	[Fact]
	public void Create_ScenarioMismatchBlocksKeyAlignment()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(
			CreateJavaArtifacts(("cm-move-threshold", CreateJavaTraceRow())),
			CreateCSharpTraceReport(CreateCSharpTraceRow("cm-teleport-animation-done")));

		Assert.True(report.NeedsKeyAlignment);
		Assert.Contains(report.Rows, row =>
			row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedKeyMismatch
			&& row.Evidence.Contains("javaOnly=", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharpOnly=", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_EventSequenceMismatchBlocksKeyAlignment()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(
			CreateJavaArtifacts(("cm-teleport-animation-done", CreateJavaTraceRow(eventSeq: 0))),
			CreateCSharpTraceReport(CreateCSharpTraceRow("cm-teleport-animation-done", eventSeq: 1)));

		Assert.True(report.NeedsKeyAlignment);
		Assert.Contains(report.Rows, row =>
			row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedKeyMismatch);
	}

	[Fact]
	public void Create_StopFlagMismatchBlocksKeyAlignment()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(
			CreateJavaArtifacts(("cm-teleport-animation-done", CreateJavaTraceRow(stopCalled: false))),
			CreateCSharpTraceReport(CreateCSharpTraceRow("cm-teleport-animation-done", stopCalled: true)));

		Assert.True(report.NeedsKeyAlignment);
		Assert.Contains(report.Rows, row =>
			row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedKeyMismatch);
	}

	[Fact]
	public void Create_PlayerSnapshotMismatchBlocksKeyAlignment()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(
			CreateJavaArtifacts(("cm-teleport-animation-done", CreateJavaTraceRow(spawned: false))),
			CreateCSharpTraceReport(CreateCSharpTraceRow("cm-teleport-animation-done", spawned: true)));

		Assert.True(report.NeedsKeyAlignment);
		Assert.Contains(report.Rows, row =>
			row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedKeyMismatch);
	}

	[Fact]
	public void Create_TimestampParityValidationIssueBlocksCSharpKeys()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService.Create(
			CreateJavaArtifacts(("cm-teleport-animation-done", CreateJavaTraceRow())),
			CreateCSharpTraceReport(CreateCSharpTraceRow("cm-teleport-animation-done", timestampIsParityKey: true)));

		Assert.True(report.NeedsCSharpKeys);
		Assert.False(report.HasKeyAlignment);
		Assert.Contains(report.Rows, row =>
			row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedMissingCSharpKeys
			&& row.Evidence.Contains("validationIssues=1", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport CreateJavaArtifacts(
		params (string Scenario, PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactTraceRow Row)[] artifacts) =>
		new(
			PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
			artifacts
				.Select(artifact => new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow(
					$"{artifact.Scenario}.json",
					new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport(
						[],
						IsValidSchemaV1: true,
						ReadyForRuntimeComparison: false,
						"synthetic Java metadata",
						new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactMetadata(
							SchemaVersion: 1,
							JavaCommit: "abcdef1",
							Scenario: artifact.Scenario,
							RuntimePacketName: "CM_TELEPORT_ANIMATION_DONE",
							RuntimeExpectedReturnReason: "animation_done_no_pending_runnable_teleport_task",
							TraceRows: [artifact.Row]))))
				.ToArray(),
			HasGeneratedJavaArtifacts: true,
			ReadyForRuntimeComparison: false,
			"synthetic shape-valid Java artifact report");

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactTraceRow CreateJavaTraceRow(
		int eventSeq = 0,
		bool stopCalled = false,
		bool spawned = false) =>
		new(
			EventSeq: eventSeq,
			Phase: "teleport_task_remove",
			PacketName: "CM_TELEPORT_ANIMATION_DONE",
			ReturnReason: "animation_done_no_pending_runnable_teleport_task",
			StopCalled: stopCalled,
			ExpectsStopProtectionCall: false,
			TimestampIsParityKey: false,
			Player: new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactPlayerSnapshot(
				ObjectId: 1001,
				Spawned: spawned,
				Flying: false,
				Dead: false,
				ProtectionActiveBefore: true,
				ProtectionActiveAfter: true,
				VisualStateBefore: ["BLINKING"],
				VisualStateAfter: ["BLINKING"]));

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport CreateCSharpTraceReport(
		params PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceRow[] rows) =>
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
			rows,
			hasLivePacketHooks: true,
			"synthetic C# trace rows");

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceRow CreateCSharpTraceRow(
		string scenario,
		int eventSeq = 0,
		bool stopCalled = false,
		bool spawned = false,
		bool timestampIsParityKey = false) =>
		new(
			EventSeq: eventSeq,
			Scenario: scenario,
			Phase: "teleport_task_remove",
			PacketName: "CM_TELEPORT_ANIMATION_DONE",
			ReturnReason: "animation_done_no_pending_runnable_teleport_task",
			StopCalled: stopCalled,
			ExpectsStopProtectionCall: false,
			TimestampIsParityKey: timestampIsParityKey,
			new PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTracePlayerSnapshot(
				ObjectId: 1001,
				Spawned: spawned,
				Flying: false,
				Dead: false,
				ProtectionActiveBefore: true,
				ProtectionActiveAfter: true,
				VisualStateBefore: ["BLINKING"],
				VisualStateAfter: ["BLINKING"]));
}
