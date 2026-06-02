namespace Aion.GameServer.Services;

public enum FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportStatus
{
	BlockedMissingAcceptedBoundaryRows,
	ReadyForJavaArtifactPairingRuntimeComparisonBlocked,
}

public sealed record FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportRow(
	int Order,
	FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate Gate,
	int? Action,
	bool Satisfied,
	bool BlocksJavaArtifactPairing,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReport(
	FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportStatus Status,
	IReadOnlyList<FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportRow> Rows,
	IReadOnlyList<string> RequiredAcceptedBoundaryRowFields,
	int AcceptedLiveRowCount,
	bool HasActionTwoAcceptedRow,
	bool HasActionSixAcceptedRow,
	bool CanFeedJavaArtifactPairing,
	bool CanRunCSharpCapture,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live handoff report for accepted C# CM_FIND_GROUP
/// action 2/6 boundary rows. It summarizes whether accepted rows can feed
/// Java artifact pairing, but it does not execute capture or comparison.
/// </summary>
public static class FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportService
{
	public static FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReport Create(
		FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflight? intakePreflight = null)
	{
		intakePreflight ??= FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create();
		var rows = intakePreflight.Rows
			.Select(row => new FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportRow(
				row.Order,
				row.Gate,
				row.Action,
				row.Satisfied,
				BlocksJavaArtifactPairing: !row.Satisfied,
				row.RequiredEvidence,
				row.CurrentEvidence,
				row.Notes))
			.ToArray();
		var status = intakePreflight.CanFeedRuntimeComparison
			? FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportStatus.ReadyForJavaArtifactPairingRuntimeComparisonBlocked
			: FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportStatus.BlockedMissingAcceptedBoundaryRows;

		return new FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReport(
			status,
			rows,
			intakePreflight.RequiredAcceptedBoundaryRowFields,
			intakePreflight.AcceptedLiveRowCount,
			intakePreflight.HasActionTwoAcceptedRow,
			intakePreflight.HasActionSixAcceptedRow,
			CanFeedJavaArtifactPairing: intakePreflight.CanFeedRuntimeComparison,
			CanRunCSharpCapture: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			intakePreflight.TraceName,
			intakePreflight.JavaSource,
			IsLive: false);
	}

	private static string DecisionFor(
		FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportStatus status)
	{
		return status switch
		{
			FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportStatus.ReadyForJavaArtifactPairingRuntimeComparisonBlocked => "Accepted C# action 2/6 boundary rows can feed Java artifact pairing, but this handoff does not execute capture, runtime comparison, or verified parity.",
			_ => "Accepted C# boundary row handoff is blocked until action 2 and action 6 rows satisfy every required boundary row field.",
		};
	}
}
