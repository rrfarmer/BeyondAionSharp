namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus
{
	MissingDirectory,
	NoArtifacts,
	AllArtifactsShapeValid,
	InvalidArtifacts,
}

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow(
	string Path,
	PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationReport ValidationReport);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport(
	PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus Status,
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow> Files,
	bool HasGeneratedJavaArtifacts,
	bool ReadyForRuntimeComparison,
	string Notes);

/// <summary>
/// Java parity breadcrumb: guarded reader for future generated Java protection stop-trigger artifacts under
/// parity-artifacts/protection-stop-trigger/java. Missing or shape-valid artifacts still do not prove parity.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReportService
{
	public static PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport Create(string artifactDirectory)
	{
		if (!Directory.Exists(artifactDirectory))
		{
			return new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport(
				PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.MissingDirectory,
				[],
				HasGeneratedJavaArtifacts: false,
				ReadyForRuntimeComparison: false,
				"Generated Java protection stop-trigger artifacts are missing; runtime comparison remains blocked.");
		}

		var files = Directory
			.EnumerateFiles(artifactDirectory, "*.json", SearchOption.TopDirectoryOnly)
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path => new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactFileRow(
				path,
				PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(File.ReadAllText(path))))
			.ToArray();

		if (files.Length == 0)
		{
			return new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport(
				PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.NoArtifacts,
				files,
				HasGeneratedJavaArtifacts: false,
				ReadyForRuntimeComparison: false,
				"Artifact directory exists but contains no schema-v1 JSON artifacts; runtime comparison remains blocked.");
		}

		var status = files.All(file => file.ValidationReport.IsValidSchemaV1)
			? PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid
			: PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.InvalidArtifacts;

		return new PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport(
			status,
			files,
			HasGeneratedJavaArtifacts: true,
			ReadyForRuntimeComparison: false,
			status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid
				? "Generated Java artifact JSON is shape-valid only; C# runtime comparison evidence is still required."
				: "One or more generated Java artifact files failed schema validation; runtime comparison remains blocked.");
	}
}
