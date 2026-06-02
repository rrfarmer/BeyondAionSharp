namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowValueComparisonStatus
{
	BlockedMissingJavaRows,
	BlockedMissingAcceptedCSharpRows,
	Compared,
}

public enum FindGroupMutationPostProjectedRowValueComparisonResultKind
{
	Matched,
	MissingJavaRow,
	MissingCSharpRow,
	FieldMismatch,
}

public sealed record FindGroupMutationPostProjectedRowValueComparisonResultRow(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostProjectedRowValueComparisonResultKind ResultKind,
	FindGroupMutationPostComparisonDifferenceKind DifferenceKind,
	string JavaValue,
	string CSharpValue,
	string JavaSource,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowValueComparisonReport(
	FindGroupMutationPostProjectedRowValueComparisonStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowValueComparisonResultRow> Rows,
	IReadOnlyList<string> ComparedFields,
	bool HasActionTwoJavaRow,
	bool HasActionSixJavaRow,
	bool HasActionTwoAcceptedCSharpRow,
	bool HasActionSixAcceptedCSharpRow,
	bool AllComparedFieldsMatched,
	bool CanClaimVerifiedParity,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: executes the first concrete CM_FIND_GROUP action 2/6
/// mutation-post row-value comparison between Java trace artifacts and accepted
/// C# boundary rows. This is scoped comparison evidence, not a verified parity claim.
/// </summary>
public static class FindGroupMutationPostProjectedRowValueComparisonExecutorService
{
	private static readonly string[] ComparedFieldNames =
	[
		"action",
		"mutationKind",
		"activePlayerObjectId",
		"mutatedEntryObjectId",
		"postedSystemMessageId",
		"refreshedListAction",
		"visibleEntryObjectIdsAfterMutation",
		"worldBroadcastCount",
		"inviteDispatchCount",
	];

	public static FindGroupMutationPostProjectedRowValueComparisonReport Compare(
		FindGroupMutationPostJavaTraceArtifactDirectoryReport javaArtifacts,
		IReadOnlyList<FindGroupDirectPacketMutationPostBoundaryTraceExport> csharpRows,
		FindGroupMutationPostComparisonKeyProjectionMetadata? keyProjection = null)
	{
		keyProjection ??= FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();
		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var javaRows = AcceptedJavaRows(javaArtifacts);
		var guarded = FindGroupMutationPostGuardedFixtureResultContractService.Create(candidateRows: csharpRows);
		var csharpAcceptedRows = guarded.CandidateRows
			.Where(row => row.IsLiveBoundaryEvidence)
			.Select(row => csharpRows[row.Order - 1])
			.ToArray();
		var hasActionTwoJavaRow = javaRows.Any(row => row.Action == 2);
		var hasActionSixJavaRow = javaRows.Any(row => row.Action == 6);
		var hasActionTwoCSharpRow = csharpAcceptedRows.Any(row => row.Action == 2);
		var hasActionSixCSharpRow = csharpAcceptedRows.Any(row => row.Action == 6);

		if (!hasActionTwoJavaRow || !hasActionSixJavaRow)
		{
			return CreateBlockedReport(
				FindGroupMutationPostProjectedRowValueComparisonStatus.BlockedMissingJavaRows,
				FindGroupMutationPostProjectedRowValueComparisonResultKind.MissingJavaRow,
				schema,
				keyProjection,
				javaRows,
				csharpAcceptedRows,
				hasActionTwoJavaRow,
				hasActionSixJavaRow,
				hasActionTwoCSharpRow,
				hasActionSixCSharpRow);
		}

		if (!hasActionTwoCSharpRow || !hasActionSixCSharpRow)
		{
			return CreateBlockedReport(
				FindGroupMutationPostProjectedRowValueComparisonStatus.BlockedMissingAcceptedCSharpRows,
				FindGroupMutationPostProjectedRowValueComparisonResultKind.MissingCSharpRow,
				schema,
				keyProjection,
				javaRows,
				csharpAcceptedRows,
				hasActionTwoJavaRow,
				hasActionSixJavaRow,
				hasActionTwoCSharpRow,
				hasActionSixCSharpRow);
		}

		var rows = new List<FindGroupMutationPostProjectedRowValueComparisonResultRow>();
		foreach (var action in schema.SupportedActions)
		{
			var javaRow = javaRows.Single(row => row.Action == action.Action);
			var csharpRow = csharpAcceptedRows.Single(row => row.Action == action.Action);
			foreach (var fieldName in ComparedFieldNames)
			{
				var javaValue = JavaValue(javaRow, fieldName);
				var csharpValue = CSharpValue(csharpRow, fieldName);
				var resultKind = string.Equals(javaValue, csharpValue, StringComparison.Ordinal)
					? FindGroupMutationPostProjectedRowValueComparisonResultKind.Matched
					: FindGroupMutationPostProjectedRowValueComparisonResultKind.FieldMismatch;
				rows.Add(CreateResultRow(rows.Count + 1, action, fieldName, resultKind, javaValue, csharpValue, keyProjection));
			}
		}

		return new FindGroupMutationPostProjectedRowValueComparisonReport(
			FindGroupMutationPostProjectedRowValueComparisonStatus.Compared,
			rows,
			ComparedFieldNames,
			hasActionTwoJavaRow,
			hasActionSixJavaRow,
			hasActionTwoCSharpRow,
			hasActionSixCSharpRow,
			rows.All(row => row.ResultKind == FindGroupMutationPostProjectedRowValueComparisonResultKind.Matched),
			CanClaimVerifiedParity: false,
			schema.TraceName,
			schema.JavaSource,
			IsLive: true);
	}

	private static FindGroupMutationPostProjectedRowValueComparisonReport CreateBlockedReport(
		FindGroupMutationPostProjectedRowValueComparisonStatus status,
		FindGroupMutationPostProjectedRowValueComparisonResultKind missingKind,
		FindGroupDirectPacketMutationPostBoundaryTraceSchema schema,
		FindGroupMutationPostComparisonKeyProjectionMetadata keyProjection,
		IReadOnlyList<FindGroupMutationPostJavaTraceArtifactValidationTraceRow> javaRows,
		IReadOnlyList<FindGroupDirectPacketMutationPostBoundaryTraceExport> csharpRows,
		bool hasActionTwoJavaRow,
		bool hasActionSixJavaRow,
		bool hasActionTwoCSharpRow,
		bool hasActionSixCSharpRow)
	{
		var rows = new List<FindGroupMutationPostProjectedRowValueComparisonResultRow>();
		foreach (var action in schema.SupportedActions)
		{
			var missingJava = !javaRows.Any(row => row.Action == action.Action);
			var missingCSharp = !csharpRows.Any(row => row.Action == action.Action);
			if ((missingKind == FindGroupMutationPostProjectedRowValueComparisonResultKind.MissingJavaRow && missingJava)
				|| (missingKind == FindGroupMutationPostProjectedRowValueComparisonResultKind.MissingCSharpRow && missingCSharp))
			{
				rows.Add(CreateResultRow(
					rows.Count + 1,
					action,
					"row",
					missingKind,
					missingJava ? "<missing>" : "<present>",
					missingCSharp ? "<missing>" : "<present>",
					keyProjection));
			}
		}

		return new FindGroupMutationPostProjectedRowValueComparisonReport(
			status,
			rows,
			ComparedFieldNames,
			hasActionTwoJavaRow,
			hasActionSixJavaRow,
			hasActionTwoCSharpRow,
			hasActionSixCSharpRow,
			AllComparedFieldsMatched: false,
			CanClaimVerifiedParity: false,
			schema.TraceName,
			schema.JavaSource,
			IsLive: false);
	}

	private static IReadOnlyList<FindGroupMutationPostJavaTraceArtifactValidationTraceRow> AcceptedJavaRows(
		FindGroupMutationPostJavaTraceArtifactDirectoryReport javaArtifacts)
	{
		return javaArtifacts.Files
			.Where(file => file.Status == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid)
			.SelectMany(file => file.ValidationReport?.Metadata?.TraceRows
				.Where(row => row.Action == file.Action) ?? [])
			.GroupBy(row => row.Action)
			.Select(group => group.Single())
			.ToArray();
	}

	private static FindGroupMutationPostProjectedRowValueComparisonResultRow CreateResultRow(
		int order,
		FindGroupDirectPacketMutationPostActionSchema action,
		string fieldName,
		FindGroupMutationPostProjectedRowValueComparisonResultKind resultKind,
		string javaValue,
		string csharpValue,
		FindGroupMutationPostComparisonKeyProjectionMetadata keyProjection)
	{
		var field = keyProjection.Fields.SingleOrDefault(row => row.Action == action.Action && row.FieldName == fieldName);
		return new FindGroupMutationPostProjectedRowValueComparisonResultRow(
			order,
			action.Action,
			action.MutationKind,
			fieldName,
			resultKind,
			field?.Role switch
			{
				FindGroupMutationPostComparisonKeyFieldRole.RowIdentity => FindGroupMutationPostComparisonDifferenceKind.RowIdentityMismatch,
				FindGroupMutationPostComparisonKeyFieldRole.MutationState => FindGroupMutationPostComparisonDifferenceKind.MutationStateMismatch,
				FindGroupMutationPostComparisonKeyFieldRole.DirectPacketShape => FindGroupMutationPostComparisonDifferenceKind.DirectPacketMismatch,
				FindGroupMutationPostComparisonKeyFieldRole.SideEffectGuard => FindGroupMutationPostComparisonDifferenceKind.SideEffectGuardMismatch,
				_ => FindGroupMutationPostComparisonDifferenceKind.RowIdentityMismatch,
			},
			javaValue,
			csharpValue,
			field?.JavaSource ?? action.JavaMethod,
			resultKind == FindGroupMutationPostProjectedRowValueComparisonResultKind.Matched
				? "Projected Java artifact value matched accepted C# boundary-row value for this scoped field."
				: "Projected Java artifact value did not match the accepted C# boundary-row value for this scoped field.");
	}

	private static string JavaValue(FindGroupMutationPostJavaTraceArtifactValidationTraceRow row, string fieldName)
	{
		return fieldName switch
		{
			"action" => row.Action.ToString(),
			"mutationKind" => row.MutationKind,
			"activePlayerObjectId" => row.ActivePlayerObjectId.ToString(),
			"mutatedEntryObjectId" => row.MutatedEntryObjectId.ToString(),
			"postedSystemMessageId" => row.PostedSystemMessageId.ToString(),
			"refreshedListAction" => row.RefreshedListAction.ToString(),
			"visibleEntryObjectIdsAfterMutation" => Format(row.VisibleEntryObjectIdsAfterMutation ?? []),
			"worldBroadcastCount" => row.WorldBroadcastCount.ToString(),
			"inviteDispatchCount" => row.InviteDispatchCount.ToString(),
			_ => string.Empty,
		};
	}

	private static string CSharpValue(FindGroupDirectPacketMutationPostBoundaryTraceExport row, string fieldName)
	{
		return fieldName switch
		{
			"action" => row.Action.ToString(),
			"mutationKind" => row.MutationKind.ToString(),
			"activePlayerObjectId" => row.ActivePlayerObjectId.ToString(),
			"mutatedEntryObjectId" => row.MutatedEntryObjectId.ToString(),
			"postedSystemMessageId" => row.PostedSystemMessageId.ToString(),
			"refreshedListAction" => row.RefreshedListAction.ToString(),
			"visibleEntryObjectIdsAfterMutation" => Format(row.VisibleEntryObjectIdsAfterMutation),
			"worldBroadcastCount" => row.WorldBroadcastCount.ToString(),
			"inviteDispatchCount" => row.InviteDispatchCount.ToString(),
			_ => string.Empty,
		};
	}

	private static string Format(IReadOnlyList<int> values) =>
		$"[{string.Join(",", values)}]";
}
