namespace Aion.GameServer.Services;

public enum FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus
{
	BlockedMissingDirectory,
	BlockedMissingExpectedFiles,
	BlockedInvalidArtifacts,
	ShapeValidRuntimeComparisonBlocked,
}

public sealed record FindGroupMutationPostJavaArtifactRootValidationCommandRow(
	int Action,
	string ArtifactPath,
	FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus FileStatus,
	bool HasFile,
	bool IsShapeValid,
	string ValidatorTarget,
	string Notes);

public sealed record FindGroupMutationPostJavaArtifactRootValidationCommandReport(
	FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus Status,
	string ArtifactRoot,
	IReadOnlyList<FindGroupMutationPostJavaArtifactRootValidationCommandRow> Rows,
	string JavaCaptureCommand,
	string CSharpValidatorCommand,
	string DeterministicTimestampProperty,
	int DeterministicServerEpochSeconds,
	bool HasGeneratedJavaArtifacts,
	bool HasAllExpectedFiles,
	bool HasOnlyShapeValidArtifacts,
	bool ReadyForRuntimeComparison,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live command report for validating generated
/// Java CM_FIND_GROUP action 2/6 mutation-post artifacts at a chosen artifact
/// root. It reports file readiness and commands only; it does not generate
/// artifacts, validate runtime C# rows, or run comparison.
/// </summary>
public static class FindGroupMutationPostJavaArtifactRootValidationCommandReportService
{
	public static FindGroupMutationPostJavaArtifactRootValidationCommandReport Create(
		string artifactRoot = FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? directoryReport = null)
	{
		directoryReport ??= FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(artifactRoot);
		var schema = FindGroupMutationPostJavaTraceArtifactSchemaReportService.Create();
		var rows = directoryReport.Files
			.Select(file => new FindGroupMutationPostJavaArtifactRootValidationCommandRow(
				file.Action,
				file.Path,
				file.Status,
				file.Status != FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile,
				file.Status == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
				"FindGroupMutationPostJavaTraceArtifactDirectoryReportService; FindGroupMutationPostJavaTraceArtifactValidatorService",
				file.Notes))
			.ToArray();
		var status = StatusFor(directoryReport.Status);

		return new FindGroupMutationPostJavaArtifactRootValidationCommandReport(
			status,
			directoryReport.ArtifactRoot,
			rows,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.JavaCaptureCommand(directoryReport.ArtifactRoot),
			"C# validator: dotnet test dotnetConversion\\tests\\Aion.GameServer.Tests\\Aion.GameServer.Tests.csproj --filter \"FullyQualifiedName~FindGroupMutationPostJavaTraceArtifactDirectoryReportServiceTests|FullyQualifiedName~FindGroupMutationPostJavaTraceArtifactValidatorServiceTests\" --no-restore",
			FindGroupMutationPostJavaArtifactCaptureRunbookService.ServerEpochSecondsProperty,
			FindGroupMutationPostJavaArtifactCaptureRunbookService.DeterministicServerEpochSeconds,
			directoryReport.HasGeneratedJavaArtifacts,
			directoryReport.HasAllExpectedFiles,
			directoryReport.HasOnlyShapeValidArtifacts,
			ReadyForRuntimeComparison: false,
			DecisionFor(status),
			schema.TraceName,
			"Java sources reviewed: CM_FIND_GROUP.runImpl actions 2 and 6; FindGroupService.addRecruitment/addApplication; FindGroupMutationPostTraceCaptureHooks.",
			IsLive: false);
	}

	private static FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus StatusFor(
		FindGroupMutationPostJavaTraceArtifactDirectoryStatus directoryStatus)
	{
		return directoryStatus switch
		{
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.MissingDirectory => FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.BlockedMissingDirectory,
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.MissingExpectedFiles => FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.BlockedMissingExpectedFiles,
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.InvalidArtifacts => FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.BlockedInvalidArtifacts,
			_ => FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.ShapeValidRuntimeComparisonBlocked,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus status)
	{
		return status switch
		{
			FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.BlockedMissingDirectory => "Java artifact-root validation is blocked because the artifact directory is missing; run the focused Java capture command with an explicit artifact root.",
			FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.BlockedMissingExpectedFiles => "Java artifact-root validation is blocked because one or both expected action 2/6 files are missing.",
			FindGroupMutationPostJavaArtifactRootValidationCommandReportStatus.BlockedInvalidArtifacts => "Java artifact-root validation is blocked because one or more generated Java artifacts failed schema/action validation.",
			_ => "Java artifact files are shape-valid only; live C# boundary rows and runtime comparison evidence are still required before parity can be claimed.",
		};
	}
}
