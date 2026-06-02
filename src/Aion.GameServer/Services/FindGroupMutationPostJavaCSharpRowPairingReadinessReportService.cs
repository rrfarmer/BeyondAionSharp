namespace Aion.GameServer.Services;

public enum FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus
{
	BlockedJavaArtifactsMissingOrInvalid,
	BlockedCSharpBoundaryRowsMissing,
	BlockedPairingIdentityMissing,
	ReadyForValueProjectionRuntimeComparisonBlocked,
}

public enum FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus
{
	BlockedMissingShapeValidJavaArtifact,
	BlockedMissingAcceptedCSharpBoundaryRow,
	BlockedMissingPairingIdentity,
	ReadyForValueProjection,
}

public sealed record FindGroupMutationPostJavaCSharpRowPairingReadinessRow(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind ExpectedMutationKind,
	string JavaMethod,
	string JavaArtifactPath,
	FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus JavaFileStatus,
	bool HasShapeValidJavaArtifact,
	bool HasAcceptedCSharpBoundaryRow,
	bool HasActionMutationPairingIdentity,
	bool CanFeedValueProjection,
	FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus Status,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostJavaCSharpRowPairingReadinessReport(
	FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus Status,
	IReadOnlyList<FindGroupMutationPostJavaCSharpRowPairingReadinessRow> Rows,
	string ArtifactRoot,
	bool HasShapeValidJavaArtifacts,
	bool HasAcceptedCSharpBoundaryRows,
	bool HasActionTwoPair,
	bool HasActionSixPair,
	bool HasAllActionMutationPairs,
	bool CanFeedValueProjection,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live action/mutation row-pairing readiness for
/// CM_FIND_GROUP action 2/6 mutation-post artifacts. It consumes explicit-root
/// Java post-capture shape validation plus accepted C# boundary-row intake, but
/// it does not project values, compare rows, execute sends, or prove parity.
/// </summary>
public static class FindGroupMutationPostJavaCSharpRowPairingReadinessReportService
{
	public static FindGroupMutationPostJavaCSharpRowPairingReadinessReport Create(
		FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummary? javaSummary = null,
		FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflight? csharpIntake = null)
	{
		javaSummary ??= FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService.Create(string.Empty);
		csharpIntake ??= FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create();
		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var rows = schema.SupportedActions
			.Select((action, index) => CreateRow(index + 1, action, javaSummary, csharpIntake))
			.ToArray();
		var hasJavaArtifacts = rows.All(row => row.HasShapeValidJavaArtifact);
		var hasCSharpRows = rows.All(row => row.HasAcceptedCSharpBoundaryRow);
		var hasAllPairs = rows.All(row => row.CanFeedValueProjection);
		var status = DetermineStatus(rows);

		return new FindGroupMutationPostJavaCSharpRowPairingReadinessReport(
			status,
			rows,
			javaSummary.ArtifactRoot,
			hasJavaArtifacts,
			hasCSharpRows,
			HasActionTwoPair: rows.Any(row => row.Action == 2 && row.CanFeedValueProjection),
			HasActionSixPair: rows.Any(row => row.Action == 6 && row.CanFeedValueProjection),
			hasAllPairs,
			CanFeedValueProjection: hasAllPairs,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			schema.TraceName,
			schema.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostJavaCSharpRowPairingReadinessRow CreateRow(
		int order,
		FindGroupDirectPacketMutationPostActionSchema action,
		FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummary javaSummary,
		FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflight csharpIntake)
	{
		var javaRow = javaSummary.Rows.SingleOrDefault(row => row.Action == action.Action);
		var hasJavaArtifact = javaRow?.IsShapeValid == true;
		var hasCSharpRow = action.Action == 2
			? csharpIntake.HasActionTwoAcceptedRow
			: csharpIntake.HasActionSixAcceptedRow;
		var hasPairingIdentity = hasJavaArtifact
			&& hasCSharpRow
			&& csharpIntake.HasJavaArtifactPairingIdentity;
		var canFeedValueProjection = hasJavaArtifact && hasCSharpRow && hasPairingIdentity;
		var status = DetermineRowStatus(hasJavaArtifact, hasCSharpRow, hasPairingIdentity);

		return new FindGroupMutationPostJavaCSharpRowPairingReadinessRow(
			order,
			action.Action,
			action.MutationKind,
			action.JavaMethod,
			javaRow?.ArtifactPath ?? string.Empty,
			javaRow?.FileStatus ?? FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.MissingFile,
			hasJavaArtifact,
			hasCSharpRow,
			hasPairingIdentity,
			canFeedValueProjection,
			status,
			$"javaStatus={javaRow?.FileStatus.ToString() ?? "MissingFile"}; javaShape={hasJavaArtifact}; csharpAccepted={hasCSharpRow}; csharpPairingIdentity={csharpIntake.HasJavaArtifactPairingIdentity}; expectedMutation={action.MutationKind}",
			status == FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.ReadyForValueProjection
				? "Java artifact and C# boundary row share action/mutation identity; value projection and runtime comparison remain separate future work."
				: "Action cannot feed value projection until shape-valid Java artifact and accepted C# boundary row share action/mutation identity.");
	}

	private static FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus DetermineStatus(
		IReadOnlyList<FindGroupMutationPostJavaCSharpRowPairingReadinessRow> rows)
	{
		if (rows.Any(row => !row.HasShapeValidJavaArtifact))
			return FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedJavaArtifactsMissingOrInvalid;

		if (rows.Any(row => !row.HasAcceptedCSharpBoundaryRow))
			return FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedCSharpBoundaryRowsMissing;

		if (rows.Any(row => !row.HasActionMutationPairingIdentity))
			return FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedPairingIdentityMissing;

		return FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.ReadyForValueProjectionRuntimeComparisonBlocked;
	}

	private static FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus DetermineRowStatus(
		bool hasJavaArtifact,
		bool hasCSharpRow,
		bool hasPairingIdentity)
	{
		if (!hasJavaArtifact)
			return FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.BlockedMissingShapeValidJavaArtifact;

		if (!hasCSharpRow)
			return FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.BlockedMissingAcceptedCSharpBoundaryRow;

		if (!hasPairingIdentity)
			return FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.BlockedMissingPairingIdentity;

		return FindGroupMutationPostJavaCSharpRowPairingReadinessRowStatus.ReadyForValueProjection;
	}

	private static string DecisionFor(
		FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus status)
	{
		return status switch
		{
			FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedJavaArtifactsMissingOrInvalid => "Java/C# row pairing is blocked until both explicit-root Java action 2/6 artifacts are present and shape-valid.",
			FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedCSharpBoundaryRowsMissing => "Java/C# row pairing is blocked until accepted C# live-boundary rows exist for actions 2 and 6.",
			FindGroupMutationPostJavaCSharpRowPairingReadinessReportStatus.BlockedPairingIdentityMissing => "Java/C# row pairing is blocked until accepted C# rows prove Java artifact pairing identity by action and mutation kind.",
			_ => "Java/C# action 2/6 rows can feed future value projection by action/mutation identity, but value projection, runtime comparison, result emission, and verified parity remain blocked.",
		};
	}
}
