namespace Aion.GameServer.Services;

public enum FindGroupMutationPostCSharpTraceRowFixtureReportStatus
{
	BlockedMissingCSharpRows,
	BlockedNonLiveRowsOnly,
	ReadyWithLiveRows,
}

public enum FindGroupMutationPostCSharpTraceRowFixtureRowStatus
{
	ShapeValidNonLiveProjection,
	LiveBoundaryEvidence,
	InvalidShape,
	UnsupportedAction,
}

public sealed record FindGroupMutationPostCSharpTraceRowFixtureRow(
	int Order,
	int Action,
	FindGroupMutationPostCSharpTraceRowFixtureRowStatus Status,
	bool IsShapeValid,
	bool IsLiveEvidence,
	bool BlocksComparisonInput,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostCSharpTraceRowFixtureReport(
	FindGroupMutationPostCSharpTraceRowFixtureReportStatus Status,
	IReadOnlyList<FindGroupMutationPostCSharpTraceRowFixtureRow> Rows,
	FindGroupMutationPostComparisonInputEnvelope Envelope,
	bool HasActionTwoCSharpRow,
	bool HasActionSixCSharpRow,
	bool HasActionTwoLiveCSharpRow,
	bool HasActionSixLiveCSharpRow,
	bool HasShapeValidJavaRows,
	bool FeedsComparisonInputEnvelope,
	bool ReadyForComparisonExecution,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: fixture report for CM_FIND_GROUP action 2/6 C# mutation-post
/// trace rows. Disabled boundary projections can exercise row shape, but only rows with
/// boundary, executor, and registry observations count as live comparison evidence.
/// </summary>
public static class FindGroupMutationPostCSharpTraceRowFixtureReportService
{
	public static FindGroupMutationPostCSharpTraceRowFixtureReport Create(
		IReadOnlyList<FindGroupDirectPacketMutationPostBoundaryTraceExport>? csharpRows = null,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? javaArtifacts = null)
	{
		csharpRows ??= [];
		javaArtifacts ??= FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create();
		var envelope = FindGroupMutationPostComparisonInputEnvelopeService.Create(javaArtifacts, csharpRows);
		var rows = csharpRows.Select((row, index) => CreateRow(index + 1, row)).ToArray();
		var status = DetermineStatus(rows);

		return new FindGroupMutationPostCSharpTraceRowFixtureReport(
			status,
			rows,
			envelope,
			HasActionTwoCSharpRow: rows.Any(row => row.Action == 2 && row.IsShapeValid),
			HasActionSixCSharpRow: rows.Any(row => row.Action == 6 && row.IsShapeValid),
			HasActionTwoLiveCSharpRow: rows.Any(row => row.Action == 2 && row.IsLiveEvidence),
			HasActionSixLiveCSharpRow: rows.Any(row => row.Action == 6 && row.IsLiveEvidence),
			HasShapeValidJavaRows: javaArtifacts.Status == FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid,
			FeedsComparisonInputEnvelope: true,
			ReadyForComparisonExecution: envelope.ReadyForComparisonExecution,
			envelope.TraceName,
			envelope.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostCSharpTraceRowFixtureRow CreateRow(
		int order,
		FindGroupDirectPacketMutationPostBoundaryTraceExport row)
	{
		var shapeValid = IsShapeValid(row);
		var live = shapeValid
			&& row.TraceSource == FindGroupDirectPacketMutationPostTraceSource.CSharp
			&& row.BoundaryAccepted
			&& row.ExecutorInvokedFromBoundary
			&& row.RegistrySendsObservedInOrder;

		var status = row.Action is not 2 and not 6
			? FindGroupMutationPostCSharpTraceRowFixtureRowStatus.UnsupportedAction
			: !shapeValid
				? FindGroupMutationPostCSharpTraceRowFixtureRowStatus.InvalidShape
				: live
					? FindGroupMutationPostCSharpTraceRowFixtureRowStatus.LiveBoundaryEvidence
					: FindGroupMutationPostCSharpTraceRowFixtureRowStatus.ShapeValidNonLiveProjection;

		return new FindGroupMutationPostCSharpTraceRowFixtureRow(
			order,
			row.Action,
			status,
			shapeValid,
			live,
			BlocksComparisonInput: !live,
			$"source={row.TraceSource}; boundaryAccepted={row.BoundaryAccepted}; executor={row.ExecutorInvokedFromBoundary}; registry={row.RegistrySendsObservedInOrder}; broadcasts={row.WorldBroadcastCount}; invites={row.InviteDispatchCount}",
			live
				? "Row has live boundary, executor, and registry observation evidence."
				: "Row can exercise C# schema shape only; it is not live comparison evidence.");
	}

	private static bool IsShapeValid(FindGroupDirectPacketMutationPostBoundaryTraceExport row)
	{
		if (row.SchemaVersion != FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.SchemaVersion)
			return false;
		if (row.TraceName != FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema().TraceName)
			return false;
		if (row.TraceSource != FindGroupDirectPacketMutationPostTraceSource.CSharp)
			return false;
		if (row.WorldBroadcastCount != 0 || row.InviteDispatchCount != 0)
			return false;

		return row.Action switch
		{
			2 => row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
				&& row.PostedSystemMessageType == "SmSystemMessage"
				&& row.PostedSystemMessageId == 1400392
				&& row.RefreshedListPacketType == "SmFindGroup"
				&& row.RefreshedListAction == 0,
			6 => row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
				&& row.PostedSystemMessageType == "SmSystemMessage"
				&& row.PostedSystemMessageId == 1400393
				&& row.RefreshedListPacketType == "SmFindGroup"
				&& row.RefreshedListAction == 4,
			_ => false,
		};
	}

	private static FindGroupMutationPostCSharpTraceRowFixtureReportStatus DetermineStatus(
		IReadOnlyList<FindGroupMutationPostCSharpTraceRowFixtureRow> rows)
	{
		if (rows.Count == 0)
			return FindGroupMutationPostCSharpTraceRowFixtureReportStatus.BlockedMissingCSharpRows;

		var hasExpectedLiveRows = rows.Any(row => row.Action == 2 && row.IsLiveEvidence)
			&& rows.Any(row => row.Action == 6 && row.IsLiveEvidence);

		return hasExpectedLiveRows
			? FindGroupMutationPostCSharpTraceRowFixtureReportStatus.ReadyWithLiveRows
			: FindGroupMutationPostCSharpTraceRowFixtureReportStatus.BlockedNonLiveRowsOnly;
	}
}
