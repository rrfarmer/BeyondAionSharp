using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractServiceTests
{
	[Fact]
	public void Create_WithoutJavaArtifactReportBlocksBeforeComparison()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.Create(
			javaArtifactDirectoryReport: null);

		Assert.False(report.IsLive);
		Assert.False(report.HasJavaArtifactDirectoryReport);
		Assert.False(report.HasShapeValidJavaArtifacts);
		Assert.True(report.NeedsJavaArtifacts);
		Assert.True(report.NeedsCSharpRuntimeTrace);
		Assert.True(report.NeedsExecutedComparison);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.JavaTraceArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedMissingJavaArtifact
			&& row.Evidence.Contains("no Java artifact directory report", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithInvalidJavaArtifactReportSurfacesInvalidArtifactBlocker()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.Create(
			CreateInvalidJavaArtifactDirectoryReport());

		Assert.True(report.HasJavaArtifactDirectoryReport);
		Assert.False(report.HasShapeValidJavaArtifacts);
		Assert.True(report.NeedsJavaArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.JavaTraceArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedInvalidJavaArtifact
			&& row.Evidence.Contains("status=InvalidArtifacts", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithShapeValidJavaArtifactsStillBlocksMissingCSharpRuntimeTrace()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.Create(
			CreateShapeValidJavaArtifactDirectoryReport());

		Assert.True(report.HasJavaArtifactDirectoryReport);
		Assert.True(report.HasShapeValidJavaArtifacts);
		Assert.False(report.NeedsJavaArtifacts);
		Assert.True(report.NeedsCSharpRuntimeTrace);
		Assert.True(report.NeedsExecutedComparison);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.JavaTraceArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.SatisfiedByNonLiveMetadata
			&& !row.BlocksRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.CSharpRuntimeTraceOutput
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedMissingCSharpRuntimeTrace
			&& row.Notes.Contains("Live C# packet hooks", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithSyntheticCSharpRuntimeTraceStillBlocksComparisonExecution()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.Create(
			CreateShapeValidJavaArtifactDirectoryReport(),
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
				[CreateTraceRow()],
				hasLivePacketHooks: true,
				"synthetic C# trace report only"));

		Assert.True(report.HasCSharpRuntimeTraceReport);
		Assert.False(report.NeedsJavaArtifacts);
		Assert.False(report.NeedsCSharpRuntimeTrace);
		Assert.True(report.NeedsExecutedComparison);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.CSharpRuntimeTraceOutput
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.SatisfiedByNonLiveMetadata
			&& row.Evidence.Contains("rows=1", StringComparison.Ordinal)
			&& row.Evidence.Contains("hasLivePacketHooks=True", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.ComparisonExecution
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedComparisonNotExecuted
			&& row.Notes.Contains("Verified parity cannot be claimed", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithSyntheticCSharpTraceWithoutLiveHooksStillBlocksCSharpTraceReadiness()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.Create(
			CreateShapeValidJavaArtifactDirectoryReport(),
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
				[CreateTraceRow(packetName: "CM_MOVE")],
				hasLivePacketHooks: false,
				"design-only C# trace placeholder"));

		Assert.True(report.NeedsCSharpRuntimeTrace);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.CSharpRuntimeTraceOutput
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedMissingCSharpRuntimeTrace
			&& row.Evidence.Contains("hasLivePacketHooks=False", StringComparison.Ordinal));
	}

	[Fact]
	public void CreateCSharpRuntimeTraceReport_WithValidRowsDerivesScenariosAndKeepsRuntimeBlocked()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
			[
				CreateTraceRow(eventSeq: 0, scenario: "cm-move-threshold"),
				CreateTraceRow(eventSeq: 1, scenario: "cm-teleport-animation-done", packetName: "CM_TELEPORT_ANIMATION_DONE")
			],
			hasLivePacketHooks: true,
			"synthetic row schema fixture");

		Assert.Equal(["cm-move-threshold", "cm-teleport-animation-done"], report.Scenarios);
		Assert.Equal(2, report.TraceRows.Count);
		Assert.Empty(report.ValidationIssues);
		Assert.True(report.HasLivePacketHooks);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal("teleport_task_remove", report.TraceRows[0].Phase);
		Assert.Equal("animation_done_no_pending_runnable_teleport_task", report.TraceRows[0].ReturnReason);
		Assert.False(report.TraceRows[0].TimestampIsParityKey);
		Assert.True(report.TraceRows[0].Player.ProtectionActiveBefore);
	}

	[Fact]
	public void CreateCSharpRuntimeTraceReport_RejectsOutOfOrderEventSeq()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
			[
				CreateTraceRow(eventSeq: 1),
				CreateTraceRow(eventSeq: 1, packetName: "CM_MOVE")
			],
			hasLivePacketHooks: true,
			"out-of-order fixture");

		Assert.Contains(report.ValidationIssues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceValidationIssueCode.OutOfOrderEventSequence
			&& issue.Path == "$.traceRows[1].eventSeq");
	}

	[Fact]
	public void CreateCSharpRuntimeTraceReport_RejectsTimestampParityKeys()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
			[CreateTraceRow(timestampIsParityKey: true)],
			hasLivePacketHooks: true,
			"timestamp parity-key fixture");

		Assert.Contains(report.ValidationIssues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceValidationIssueCode.TimestampMarkedAsParityKey
			&& issue.Path == "$.traceRows[0].timestampIsParityKey");
	}

	[Fact]
	public void Create_WithInvalidCSharpRuntimeTraceRowsKeepsCSharpTraceBlocked()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.Create(
			CreateShapeValidJavaArtifactDirectoryReport(),
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
				[CreateTraceRow(timestampIsParityKey: true)],
				hasLivePacketHooks: true,
				"invalid C# trace row fixture"));

		Assert.True(report.NeedsCSharpRuntimeTrace);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.CSharpRuntimeTraceOutput
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedMissingCSharpRuntimeTrace
			&& row.Evidence.Contains("validationIssues=1", StringComparison.Ordinal)
			&& row.Notes.Contains("TimestampMarkedAsParityKey", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport CreateShapeValidJavaArtifactDirectoryReport() =>
		new(
			PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
			[
				new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow(
					"teleport-animation-done-no-op.json",
					new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport(
						[],
						IsValidSchemaV1: true,
						ReadyForRuntimeComparison: false,
						"shape-valid only"))
			],
			HasGeneratedJavaArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid generated Java artifact JSON only");

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport CreateInvalidJavaArtifactDirectoryReport() =>
		new(
			PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.InvalidArtifacts,
			[
				new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow(
					"teleport-animation-done-invalid.json",
					new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport(
						[
							new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue(
								PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.UnsupportedSchemaVersion,
								"$.schemaVersion",
								"Expected schemaVersion 1.")
						],
						IsValidSchemaV1: false,
						ReadyForRuntimeComparison: false,
						"invalid schema-v1 artifact"))
			],
			HasGeneratedJavaArtifacts: true,
			ReadyForRuntimeComparison: false,
			"invalid generated Java artifact JSON");

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceRow CreateTraceRow(
		int eventSeq = 0,
		string scenario = "cm-teleport-animation-done",
		string packetName = "CM_TELEPORT_ANIMATION_DONE",
		bool timestampIsParityKey = false) =>
		new(
			EventSeq: eventSeq,
			Scenario: scenario,
			Phase: "teleport_task_remove",
			PacketName: packetName,
			ReturnReason: "animation_done_no_pending_runnable_teleport_task",
			StopCalled: false,
			ExpectsStopProtectionCall: false,
			TimestampIsParityKey: timestampIsParityKey,
			new PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTracePlayerSnapshot(
				ObjectId: 1001,
				Spawned: false,
				Flying: false,
				Dead: false,
				ProtectionActiveBefore: true,
				ProtectionActiveAfter: true,
				VisualStateBefore: ["BLINKING"],
				VisualStateAfter: ["BLINKING"]));
}
