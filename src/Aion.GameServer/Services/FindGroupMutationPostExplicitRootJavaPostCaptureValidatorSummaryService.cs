namespace Aion.GameServer.Services;

public enum FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus
{
	BlockedMissingExplicitRoot,
	BlockedRepositoryArtifactRoot,
	BlockedMissingDirectory,
	BlockedMissingExpectedFiles,
	BlockedInvalidArtifacts,
	ShapeValidRuntimeComparisonBlocked,
}

public sealed record FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryRow(
	int Action,
	string ArtifactPath,
	FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus FileStatus,
	bool HasFile,
	bool IsShapeValid,
	int TraceRowCount,
	int ValidationIssueCount,
	string Notes);

public sealed record FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummary(
	FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus Status,
	string ArtifactRoot,
	IReadOnlyList<FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryRow> Rows,
	string JavaCaptureCommand,
	string CSharpValidatorCommand,
	bool UsesExplicitRoot,
	bool UsesRepositoryArtifactRoot,
	bool HasGeneratedJavaArtifacts,
	bool HasAllExpectedFiles,
	bool HasOnlyShapeValidArtifacts,
	bool HasAcceptedLiveCSharpBoundaryRows,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live post-capture summary for explicit-root Java
/// CM_FIND_GROUP action 2/6 mutation-post artifacts. It consumes existing C#
/// artifact directory/validator reports and never treats shape-valid Java files
/// alone as runtime comparison or verified parity evidence.
/// </summary>
public static class FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService
{
	public static FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummary Create(
		string artifactRoot,
		FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReport? dryRunReport = null,
		FindGroupMutationPostJavaArtifactRootValidationCommandReport? rootValidationReport = null,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? directoryReport = null)
	{
		directoryReport ??= FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(artifactRoot);
		rootValidationReport ??= FindGroupMutationPostJavaArtifactRootValidationCommandReportService.Create(artifactRoot, directoryReport);
		dryRunReport ??= FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportService.Create(artifactRoot, rootValidationReport);

		var rows = directoryReport.Files
			.Select(file => new FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryRow(
				file.Action,
				file.Path,
				file.Status,
				file.Status != FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile,
				file.Status == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
				file.ValidationReport?.Metadata?.TraceRows.Count ?? 0,
				file.ValidationReport?.Issues.Count ?? 0,
				file.Notes))
			.ToArray();
		var status = StatusFor(dryRunReport, directoryReport);

		return new FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummary(
			status,
			artifactRoot,
			rows,
			dryRunReport.JavaCaptureCommand,
			rootValidationReport.CSharpValidatorCommand,
			dryRunReport.UsesExplicitRoot,
			dryRunReport.UsesRepositoryArtifactRoot,
			directoryReport.HasGeneratedJavaArtifacts,
			directoryReport.HasAllExpectedFiles,
			directoryReport.HasOnlyShapeValidArtifacts,
			HasAcceptedLiveCSharpBoundaryRows: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			rootValidationReport.TraceName,
			"Java sources reviewed: FindGroupMutationPostTraceCaptureTest.commandSuppliedArtifactRootPropertyWritesGuardedArtifacts; FindGroupMutationPostTraceCaptureArtifactValidator; FindGroupMutationPostTraceCaptureArtifactWriter; FindGroupMutationPostTraceCaptureInMemoryArtifactBridge.",
			IsLive: false);
	}

	private static FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus StatusFor(
		FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReport dryRunReport,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport directoryReport)
	{
		if (!dryRunReport.UsesExplicitRoot)
			return FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedMissingExplicitRoot;
		if (dryRunReport.UsesRepositoryArtifactRoot)
			return FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedRepositoryArtifactRoot;

		return directoryReport.Status switch
		{
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.MissingDirectory => FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedMissingDirectory,
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.MissingExpectedFiles => FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedMissingExpectedFiles,
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.InvalidArtifacts => FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedInvalidArtifacts,
			_ => FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.ShapeValidRuntimeComparisonBlocked,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus status)
	{
		return status switch
		{
			FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedMissingExplicitRoot => "Post-capture validation is blocked because no explicit artifact root was supplied.",
			FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedRepositoryArtifactRoot => "Post-capture validation is blocked because the repository artifact root is not an isolated explicit capture root.",
			FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedMissingDirectory => "Post-capture validation is blocked because the explicit artifact root directory is missing.",
			FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedMissingExpectedFiles => "Post-capture validation is blocked because one or both expected action 2/6 Java artifacts are missing.",
			FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryStatus.BlockedInvalidArtifacts => "Post-capture validation is blocked because one or more generated Java artifacts failed schema/action validation.",
			_ => "Explicit-root Java artifacts are shape-valid only; accepted live C# boundary rows and runtime comparison evidence are still required before parity can be claimed.",
		};
	}
}
