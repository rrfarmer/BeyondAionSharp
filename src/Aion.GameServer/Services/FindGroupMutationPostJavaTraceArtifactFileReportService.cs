namespace Aion.GameServer.Services;

public enum FindGroupMutationPostJavaTraceArtifactFileStatus
{
	BlockedMissingGeneratedArtifact,
	BlockedMissingJavaInstrumentation,
	BlockedMissingTraceSerializer,
	ReadyForFileDiscoveryOnly,
}

public sealed record FindGroupMutationPostJavaTraceArtifactFileRow(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string ArtifactPath,
	string ExpectedTraceName,
	FindGroupMutationPostJavaTraceArtifactFileStatus Status,
	string ValidatorTarget,
	string Notes);

public sealed record FindGroupMutationPostJavaTraceArtifactFileReport(
	string ArtifactRoot,
	string FileNamePattern,
	IReadOnlyList<FindGroupMutationPostJavaTraceArtifactFileRow> Files,
	bool HasActionTwoTarget,
	bool HasActionSixTarget,
	bool UsesStableTraceName,
	bool RequiresJavaInstrumentation,
	bool RequiresTraceSerializer,
	bool RequiresGeneratedArtifacts,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live file target report for future generated Java
/// CM_FIND_GROUP action 2/6 mutation-post trace artifacts.
/// </summary>
public static class FindGroupMutationPostJavaTraceArtifactFileReportService
{
	public const string DefaultArtifactRoot = "parity-artifacts/find-group/mutation-post/java";
	public const string FileNamePattern = "cm-find-group-direct-mutation-post-boundary-action-{action}-java.json";

	public static FindGroupMutationPostJavaTraceArtifactFileReport Create(string artifactRoot = DefaultArtifactRoot)
	{
		var schemaReport = FindGroupMutationPostJavaTraceArtifactSchemaReportService.Create();
		var files = schemaReport.Actions
			.Select((action, index) => new FindGroupMutationPostJavaTraceArtifactFileRow(
				index + 1,
				action.Action,
				action.MutationKind,
				$"{artifactRoot}/{FileNameForAction(action.Action)}",
				schemaReport.TraceName,
				FindGroupMutationPostJavaTraceArtifactFileStatus.BlockedMissingGeneratedArtifact,
				"FindGroupMutationPostJavaTraceArtifactValidatorService",
				$"Generated Java artifact must validate schemaVersion={schemaReport.SchemaVersion}, traceSource=Java, postedSystemMessageId={action.PostedSystemMessageId}, refreshedListAction={action.RefreshedShowListAction}, worldBroadcastCount=0, and inviteDispatchCount=0."))
			.ToArray();

		return new FindGroupMutationPostJavaTraceArtifactFileReport(
			artifactRoot,
			FileNamePattern,
			files,
			HasActionTwoTarget: files.Any(file => file.Action == 2),
			HasActionSixTarget: files.Any(file => file.Action == 6),
			UsesStableTraceName: files.All(file => file.ExpectedTraceName == schemaReport.TraceName),
			RequiresJavaInstrumentation: true,
			RequiresTraceSerializer: true,
			RequiresGeneratedArtifacts: true,
			ReadyForRuntimeComparison: false,
			"Java sources reviewed: CM_FIND_GROUP.runImpl actions 2 and 6; FindGroupService.addRecruitment/addApplication.",
			IsLive: false);
	}

	public static string FileNameForAction(int action) =>
		$"cm-find-group-direct-mutation-post-boundary-action-{action}-java.json";
}
