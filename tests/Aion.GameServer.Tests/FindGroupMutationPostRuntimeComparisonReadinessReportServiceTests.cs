using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostRuntimeComparisonReadinessReportServiceTests
{
	[Fact]
	public void Create_DefaultAggregateKeepsRuntimeComparisonBlocked()
	{
		var report = FindGroupMutationPostRuntimeComparisonReadinessReportService.Create();

		Assert.False(report.IsLive);
		Assert.True(report.HasTraceSchema);
		Assert.True(report.HasJavaInstrumentationDesign);
		Assert.True(report.HasJavaArtifactReader);
		Assert.False(report.HasShapeValidJavaArtifacts);
		Assert.True(report.HasCSharpTraceEmitterDesign);
		Assert.True(report.NeedsGeneratedJavaArtifacts);
		Assert.True(report.NeedsLiveBoundaryCapture);
		Assert.True(report.NeedsCSharpRuntimeTrace);
		Assert.True(report.NeedsComparisonExecution);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Equal(Enumerable.Range(1, report.Rows.Count), report.Rows.Select(row => row.Order));
	}

	[Fact]
	public void Create_SatisfiesNonLiveSchemaInstrumentationAndEmitterMetadataRows()
	{
		var report = FindGroupMutationPostRuntimeComparisonReadinessReportService.Create();

		Assert.Contains(report.Rows, row =>
			row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.MutationPostTraceSchema
			&& row.Status == FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata
			&& !row.BlocksRuntimeComparison
			&& row.Evidence.Contains("fields=22", StringComparison.Ordinal)
			&& row.Evidence.Contains("actions=2/6", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.JavaInstrumentationDesign
			&& row.Status == FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata
			&& row.Evidence.Contains("coversActionsTwoAndSix=True", StringComparison.Ordinal)
			&& row.Notes.Contains("no Java hooks or serializer", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.CSharpTraceEmitterDesign
			&& row.Status == FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata
			&& row.Evidence.Contains("requiresLiveEmitter=True", StringComparison.Ordinal)
			&& row.Notes.Contains("live runtime rows are still missing", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingGeneratedJavaArtifactsRemainABlocker()
	{
		var report = FindGroupMutationPostRuntimeComparisonReadinessReportService.Create();

		Assert.Contains(report.Rows, row =>
			row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.JavaArtifactReader
			&& row.Status == FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedMissingJavaArtifact
			&& row.BlocksRuntimeComparison
			&& row.Evidence.Contains("hasGenerated=False", StringComparison.Ordinal)
			&& row.Notes.Contains("missing or invalid", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithShapeValidJavaArtifactsClearsArtifactBlockerButKeepsRuntimeBlocked()
	{
		var report = FindGroupMutationPostRuntimeComparisonReadinessReportService.Create(
			javaArtifactReader: ShapeValidReader());

		Assert.True(report.HasShapeValidJavaArtifacts);
		Assert.False(report.NeedsGeneratedJavaArtifacts);
		Assert.True(report.NeedsLiveBoundaryCapture);
		Assert.True(report.NeedsComparisonExecution);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Contains(report.Rows, row =>
			row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.JavaArtifactReader
			&& row.Status == FindGroupMutationPostRuntimeComparisonReadinessStatus.SatisfiedByNonLiveMetadata
			&& !row.BlocksRuntimeComparison
			&& row.Evidence.Contains("shapeValid=True", StringComparison.Ordinal)
			&& row.Notes.Contains("shape-valid only", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_WithInvalidJavaArtifactsSurfacesInvalidArtifactStatus()
	{
		var report = FindGroupMutationPostRuntimeComparisonReadinessReportService.Create(
			javaArtifactReader: InvalidReader());

		Assert.True(report.NeedsGeneratedJavaArtifacts);
		Assert.Contains(report.Rows, row =>
			row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.JavaArtifactReader
			&& row.Status == FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedInvalidJavaArtifact
			&& row.Evidence.Contains("status=InvalidArtifacts", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_LiveBoundaryAndComparisonRowsPreventParityClaim()
	{
		var report = FindGroupMutationPostRuntimeComparisonReadinessReportService.Create(
			javaArtifactReader: ShapeValidReader());

		Assert.Contains(report.Rows, row =>
			row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.LiveCSharpBoundaryCapture
			&& row.Status == FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedMissingLiveBoundaryCapture
			&& row.BlocksRuntimeComparison
			&& row.CSharpTarget.Contains("GameServerConnection.ProcessPacketAsync", StringComparison.Ordinal)
			&& row.Notes.Contains("registry send ordering", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Blocker == FindGroupMutationPostRuntimeComparisonReadinessBlocker.RuntimeComparisonExecution
			&& row.Status == FindGroupMutationPostRuntimeComparisonReadinessStatus.BlockedComparisonNotExecuted
			&& row.BlocksRuntimeComparison
			&& row.Notes.Contains("Verified parity cannot be claimed", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryReport ShapeValidReader() =>
		new(
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid,
			"parity-artifacts/find-group/mutation-post/java",
			[
				ShapeValidFile(2),
				ShapeValidFile(6),
			],
			HasGeneratedJavaArtifacts: true,
			HasAllExpectedFiles: true,
			HasOnlyShapeValidArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid only");

	private static FindGroupMutationPostJavaTraceArtifactDirectoryReport InvalidReader() =>
		new(
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.InvalidArtifacts,
			"parity-artifacts/find-group/mutation-post/java",
			[
				new FindGroupMutationPostJavaTraceArtifactDirectoryFileRow(
					2,
					"action-2.json",
					FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.InvalidArtifact,
					new FindGroupMutationPostJavaTraceArtifactValidationReport(
						[
							new FindGroupMutationPostJavaTraceArtifactValidationIssue(
								FindGroupMutationPostJavaTraceArtifactValidationIssueCode.UnsupportedSchemaVersion,
								"$.schemaVersion",
								"Expected schemaVersion 1.")
						],
						IsValid: false,
						Metadata: null),
					"invalid artifact")
			],
			HasGeneratedJavaArtifacts: true,
			HasAllExpectedFiles: true,
			HasOnlyShapeValidArtifacts: false,
			ReadyForRuntimeComparison: false,
			"invalid artifact");

	private static FindGroupMutationPostJavaTraceArtifactDirectoryFileRow ShapeValidFile(int action) =>
		new(
			action,
			$"action-{action}.json",
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
