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
			new PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport(
				["CM_TELEPORT_ANIMATION_DONE"],
				HasLivePacketHooks: true,
				ReadyForRuntimeComparison: false,
				"synthetic C# trace report only"));

		Assert.True(report.HasCSharpRuntimeTraceReport);
		Assert.False(report.NeedsJavaArtifacts);
		Assert.False(report.NeedsCSharpRuntimeTrace);
		Assert.True(report.NeedsExecutedComparison);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.CSharpRuntimeTraceOutput
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.SatisfiedByNonLiveMetadata
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
			new PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport(
				["CM_MOVE"],
				HasLivePacketHooks: false,
				ReadyForRuntimeComparison: false,
				"design-only C# trace placeholder"));

		Assert.True(report.NeedsCSharpRuntimeTrace);
		Assert.Contains(report.Rows, row =>
			row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.CSharpRuntimeTraceOutput
			&& row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedMissingCSharpRuntimeTrace
			&& row.Evidence.Contains("hasLivePacketHooks=False", StringComparison.Ordinal));
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
}
