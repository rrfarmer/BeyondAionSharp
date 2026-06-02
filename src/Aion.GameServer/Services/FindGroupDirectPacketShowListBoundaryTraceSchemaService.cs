namespace Aion.GameServer.Services;

public static class FindGroupDirectPacketShowListBoundaryTraceSchemaService
{
	public const int SchemaVersion = 1;

	public static FindGroupDirectPacketShowListBoundaryTraceSchema CreateSchema()
	{
		return new FindGroupDirectPacketShowListBoundaryTraceSchema(
			SchemaVersion,
			TraceName: "cm-find-group-direct-show-list-boundary",
			SupportedActions:
			[
				Action(
					0,
					FindGroupDirectPacketShowListTraceListKind.Recruitments,
					"FindGroupService.showRecruitments(player)",
					"SM_FIND_GROUP action 0"),
				Action(
					4,
					FindGroupDirectPacketShowListTraceListKind.Applications,
					"FindGroupService.showApplications(player)",
					"SM_FIND_GROUP action 4"),
			],
			RequiredFields:
			[
				Field("schemaVersion", "Trace schema version. Current value is 1."),
				Field("traceName", "Stable trace family name."),
				Field("traceSource", "Java or CSharp trace source identifier."),
				Field("action", "Parsed CM_FIND_GROUP action. Supported values are 0 and 4."),
				Field("boundaryAccepted", "Whether GameServerConnection.ProcessPacketAsync accepted the triggering client packet."),
				Field("activePlayerObjectId", "Object id of the triggering player."),
				Field("activePlayerRace", "Race used by Java show-list filtering."),
				Field("serverEpochSeconds", "Server second written into the SM_FIND_GROUP show-list packet header."),
				Field("listKind", "Recruitments for action 0 or Applications for action 4."),
				Field("visibleEntryObjectIds", "Filtered singleton entry ids visible to the active player's race in materialized packet order."),
				Field("directPacketRecipientObjectId", "Recipient object id selected by Java PacketSendUtility.sendPacket."),
				Field("directPacketType", "Expected packet type. For this schema it is SmFindGroup."),
				Field("directPacketAction", "Expected SM_FIND_GROUP action id emitted to the recipient."),
				Field("executorInvokedFromBoundary", "Whether the direct-packet executor was invoked from the CmFindGroup boundary."),
				Field("registrySendObserved", "Whether the connection registry send was observed."),
				Field("worldBroadcastCount", "Must remain 0 for show-list direct traces."),
				Field("inviteDispatchCount", "Must remain 0 for show-list direct traces."),
			],
			"Non-live schema only; use for future Java/C# trace exports after live boundary capture exists.",
			"Java sources reviewed: CM_FIND_GROUP.runImpl actions 0 and 4; FindGroupService.showRecruitments/showApplications.");
	}

	public static FindGroupDirectPacketShowListBoundaryTraceExport CreateSampleExport(int action)
	{
		var mapping = CreateSchema().SupportedActions.Single(item => item.Action == action);
		return new FindGroupDirectPacketShowListBoundaryTraceExport(
			SchemaVersion,
			TraceName: "cm-find-group-direct-show-list-boundary",
			TraceSource: FindGroupDirectPacketShowListTraceSource.CSharp,
			action,
			BoundaryAccepted: false,
			ActivePlayerObjectId: 0,
			ActivePlayerRace: string.Empty,
			ServerEpochSeconds: 0,
			mapping.ListKind,
			VisibleEntryObjectIds: [],
			DirectPacketRecipientObjectId: 0,
			DirectPacketType: "SmFindGroup",
			DirectPacketAction: action,
			ExecutorInvokedFromBoundary: false,
			RegistrySendObserved: false,
			WorldBroadcastCount: 0,
			InviteDispatchCount: 0);
	}

	private static FindGroupDirectPacketShowListActionSchema Action(
		int action,
		FindGroupDirectPacketShowListTraceListKind listKind,
		string javaMethod,
		string javaPacket)
	{
		return new FindGroupDirectPacketShowListActionSchema(action, listKind, javaMethod, javaPacket);
	}

	private static FindGroupDirectPacketShowListBoundaryTraceField Field(string name, string requirement)
	{
		return new FindGroupDirectPacketShowListBoundaryTraceField(name, requirement);
	}
}

public enum FindGroupDirectPacketShowListTraceSource
{
	Java,
	CSharp,
}

public enum FindGroupDirectPacketShowListTraceListKind
{
	Recruitments,
	Applications,
}

public sealed record FindGroupDirectPacketShowListBoundaryTraceSchema(
	int SchemaVersion,
	string TraceName,
	IReadOnlyList<FindGroupDirectPacketShowListActionSchema> SupportedActions,
	IReadOnlyList<FindGroupDirectPacketShowListBoundaryTraceField> RequiredFields,
	string BoundaryNote,
	string JavaSource);

public sealed record FindGroupDirectPacketShowListActionSchema(
	int Action,
	FindGroupDirectPacketShowListTraceListKind ListKind,
	string JavaMethod,
	string JavaPacket);

public sealed record FindGroupDirectPacketShowListBoundaryTraceField(
	string Name,
	string Requirement);

public sealed record FindGroupDirectPacketShowListBoundaryTraceExport(
	int SchemaVersion,
	string TraceName,
	FindGroupDirectPacketShowListTraceSource TraceSource,
	int Action,
	bool BoundaryAccepted,
	int ActivePlayerObjectId,
	string ActivePlayerRace,
	int ServerEpochSeconds,
	FindGroupDirectPacketShowListTraceListKind ListKind,
	IReadOnlyList<int> VisibleEntryObjectIds,
	int DirectPacketRecipientObjectId,
	string DirectPacketType,
	int DirectPacketAction,
	bool ExecutorInvokedFromBoundary,
	bool RegistrySendObserved,
	int WorldBroadcastCount,
	int InviteDispatchCount);
