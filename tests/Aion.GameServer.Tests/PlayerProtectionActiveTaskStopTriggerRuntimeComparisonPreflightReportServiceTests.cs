using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportServiceTests
{
	[Fact]
	public void Create_MissingJavaArtifactsBlocksPreflight()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			javaArtifacts: null,
			CreateCSharpTraceReport("cm-teleport-animation-done"));

		Assert.False(report.IsLive);
		Assert.False(report.HasShapeValidJavaArtifacts);
		Assert.True(report.NeedsJavaArtifacts);
		Assert.True(report.NeedsScenarioAlignment);
		Assert.True(report.NeedsRowCountAlignment);
		Assert.True(report.NeedsComparisonExecution);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.JavaArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedMissingJavaArtifact);
	}

	[Fact]
	public void Create_InvalidJavaArtifactsBlocksPreflightWithInvalidStatus()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			CreateJavaArtifacts(PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.InvalidArtifacts, "cm-teleport-animation-done"),
			CreateCSharpTraceReport("cm-teleport-animation-done"));

		Assert.True(report.NeedsJavaArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.JavaArtifacts
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedInvalidJavaArtifact);
	}

	[Fact]
	public void Create_InvalidCSharpTraceRowsBlocksPreflight()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			CreateJavaArtifacts(PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid, "cm-teleport-animation-done"),
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
				[CreateTraceRow("cm-teleport-animation-done", timestampIsParityKey: true)],
				hasLivePacketHooks: true,
				"invalid C# trace"));

		Assert.True(report.HasShapeValidJavaArtifacts);
		Assert.False(report.HasValidCSharpTraceRows);
		Assert.True(report.NeedsCSharpTraceRows);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.CSharpRuntimeTraceRows
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedMissingCSharpRuntimeTrace
			&& row.Evidence.Contains("validationIssues=1", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ScenarioMismatchBlocksPreflight()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			CreateJavaArtifacts(PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid, "cm-move-threshold"),
			CreateCSharpTraceReport("cm-teleport-animation-done"));

		Assert.True(report.HasValidCSharpTraceRows);
		Assert.True(report.NeedsScenarioAlignment);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.ScenarioAlignment
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedScenarioMismatch
			&& row.Evidence.Contains("java=[cm-move-threshold]", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharp=[cm-teleport-animation-done]", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RowCountMismatchBlocksPreflight()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			CreateJavaArtifacts(
				PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
				"cm-move-threshold",
				"cm-teleport-animation-done"),
			CreateCSharpTraceReport("cm-move-threshold", "cm-teleport-animation-done", "cm-teleport-animation-done"));

		Assert.True(report.HasScenarioAlignment);
		Assert.True(report.NeedsRowCountAlignment);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.RowCountAlignment
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedRowCountMismatch
			&& row.Evidence.Contains("javaFiles=2", StringComparison.Ordinal)
			&& row.Evidence.Contains("csharpRows=3", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_AlignedSyntheticInputsStillBlocksComparisonExecution()
	{
		var report = PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService.Create(
			CreateJavaArtifacts(
				PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
				"cm-move-threshold",
				"cm-teleport-animation-done"),
			CreateCSharpTraceReport("cm-move-threshold", "cm-teleport-animation-done"));

		Assert.True(report.HasShapeValidJavaArtifacts);
		Assert.True(report.HasValidCSharpTraceRows);
		Assert.True(report.HasScenarioAlignment);
		Assert.True(report.HasRowCountAlignment);
		Assert.False(report.NeedsJavaArtifacts);
		Assert.False(report.NeedsCSharpTraceRows);
		Assert.False(report.NeedsScenarioAlignment);
		Assert.False(report.NeedsRowCountAlignment);
		Assert.True(report.NeedsComparisonExecution);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.ComparisonExecution
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedComparisonNotExecuted
			&& row.Notes.Contains("Verified parity cannot be claimed", StringComparison.Ordinal));
	}

	private static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport CreateJavaArtifacts(
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus status,
		params string[] scenarioNames) =>
		new(
			status,
			scenarioNames
				.Select(name => new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow(
					$"{name}.json",
					new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport(
						status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.InvalidArtifacts
							? [new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssue(
								PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.UnsupportedSchemaVersion,
								"$.schemaVersion",
								"Expected schemaVersion 1.")]
							: [],
						IsValidSchemaV1: status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
						ReadyForRuntimeComparison: false,
						"synthetic Java artifact row")))
				.ToArray(),
			HasGeneratedJavaArtifacts: scenarioNames.Length > 0,
			ReadyForRuntimeComparison: false,
			$"synthetic Java artifact report status={status}");

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport CreateCSharpTraceReport(params string[] scenarioNames) =>
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService.CreateCSharpRuntimeTraceReport(
			scenarioNames.Select((scenario, index) => CreateTraceRow(scenario, eventSeq: index)).ToArray(),
			hasLivePacketHooks: true,
			"synthetic C# trace rows");

	private static PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceRow CreateTraceRow(
		string scenario,
		int eventSeq = 0,
		bool timestampIsParityKey = false) =>
		new(
			EventSeq: eventSeq,
			Scenario: scenario,
			Phase: "teleport_task_remove",
			PacketName: "CM_TELEPORT_ANIMATION_DONE",
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
