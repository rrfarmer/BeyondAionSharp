namespace Aion.GameServer.Services;

public enum FindGroupMutationPostJavaTraceArtifactDirectoryStatus
{
	MissingDirectory,
	MissingExpectedFiles,
	InvalidArtifacts,
	AllExpectedArtifactsShapeValid,
}

public enum FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus
{
	MissingFile,
	InvalidArtifact,
	MissingExpectedAction,
	ShapeValid,
}

public sealed record FindGroupMutationPostJavaTraceArtifactDirectoryFileRow(
	int Action,
	string Path,
	FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus Status,
	FindGroupMutationPostJavaTraceArtifactValidationReport? ValidationReport,
	string Notes);

public sealed record FindGroupMutationPostJavaTraceArtifactDirectoryReport(
	FindGroupMutationPostJavaTraceArtifactDirectoryStatus Status,
	string ArtifactRoot,
	IReadOnlyList<FindGroupMutationPostJavaTraceArtifactDirectoryFileRow> Files,
	bool HasGeneratedJavaArtifacts,
	bool HasAllExpectedFiles,
	bool HasOnlyShapeValidArtifacts,
	bool ReadyForRuntimeComparison,
	string Notes);

/// <summary>
/// Java parity breadcrumb: guarded reader for future generated Java CM_FIND_GROUP action 2/6
/// mutation-post artifacts. Shape-valid files still do not prove runtime parity.
/// </summary>
public static class FindGroupMutationPostJavaTraceArtifactDirectoryReportService
{
	public static FindGroupMutationPostJavaTraceArtifactDirectoryReport Create(
		string artifactRoot = FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot)
	{
		var fileTargets = FindGroupMutationPostJavaTraceArtifactFileReportService.Create(artifactRoot);
		if (!Directory.Exists(artifactRoot))
		{
			return new FindGroupMutationPostJavaTraceArtifactDirectoryReport(
				FindGroupMutationPostJavaTraceArtifactDirectoryStatus.MissingDirectory,
				artifactRoot,
				fileTargets.Files.Select(target => Missing(target.Action, PathForAction(artifactRoot, target.Action), "Generated Java mutation-post artifact directory is missing.")).ToArray(),
				HasGeneratedJavaArtifacts: false,
				HasAllExpectedFiles: false,
				HasOnlyShapeValidArtifacts: false,
				ReadyForRuntimeComparison: false,
				"Generated Java CM_FIND_GROUP action 2/6 mutation-post artifacts are missing; runtime comparison remains blocked.");
		}

		var files = fileTargets.Files
			.Select(target => ReadExpectedFile(artifactRoot, target.Action))
			.ToArray();

		var hasGeneratedArtifacts = files.Any(file => file.Status != FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile);
		var hasAllExpectedFiles = files.All(file => file.Status != FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile);
		var hasOnlyShapeValidArtifacts = hasAllExpectedFiles && files.All(file => file.Status == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid);
		var status = DetermineStatus(hasAllExpectedFiles, hasOnlyShapeValidArtifacts);

		return new FindGroupMutationPostJavaTraceArtifactDirectoryReport(
			status,
			artifactRoot,
			files,
			hasGeneratedArtifacts,
			hasAllExpectedFiles,
			hasOnlyShapeValidArtifacts,
			ReadyForRuntimeComparison: false,
			status == FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid
				? "Generated Java artifact JSON is shape-valid only; live C# trace capture and runtime comparison evidence are still required."
				: "One or more expected generated Java artifact files are missing or failed validation; runtime comparison remains blocked.");
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryStatus DetermineStatus(
		bool hasAllExpectedFiles,
		bool hasOnlyShapeValidArtifacts)
	{
		if (!hasAllExpectedFiles)
			return FindGroupMutationPostJavaTraceArtifactDirectoryStatus.MissingExpectedFiles;

		return hasOnlyShapeValidArtifacts
			? FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid
			: FindGroupMutationPostJavaTraceArtifactDirectoryStatus.InvalidArtifacts;
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryFileRow ReadExpectedFile(string artifactRoot, int action)
	{
		var path = PathForAction(artifactRoot, action);
		if (!File.Exists(path))
			return Missing(action, path, "Expected generated Java mutation-post artifact file is missing.");

		var validationReport = FindGroupMutationPostJavaTraceArtifactValidatorService.Validate(File.ReadAllText(path));
		if (!validationReport.IsValid)
		{
			return new FindGroupMutationPostJavaTraceArtifactDirectoryFileRow(
				action,
				path,
				FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.InvalidArtifact,
				validationReport,
				"Generated Java artifact failed mutation-post schema validation.");
		}

		if (validationReport.Metadata?.TraceRows.Any(row => row.Action == action) != true)
		{
			return new FindGroupMutationPostJavaTraceArtifactDirectoryFileRow(
				action,
				path,
				FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingExpectedAction,
				validationReport,
				"Generated Java artifact is shape-valid but does not contain the expected CM_FIND_GROUP action row.");
		}

		return new FindGroupMutationPostJavaTraceArtifactDirectoryFileRow(
			action,
			path,
			FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
			validationReport,
			"Generated Java artifact is shape-valid only; runtime comparison remains blocked.");
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryFileRow Missing(int action, string path, string notes) =>
		new(
			action,
			path,
			FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile,
			ValidationReport: null,
			notes);

	private static string PathForAction(string artifactRoot, int action) =>
		Path.Combine(artifactRoot, FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(action));
}
