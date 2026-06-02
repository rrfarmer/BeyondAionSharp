namespace Aion.GameServer.Services;

public enum FindGroupMutationPostRegistryObservationTraceContractStatus
{
	BlockedPendingLiveBoundaryTrace,
	Ready,
}

public enum FindGroupMutationPostRegistryObservationRequirementKind
{
	BoundaryExecutorInvocation,
	PostedSystemMessageSend,
	RefreshedShowListSend,
	RegistrySendOrdering,
	NoUnexpectedSideEffects,
	RuntimeTraceFields,
}

public enum FindGroupMutationPostRegistryObservationRequirementStatus
{
	NonLiveSchemaAvailable,
	BlockedPendingLiveBoundary,
	Ready,
}

public sealed record FindGroupMutationPostRegistryObservationRequirementRow(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	FindGroupMutationPostRegistryObservationRequirementKind Kind,
	FindGroupMutationPostRegistryObservationRequirementStatus Status,
	string RequiredObservation,
	string TraceFields,
	string Notes);

public sealed record FindGroupMutationPostRegistryObservationTraceContract(
	FindGroupMutationPostRegistryObservationTraceContractStatus Status,
	IReadOnlyList<int> Actions,
	IReadOnlyList<FindGroupMutationPostRegistryObservationRequirementRow> Requirements,
	bool RequiresExecutorInvokedFromBoundary,
	bool RequiresRegistrySendsObservedInOrder,
	bool RequiresTwoDirectSendsPerAction,
	bool RequiresZeroWorldBroadcasts,
	bool RequiresZeroInviteDispatches,
	bool ReadyForRuntimeComparison,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: live-boundary registry observation contract for future CM_FIND_GROUP
/// action 2/6 mutation-post traces. This names required evidence only; it performs no sends.
/// </summary>
public static class FindGroupMutationPostRegistryObservationTraceContractService
{
	public static FindGroupMutationPostRegistryObservationTraceContract Create()
	{
		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var rows = new List<FindGroupMutationPostRegistryObservationRequirementRow>();

		foreach (var action in schema.SupportedActions)
		{
			AddActionRows(rows, action);
		}

		return new FindGroupMutationPostRegistryObservationTraceContract(
			FindGroupMutationPostRegistryObservationTraceContractStatus.BlockedPendingLiveBoundaryTrace,
			schema.SupportedActions.Select(action => action.Action).ToArray(),
			rows,
			RequiresExecutorInvokedFromBoundary: true,
			RequiresRegistrySendsObservedInOrder: true,
			RequiresTwoDirectSendsPerAction: true,
			RequiresZeroWorldBroadcasts: true,
			RequiresZeroInviteDispatches: true,
			ReadyForRuntimeComparison: false,
			schema.TraceName,
			schema.JavaSource,
			IsLive: false);
	}

	private static void AddActionRows(
		ICollection<FindGroupMutationPostRegistryObservationRequirementRow> rows,
		FindGroupDirectPacketMutationPostActionSchema action)
	{
		Add(rows,
			action,
			FindGroupMutationPostRegistryObservationRequirementKind.BoundaryExecutorInvocation,
			FindGroupMutationPostRegistryObservationRequirementStatus.BlockedPendingLiveBoundary,
			"Observe FindGroupSideEffectDispatchExecutorService invoked by the live CmFindGroup boundary for this action.",
			"executorInvokedFromBoundary=true, boundaryAccepted=true",
			"Disabled opt-in executor evidence is not sufficient for live mutation-post parity.");

		Add(rows,
			action,
			FindGroupMutationPostRegistryObservationRequirementKind.PostedSystemMessageSend,
			FindGroupMutationPostRegistryObservationRequirementStatus.BlockedPendingLiveBoundary,
			$"Observe registry direct send #1 to the active player as SmSystemMessage id {action.PostedSystemMessageId}.",
			"postedSystemMessageRecipientObjectId=activePlayerObjectId, postedSystemMessageType=SmSystemMessage, postedSystemMessageId",
			"Java sends the posted system message immediately after the singleton mutation.");

		Add(rows,
			action,
			FindGroupMutationPostRegistryObservationRequirementKind.RefreshedShowListSend,
			FindGroupMutationPostRegistryObservationRequirementStatus.BlockedPendingLiveBoundary,
			$"Observe registry direct send #2 to the active player as SmFindGroup action {action.RefreshedShowListAction}.",
			"refreshedListRecipientObjectId=activePlayerObjectId, refreshedListPacketType=SmFindGroup, refreshedListAction, visibleEntryObjectIdsAfterMutation",
			"Java refreshes the active player's race-filtered list after the posted system message.");

		Add(rows,
			action,
			FindGroupMutationPostRegistryObservationRequirementKind.RegistrySendOrdering,
			FindGroupMutationPostRegistryObservationRequirementStatus.BlockedPendingLiveBoundary,
			"Observe exactly posted system message before refreshed show-list for this triggering client packet.",
			"registrySendsObservedInOrder=true",
			"Ordering must be observed at the connection registry, not inferred only from intent list order.");

		Add(rows,
			action,
			FindGroupMutationPostRegistryObservationRequirementKind.NoUnexpectedSideEffects,
			FindGroupMutationPostRegistryObservationRequirementStatus.BlockedPendingLiveBoundary,
			"Observe no world broadcasts and no invite dispatches for this mutation-post direct trace.",
			"worldBroadcastCount=0, inviteDispatchCount=0",
			"Actions 2 and 6 are direct packet mutation-post flows, not broadcast or invite flows.");

		Add(rows,
			action,
			FindGroupMutationPostRegistryObservationRequirementKind.RuntimeTraceFields,
			FindGroupMutationPostRegistryObservationRequirementStatus.NonLiveSchemaAvailable,
			"Serialize one schema-v1 runtime row for this action after live boundary/registry observation exists.",
			"schemaVersion, traceName, traceSource=CSharp, action, mutationKind, mutatedEntryObjectId, stateMutationRecordedBeforeDirectPackets",
			"Schema field names are available as non-live metadata; runtime values still require live capture.");
	}

	private static void Add(
		ICollection<FindGroupMutationPostRegistryObservationRequirementRow> rows,
		FindGroupDirectPacketMutationPostActionSchema action,
		FindGroupMutationPostRegistryObservationRequirementKind kind,
		FindGroupMutationPostRegistryObservationRequirementStatus status,
		string requiredObservation,
		string traceFields,
		string notes)
	{
		rows.Add(new FindGroupMutationPostRegistryObservationRequirementRow(
			rows.Count + 1,
			action.Action,
			action.MutationKind,
			kind,
			status,
			requiredObservation,
			traceFields,
			notes));
	}
}
