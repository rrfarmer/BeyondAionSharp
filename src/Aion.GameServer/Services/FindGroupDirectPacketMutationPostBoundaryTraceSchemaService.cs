namespace Aion.GameServer.Services;

public static class FindGroupDirectPacketMutationPostBoundaryTraceSchemaService
{
	public const int SchemaVersion = 1;

	public static FindGroupDirectPacketMutationPostBoundaryTraceSchema CreateSchema()
	{
		return new FindGroupDirectPacketMutationPostBoundaryTraceSchema(
			SchemaVersion,
			TraceName: "cm-find-group-direct-mutation-post-boundary",
			SupportedActions:
			[
				Action(
					2,
					FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment,
					"FindGroupService.addRecruitment(player, message, groupType)",
					"SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_OFFER_PARTY_POSTED",
					postedSystemMessageId: 1400392,
					refreshedShowListAction: 0),
				Action(
					6,
					FindGroupDirectPacketMutationPostTraceMutationKind.Application,
					"FindGroupService.addApplication(player, message, groupType, classId, level)",
					"SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_SEEK_PARTY_POSTED",
					postedSystemMessageId: 1400393,
					refreshedShowListAction: 4),
			],
			RequiredFields:
			[
				Field("schemaVersion", "Trace schema version. Current value is 1."),
				Field("traceName", "Stable trace family name."),
				Field("traceSource", "Java or CSharp trace source identifier."),
				Field("action", "Parsed CM_FIND_GROUP action. Supported values are 2 and 6."),
				Field("boundaryAccepted", "Whether GameServerConnection.ProcessPacketAsync accepted the triggering client packet."),
				Field("activePlayerObjectId", "Object id of the triggering player."),
				Field("activePlayerRace", "Race used by Java refreshed show-list filtering."),
				Field("serverEpochSeconds", "Server second written into the refreshed SM_FIND_GROUP show-list packet header."),
				Field("mutationKind", "Recruitment for action 2 or Application for action 6."),
				Field("mutatedEntryObjectId", "Recruitment player/team id for action 2, or player object id for action 6."),
				Field("stateMutationRecordedBeforeDirectPackets", "Whether singleton state mutation was recorded before posted message and refreshed list sends."),
				Field("postedSystemMessageRecipientObjectId", "Recipient object id selected by Java PacketSendUtility.sendPacket for the posted system message."),
				Field("postedSystemMessageType", "Expected packet type. For this schema it is SmSystemMessage."),
				Field("postedSystemMessageId", "Expected Java system message id for the posted notification."),
				Field("refreshedListRecipientObjectId", "Recipient object id selected by Java PacketSendUtility.sendPacket for the refreshed show-list."),
				Field("refreshedListPacketType", "Expected packet type. For this schema it is SmFindGroup."),
				Field("refreshedListAction", "Expected SM_FIND_GROUP action id emitted after the posted system message."),
				Field("visibleEntryObjectIdsAfterMutation", "Filtered singleton entry ids visible to the active player's race after mutation, in materialized packet order."),
				Field("executorInvokedFromBoundary", "Whether the direct-packet executor was invoked from the CmFindGroup boundary."),
				Field("registrySendsObservedInOrder", "Whether registry sends observed posted system message before refreshed show-list."),
				Field("worldBroadcastCount", "Must remain 0 for mutation-post direct traces."),
				Field("inviteDispatchCount", "Must remain 0 for mutation-post direct traces."),
			],
			"Non-live schema only; use for future Java/C# action 2 and 6 trace exports after live boundary capture exists.",
			"Java sources reviewed: CM_FIND_GROUP.runImpl actions 2 and 6; FindGroupService.addRecruitment/addApplication.");
	}

	public static FindGroupDirectPacketMutationPostBoundaryTraceExport CreateSampleExport(int action)
	{
		var mapping = CreateSchema().SupportedActions.Single(item => item.Action == action);
		return new FindGroupDirectPacketMutationPostBoundaryTraceExport(
			SchemaVersion,
			TraceName: "cm-find-group-direct-mutation-post-boundary",
			TraceSource: FindGroupDirectPacketMutationPostTraceSource.CSharp,
			action,
			BoundaryAccepted: false,
			ActivePlayerObjectId: 0,
			ActivePlayerRace: string.Empty,
			ServerEpochSeconds: 0,
			mapping.MutationKind,
			MutatedEntryObjectId: 0,
			StateMutationRecordedBeforeDirectPackets: false,
			PostedSystemMessageRecipientObjectId: 0,
			PostedSystemMessageType: "SmSystemMessage",
			mapping.PostedSystemMessageId,
			RefreshedListRecipientObjectId: 0,
			RefreshedListPacketType: "SmFindGroup",
			mapping.RefreshedShowListAction,
			VisibleEntryObjectIdsAfterMutation: [],
			ExecutorInvokedFromBoundary: false,
			RegistrySendsObservedInOrder: false,
			WorldBroadcastCount: 0,
			InviteDispatchCount: 0);
	}

	public static FindGroupDirectPacketMutationPostBoundaryTraceExportProjection CreateExportFromDisabledPlan(
		FindGroupConnectionClientActionCompositionPlan compositionPlan)
	{
		var action = compositionPlan.Action.Action;
		if (action is not 2 and not 6)
		{
			return Failed(
				FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.UnsupportedAction,
				action,
				"Only CM_FIND_GROUP actions 2 and 6 are supported by the mutation-post trace schema.");
		}

		if (compositionPlan.Status != FindGroupConnectionClientActionCompositionStatus.ComposedDisabledPlan
			|| compositionPlan.ActivePlayer == null)
		{
			return Failed(
				FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.MissingActivePlayer,
				action,
				"Disabled boundary plan did not include the active player required by Java CM_FIND_GROUP.runImpl.");
		}

		if (compositionPlan.ClientActionPlan == null)
		{
			return Failed(
				FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.MissingClientActionPlan,
				action,
				"Disabled boundary plan did not include a client action plan.");
		}

		if (compositionPlan.ShouldDispatchLiveSideEffects || compositionPlan.ClientActionPlan.DispatchLiveSideEffects)
		{
			return Failed(
				FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.LiveSideEffectsRequested,
				action,
				"Mutation-post export projection only accepts disabled non-live boundary plans.");
		}

		var intentPlan = FindGroupConnectionBoundarySideEffectCompositionEvidenceService.CreateIntentPlan(compositionPlan);
		if (intentPlan.DirectPacketIntents.Count != 2
			|| intentPlan.WorldBroadcastIntents.Count != 0
			|| intentPlan.InviteIntent != null)
		{
			return Failed(
				FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.UnexpectedSideEffectShape,
				action,
				"Mutation-post export projection requires exactly two direct packets and no world broadcast or invite intent.");
		}

		var clientPlan = compositionPlan.ClientActionPlan;
		var activePlayer = compositionPlan.ActivePlayer;
		var schema = CreateSchema();
		var mapping = schema.SupportedActions.Single(item => item.Action == action);
		var postedIntent = intentPlan.DirectPacketIntents[0];
		var refreshedIntent = intentPlan.DirectPacketIntents[1];

		var mutation = CreateMutationProjectionFacts(action, clientPlan);
		if (mutation == null)
		{
			return Failed(
				FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.MissingMutationPlan,
				action,
				"Disabled boundary plan did not include the Java-equivalent added mutation and refreshed show-list plan.");
		}

		var export = new FindGroupDirectPacketMutationPostBoundaryTraceExport(
			SchemaVersion,
			schema.TraceName,
			FindGroupDirectPacketMutationPostTraceSource.CSharp,
			action,
			BoundaryAccepted: true,
			activePlayer.ObjectId,
			activePlayer.Race,
			mutation.ServerEpochSeconds,
			mapping.MutationKind,
			mutation.MutatedEntryObjectId,
			StateMutationRecordedBeforeDirectPackets: true,
			postedIntent.RecipientObjectId,
			PostedSystemMessageType: "SmSystemMessage",
			mapping.PostedSystemMessageId,
			refreshedIntent.RecipientObjectId,
			RefreshedListPacketType: "SmFindGroup",
			mapping.RefreshedShowListAction,
			mutation.VisibleEntryObjectIdsAfterMutation,
			ExecutorInvokedFromBoundary: false,
			RegistrySendsObservedInOrder: false,
			WorldBroadcastCount: 0,
			InviteDispatchCount: 0);

		return new FindGroupDirectPacketMutationPostBoundaryTraceExportProjection(
			FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.Created,
			export,
			"Projected from a disabled CM_FIND_GROUP boundary plan; executor and registry ordering observations remain false.");
	}

	private static FindGroupDirectPacketMutationPostActionSchema Action(
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind,
		string javaMethod,
		string javaPostedSystemMessage,
		int postedSystemMessageId,
		int refreshedShowListAction)
	{
		return new FindGroupDirectPacketMutationPostActionSchema(
			action,
			mutationKind,
			javaMethod,
			javaPostedSystemMessage,
			postedSystemMessageId,
			refreshedShowListAction);
	}

	private static FindGroupDirectPacketMutationPostBoundaryTraceField Field(string name, string requirement)
	{
		return new FindGroupDirectPacketMutationPostBoundaryTraceField(name, requirement);
	}

	private static FindGroupDirectPacketMutationPostProjectionFacts? CreateMutationProjectionFacts(
		int action,
		FindGroupClientActionPlan clientPlan)
	{
		if (action == 2)
		{
			var mutation = clientPlan.RecruitmentMutationPlan;
			if (mutation?.Status != FindGroupRecruitmentPlanStatus.Added
				|| mutation.CurrentRecruitment == null
				|| mutation.ShowRecruitmentsPlan == null)
			{
				return null;
			}

			return new FindGroupDirectPacketMutationPostProjectionFacts(
				mutation.CurrentRecruitment.ObjectId,
				mutation.ShowRecruitmentsPlan.LastUpdate,
				mutation.ShowRecruitmentsPlan.Recruitments.Select(recruitment => recruitment.ObjectId).ToArray());
		}

		var applicationMutation = clientPlan.ApplicationMutationPlan;
		if (applicationMutation?.Status != FindGroupApplicationPlanStatus.Added
			|| applicationMutation.CurrentApplication == null
			|| applicationMutation.ShowApplicationsPlan == null)
		{
			return null;
		}

		return new FindGroupDirectPacketMutationPostProjectionFacts(
			applicationMutation.CurrentApplication.PlayerObjectId,
			applicationMutation.ShowApplicationsPlan.LastUpdate,
			applicationMutation.ShowApplicationsPlan.Applications.Select(application => application.PlayerObjectId).ToArray());
	}

	private static FindGroupDirectPacketMutationPostBoundaryTraceExportProjection Failed(
		FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus status,
		int action,
		string reason)
	{
		var export = CreateSampleExport(action is 2 or 6 ? action : 2);
		return new FindGroupDirectPacketMutationPostBoundaryTraceExportProjection(status, export with { Action = action }, reason);
	}
}

public enum FindGroupDirectPacketMutationPostTraceSource
{
	Java,
	CSharp,
}

public enum FindGroupDirectPacketMutationPostTraceMutationKind
{
	Recruitment,
	Application,
}

public sealed record FindGroupDirectPacketMutationPostBoundaryTraceSchema(
	int SchemaVersion,
	string TraceName,
	IReadOnlyList<FindGroupDirectPacketMutationPostActionSchema> SupportedActions,
	IReadOnlyList<FindGroupDirectPacketMutationPostBoundaryTraceField> RequiredFields,
	string BoundaryNote,
	string JavaSource);

public sealed record FindGroupDirectPacketMutationPostActionSchema(
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string JavaMethod,
	string JavaPostedSystemMessage,
	int PostedSystemMessageId,
	int RefreshedShowListAction);

public sealed record FindGroupDirectPacketMutationPostBoundaryTraceField(
	string Name,
	string Requirement);

public sealed record FindGroupDirectPacketMutationPostBoundaryTraceExport(
	int SchemaVersion,
	string TraceName,
	FindGroupDirectPacketMutationPostTraceSource TraceSource,
	int Action,
	bool BoundaryAccepted,
	int ActivePlayerObjectId,
	string ActivePlayerRace,
	int ServerEpochSeconds,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	int MutatedEntryObjectId,
	bool StateMutationRecordedBeforeDirectPackets,
	int PostedSystemMessageRecipientObjectId,
	string PostedSystemMessageType,
	int PostedSystemMessageId,
	int RefreshedListRecipientObjectId,
	string RefreshedListPacketType,
	int RefreshedListAction,
	IReadOnlyList<int> VisibleEntryObjectIdsAfterMutation,
	bool ExecutorInvokedFromBoundary,
	bool RegistrySendsObservedInOrder,
	int WorldBroadcastCount,
	int InviteDispatchCount);

public enum FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus
{
	Created,
	UnsupportedAction,
	MissingActivePlayer,
	MissingClientActionPlan,
	LiveSideEffectsRequested,
	UnexpectedSideEffectShape,
	MissingMutationPlan,
}

public sealed record FindGroupDirectPacketMutationPostBoundaryTraceExportProjection(
	FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus Status,
	FindGroupDirectPacketMutationPostBoundaryTraceExport Export,
	string Reason);

sealed record FindGroupDirectPacketMutationPostProjectionFacts(
	int MutatedEntryObjectId,
	int ServerEpochSeconds,
	IReadOnlyList<int> VisibleEntryObjectIdsAfterMutation);
