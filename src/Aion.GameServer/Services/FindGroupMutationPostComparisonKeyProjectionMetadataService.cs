namespace Aion.GameServer.Services;

public enum FindGroupMutationPostComparisonKeyProjectionStatus
{
	BlockedPendingTraceRows,
	Ready,
}

public enum FindGroupMutationPostComparisonKeyFieldRole
{
	CompatibilityGate,
	RowIdentity,
	MutationState,
	DirectPacketShape,
	RegistryObservation,
	SideEffectGuard,
	RuntimeOnly,
}

public enum FindGroupMutationPostComparisonKeyFieldStatus
{
	RequiredForProjection,
	IgnoredForEquality,
}

public sealed record FindGroupMutationPostComparisonKeyProjectionFieldRow(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostComparisonKeyFieldRole Role,
	FindGroupMutationPostComparisonKeyFieldStatus Status,
	string ProjectionRule,
	string JavaSource,
	string Notes);

public sealed record FindGroupMutationPostComparisonKeyProjectionMetadata(
	FindGroupMutationPostComparisonKeyProjectionStatus Status,
	IReadOnlyList<int> Actions,
	IReadOnlyList<FindGroupMutationPostComparisonKeyProjectionFieldRow> Fields,
	IReadOnlyList<string> CompatibilityGateFields,
	IReadOnlyList<string> RowIdentityFields,
	IReadOnlyList<string> EqualityProjectionFields,
	IReadOnlyList<string> IgnoredRuntimeFields,
	bool RequiresGeneratedJavaTraceRows,
	bool RequiresLiveCSharpTraceRows,
	bool RequiresRegistryObservation,
	bool ReadyForRuntimeComparison,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: comparison-key metadata for future CM_FIND_GROUP action 2/6
/// mutation-post Java/C# trace rows. This defines equality projection only; it performs
/// no live capture and does not compare runtime rows.
/// </summary>
public static class FindGroupMutationPostComparisonKeyProjectionMetadataService
{
	public static FindGroupMutationPostComparisonKeyProjectionMetadata Create()
	{
		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var rows = new List<FindGroupMutationPostComparisonKeyProjectionFieldRow>();

		foreach (var action in schema.SupportedActions)
		{
			AddActionRows(rows, action);
		}

		var fieldArray = rows.ToArray();

		return new FindGroupMutationPostComparisonKeyProjectionMetadata(
			FindGroupMutationPostComparisonKeyProjectionStatus.BlockedPendingTraceRows,
			schema.SupportedActions.Select(action => action.Action).ToArray(),
			fieldArray,
			DistinctFields(fieldArray, FindGroupMutationPostComparisonKeyFieldRole.CompatibilityGate),
			DistinctFields(fieldArray, FindGroupMutationPostComparisonKeyFieldRole.RowIdentity),
			fieldArray
				.Where(row => row.Status == FindGroupMutationPostComparisonKeyFieldStatus.RequiredForProjection
					&& row.Role != FindGroupMutationPostComparisonKeyFieldRole.CompatibilityGate)
				.Select(row => row.FieldName)
				.Distinct(StringComparer.Ordinal)
				.ToArray(),
			fieldArray
				.Where(row => row.Status == FindGroupMutationPostComparisonKeyFieldStatus.IgnoredForEquality)
				.Select(row => row.FieldName)
				.Distinct(StringComparer.Ordinal)
				.ToArray(),
			RequiresGeneratedJavaTraceRows: true,
			RequiresLiveCSharpTraceRows: true,
			RequiresRegistryObservation: true,
			ReadyForRuntimeComparison: false,
			schema.TraceName,
			schema.JavaSource,
			IsLive: false);
	}

	private static void AddActionRows(
		ICollection<FindGroupMutationPostComparisonKeyProjectionFieldRow> rows,
		FindGroupDirectPacketMutationPostActionSchema action)
	{
		Add(rows, action, "schemaVersion", FindGroupMutationPostComparisonKeyFieldRole.CompatibilityGate,
			"Require schemaVersion == 1 before projecting equality keys.",
			"CM_FIND_GROUP mutation-post trace schema",
			"Schema compatibility is checked before equality projection.");

		Add(rows, action, "traceName", FindGroupMutationPostComparisonKeyFieldRole.CompatibilityGate,
			"Require traceName == cm-find-group-direct-mutation-post-boundary before projecting equality keys.",
			"CM_FIND_GROUP mutation-post trace schema",
			"Trace family compatibility is checked before equality projection.");

		Add(rows, action, "action", FindGroupMutationPostComparisonKeyFieldRole.RowIdentity,
			$"Use action {action.Action} as the primary row identity partition.",
			"CM_FIND_GROUP.readImpl/runImpl action dispatch",
			"Only actions 2 and 6 are in this mutation-post projection.");

		Add(rows, action, "mutationKind", FindGroupMutationPostComparisonKeyFieldRole.RowIdentity,
			$"Require mutationKind == {action.MutationKind}.",
			action.JavaMethod,
			"Separates recruitment and application singleton mutations.");

		Add(rows, action, "activePlayerObjectId", FindGroupMutationPostComparisonKeyFieldRole.RowIdentity,
			"Use the triggering active player object id as a row identity key.",
			"CM_FIND_GROUP.runImpl getConnection().getActivePlayer()",
			"Future Java/C# fixtures must use matching player identities before this key can prove parity.");

		Add(rows, action, "activePlayerRace", FindGroupMutationPostComparisonKeyFieldRole.MutationState,
			"Compare the active player's race used by the refreshed show-list filter.",
			action.Action == 2 ? "showRecruitments filters recruitment.getRace() == player.getRace()" : "showApplications filters application.getPlayer().getRace() == player.getRace()",
			"Race filtering affects visibleEntryObjectIdsAfterMutation.");

		Add(rows, action, "mutatedEntryObjectId", FindGroupMutationPostComparisonKeyFieldRole.RowIdentity,
			action.Action == 2
				? "Compare the recruitment player/team object id inserted into recruitments."
				: "Compare the application player object id inserted into applications.",
			action.JavaMethod,
			"Captures the Java singleton key mutated before direct packets.");

		Add(rows, action, "stateMutationRecordedBeforeDirectPackets", FindGroupMutationPostComparisonKeyFieldRole.MutationState,
			"Require true before comparing direct packet evidence.",
			action.JavaMethod,
			"Java mutates the singleton map before sending the posted system message and refreshed list.");

		Add(rows, action, "postedSystemMessageRecipientObjectId", FindGroupMutationPostComparisonKeyFieldRole.DirectPacketShape,
			"Compare against activePlayerObjectId.",
			"PacketSendUtility.sendPacket(player, SM_SYSTEM_MESSAGE...)",
			"Java sends the posted system message directly to the triggering player.");

		Add(rows, action, "postedSystemMessageType", FindGroupMutationPostComparisonKeyFieldRole.DirectPacketShape,
			"Require SmSystemMessage.",
			action.JavaPostedSystemMessage,
			"Packet type must match before packet id comparison is meaningful.");

		Add(rows, action, "postedSystemMessageId", FindGroupMutationPostComparisonKeyFieldRole.DirectPacketShape,
			$"Require Java system message id {action.PostedSystemMessageId}.",
			action.JavaPostedSystemMessage,
			"Action-specific posted notification id is stable Java evidence.");

		Add(rows, action, "refreshedListRecipientObjectId", FindGroupMutationPostComparisonKeyFieldRole.DirectPacketShape,
			"Compare against activePlayerObjectId.",
			action.Action == 2 ? "showRecruitments(player)" : "showApplications(player)",
			"Java refreshes the triggering player's current tab after posting the mutation.");

		Add(rows, action, "refreshedListPacketType", FindGroupMutationPostComparisonKeyFieldRole.DirectPacketShape,
			"Require SmFindGroup.",
			action.Action == 2 ? "new SM_FIND_GROUP(0, recruitments)" : "new SM_FIND_GROUP(4, applications)",
			"Packet type must match before action/list comparison is meaningful.");

		Add(rows, action, "refreshedListAction", FindGroupMutationPostComparisonKeyFieldRole.DirectPacketShape,
			$"Require refreshed SM_FIND_GROUP action {action.RefreshedShowListAction}.",
			action.Action == 2 ? "new SM_FIND_GROUP(0, recruitments)" : "new SM_FIND_GROUP(4, applications)",
			"Action-specific refreshed list id is stable Java evidence.");

		Add(rows, action, "visibleEntryObjectIdsAfterMutation", FindGroupMutationPostComparisonKeyFieldRole.MutationState,
			"Compare the race-filtered visible entry ids after the mutation in materialized packet order.",
			action.Action == 2 ? "this.recruitments.values().stream().filter(...).toList()" : "this.applications.values().stream().filter(...).toList()",
			"This captures the state snapshot sent by the refreshed show-list packet.");

		Add(rows, action, "executorInvokedFromBoundary", FindGroupMutationPostComparisonKeyFieldRole.RegistryObservation,
			"Require true for live C# rows before runtime comparison.",
			"CM_FIND_GROUP.runImpl synchronous service dispatch",
			"Disabled executor evidence is not enough for live boundary parity.");

		Add(rows, action, "registrySendsObservedInOrder", FindGroupMutationPostComparisonKeyFieldRole.RegistryObservation,
			"Require true after observing posted message before refreshed list at the connection registry.",
			"PacketSendUtility.sendPacket order in FindGroupService.addRecruitment/addApplication",
			"Ordering must be observed, not inferred from planner intent order.");

		Add(rows, action, "worldBroadcastCount", FindGroupMutationPostComparisonKeyFieldRole.SideEffectGuard,
			"Require 0.",
			action.JavaMethod,
			"Mutation-post actions 2 and 6 use direct sends, not broadcast fanout.");

		Add(rows, action, "inviteDispatchCount", FindGroupMutationPostComparisonKeyFieldRole.SideEffectGuard,
			"Require 0.",
			action.JavaMethod,
			"Mutation-post actions 2 and 6 do not dispatch group/alliance invites.");

		AddIgnored(rows, action, "traceSource",
			"Ignore for equality after partitioning rows by Java and CSharp sources.",
			"Trace exporter source marker",
			"Java and C# rows must differ here by design.");

		AddIgnored(rows, action, "serverEpochSeconds",
			"Ignore raw wall-clock value for cross-runtime equality; compare refreshed packet shape and visible entries instead.",
			"SM_FIND_GROUP refreshed list header time",
			"Wall-clock seconds are runtime capture metadata unless a future same-clock fixture explicitly constrains them.");
	}

	private static IReadOnlyList<string> DistinctFields(
		IEnumerable<FindGroupMutationPostComparisonKeyProjectionFieldRow> rows,
		FindGroupMutationPostComparisonKeyFieldRole role)
	{
		return rows
			.Where(row => row.Role == role)
			.Select(row => row.FieldName)
			.Distinct(StringComparer.Ordinal)
			.ToArray();
	}

	private static void Add(
		ICollection<FindGroupMutationPostComparisonKeyProjectionFieldRow> rows,
		FindGroupDirectPacketMutationPostActionSchema action,
		string fieldName,
		FindGroupMutationPostComparisonKeyFieldRole role,
		string projectionRule,
		string javaSource,
		string notes)
	{
		rows.Add(new FindGroupMutationPostComparisonKeyProjectionFieldRow(
			rows.Count + 1,
			action.Action,
			action.MutationKind,
			fieldName,
			role,
			FindGroupMutationPostComparisonKeyFieldStatus.RequiredForProjection,
			projectionRule,
			javaSource,
			notes));
	}

	private static void AddIgnored(
		ICollection<FindGroupMutationPostComparisonKeyProjectionFieldRow> rows,
		FindGroupDirectPacketMutationPostActionSchema action,
		string fieldName,
		string projectionRule,
		string javaSource,
		string notes)
	{
		rows.Add(new FindGroupMutationPostComparisonKeyProjectionFieldRow(
			rows.Count + 1,
			action.Action,
			action.MutationKind,
			fieldName,
			FindGroupMutationPostComparisonKeyFieldRole.RuntimeOnly,
			FindGroupMutationPostComparisonKeyFieldStatus.IgnoredForEquality,
			projectionRule,
			javaSource,
			notes));
	}
}
