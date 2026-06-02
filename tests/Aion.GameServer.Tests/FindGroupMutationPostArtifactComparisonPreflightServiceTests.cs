using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostArtifactComparisonPreflightServiceTests
{
	[Fact]
	public void Create_DefaultPreflightBlocksOnMissingJavaArtifacts()
	{
		var report = FindGroupMutationPostArtifactComparisonPreflightService.Create();

		Assert.Equal(FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingJavaArtifacts, report.Status);
		Assert.False(report.IsLive);
		Assert.True(report.HasJavaArtifactTargets);
		Assert.False(report.HasShapeValidJavaArtifacts);
		Assert.True(report.HasComparisonKeyProjection);
		Assert.False(report.HasLiveCSharpTraceRows);
		Assert.False(report.HasRegistryObservation);
		Assert.False(report.HasComparisonExecution);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.True(report.NeedsGeneratedJavaArtifacts);
		Assert.True(report.NeedsLiveCSharpTraceRows);
		Assert.True(report.NeedsRegistryObservation);
		Assert.True(report.NeedsComparisonExecution);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
	}

	[Fact]
	public void Create_RecordsArtifactTargetsKeyProjectionAndRegistryContractRows()
	{
		var report = FindGroupMutationPostArtifactComparisonPreflightService.Create();

		Assert.Equal(Enumerable.Range(1, report.Rows.Count), report.Rows.Select(row => row.Order));
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.JavaArtifactTargets
			&& row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByNonLiveMetadata
			&& row.Evidence.Contains("action-{action}-java.json", StringComparison.Ordinal)
			&& row.Evidence.Contains("actions=2/6", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.ComparisonKeyProjection
			&& row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByNonLiveMetadata
			&& row.Evidence.Contains("ignoredRuntimeFields=traceSource/serverEpochSeconds", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.RegistryObservation
			&& row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingRegistryObservation
			&& row.Evidence.Contains("requiresOrderedSends=True", StringComparison.Ordinal)
			&& row.Notes.Contains("posted system message before refreshed list", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ShapeValidJavaArtifactsMoveBlockerToLiveCSharpRows()
	{
		var report = FindGroupMutationPostArtifactComparisonPreflightService.Create(
			javaArtifacts: ShapeValidJavaArtifacts());

		Assert.Equal(FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingLiveCSharpRows, report.Status);
		Assert.True(report.HasShapeValidJavaArtifacts);
		Assert.False(report.NeedsGeneratedJavaArtifacts);
		Assert.True(report.NeedsLiveCSharpTraceRows);
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.JavaArtifactReader
			&& row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByShapeValidArtifact
			&& row.Evidence.Contains("shapeValid=True", StringComparison.Ordinal)
			&& row.Notes.Contains("shape-valid only", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithJavaArtifactsAndLiveRowsStillRequiresRegistryObservation()
	{
		var report = FindGroupMutationPostArtifactComparisonPreflightService.Create(
			javaArtifacts: ShapeValidJavaArtifacts(),
			hasLiveCSharpTraceRows: true);

		Assert.Equal(FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingRegistryObservation, report.Status);
		Assert.True(report.HasLiveCSharpTraceRows);
		Assert.False(report.NeedsLiveCSharpTraceRows);
		Assert.True(report.NeedsRegistryObservation);
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.CSharpLiveTraceRows
			&& row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByLiveEvidence
			&& row.Evidence.Contains("hasLiveCSharpTraceRows=True", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithPrerequisitesStillBlocksUntilComparisonExecutes()
	{
		var report = FindGroupMutationPostArtifactComparisonPreflightService.Create(
			javaArtifacts: ShapeValidJavaArtifacts(),
			hasLiveCSharpTraceRows: true,
			hasRegistryObservation: true);

		Assert.Equal(FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedComparisonNotExecuted, report.Status);
		Assert.False(report.NeedsGeneratedJavaArtifacts);
		Assert.False(report.NeedsLiveCSharpTraceRows);
		Assert.False(report.NeedsRegistryObservation);
		Assert.True(report.NeedsComparisonExecution);
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.ComparisonExecution
			&& row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedComparisonNotExecuted
			&& row.Evidence.Contains("prerequisitesReady=True", StringComparison.Ordinal)
			&& row.Notes.Contains("Verified parity cannot be claimed", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ComparisonExecutionMustMatchProjectedRowsBeforeReady()
	{
		var mismatched = FindGroupMutationPostArtifactComparisonPreflightService.Create(
			javaArtifacts: ShapeValidJavaArtifacts(),
			hasLiveCSharpTraceRows: true,
			hasRegistryObservation: true,
			comparisonExecuted: true,
			hasMatchingComparisonResult: false);

		Assert.Equal(FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedComparisonNotMatching, mismatched.Status);
		Assert.False(mismatched.ReadyForRuntimeComparison);

		var matched = FindGroupMutationPostArtifactComparisonPreflightService.Create(
			javaArtifacts: ShapeValidJavaArtifacts(),
			hasLiveCSharpTraceRows: true,
			hasRegistryObservation: true,
			comparisonExecuted: true,
			hasMatchingComparisonResult: true);

		Assert.Equal(FindGroupMutationPostArtifactComparisonPreflightStatus.Ready, matched.Status);
		Assert.True(matched.ReadyForRuntimeComparison);
		Assert.DoesNotContain(matched.Rows, row => row.BlocksRuntimeComparison);
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryReport ShapeValidJavaArtifacts() =>
		new(
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid,
			FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot,
			[
				ShapeValidFile(2),
				ShapeValidFile(6),
			],
			HasGeneratedJavaArtifacts: true,
			HasAllExpectedFiles: true,
			HasOnlyShapeValidArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid only");

	private static FindGroupMutationPostJavaTraceArtifactDirectoryFileRow ShapeValidFile(int action) =>
		new(
			action,
			FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(action),
			FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
			new FindGroupMutationPostJavaTraceArtifactValidationReport(
				[],
				IsValid: true,
				new FindGroupMutationPostJavaTraceArtifactMetadata(
					SchemaVersion: 1,
					TraceName: "cm-find-group-direct-mutation-post-boundary",
					[
						new FindGroupMutationPostJavaTraceArtifactValidationTraceRow(
							SchemaVersion: 1,
							TraceName: "cm-find-group-direct-mutation-post-boundary",
							TraceSource: "Java",
							action,
							MutationKind: action == 2 ? "Recruitment" : "Application",
							PostedSystemMessageId: action == 2 ? 1400392 : 1400393,
							RefreshedListAction: action == 2 ? 0 : 4)
					])),
			"shape-valid only");
}
