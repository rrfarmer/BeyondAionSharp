namespace Aion.GameServer.Services;

public enum FindGroupMutationPostGuardedFixtureResultContractStatus
{
	BlockedMissingGuardedFixtureRows,
	ReadyForComparisonHandoff,
}

public enum FindGroupMutationPostGuardedFixtureResultRequirementKind
{
	ExplicitTraceGuard,
	ProductionDispatchGuard,
	ActionTwoLiveBoundaryRow,
	ActionSixLiveBoundaryRow,
	ExecutorObservation,
	RegistryObservation,
	SideEffectGuard,
	ComparisonHandoff,
}

public enum FindGroupMutationPostGuardedFixtureResultRequirementStatus
{
	SatisfiedByContract,
	SatisfiedByLiveBoundaryRow,
	BlockedMissingLiveBoundaryRow,
}

public enum FindGroupMutationPostGuardedFixtureCandidateRowStatus
{
	AcceptedLiveBoundaryRow,
	RejectedUnsupportedAction,
	RejectedNonCSharpSource,
	RejectedMissingBoundaryAcceptance,
	RejectedMissingExecutorObservation,
	RejectedMissingRegistryObservation,
	RejectedUnexpectedSideEffects,
	RejectedUnexpectedPacketShape,
}

public sealed record FindGroupMutationPostGuardedFixtureResultRequirement(
	int Order,
	FindGroupMutationPostGuardedFixtureResultRequirementKind Kind,
	int? Action,
	FindGroupMutationPostGuardedFixtureResultRequirementStatus Status,
	bool BlocksComparisonHandoff,
	string Evidence,
	string JavaSource,
	string CSharpTarget,
	string Notes);

public sealed record FindGroupMutationPostGuardedFixtureCandidateRow(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	FindGroupMutationPostGuardedFixtureCandidateRowStatus Status,
	bool IsShapeValid,
	bool IsLiveBoundaryEvidence,
	bool BoundaryAccepted,
	bool ExecutorInvokedFromBoundary,
	bool RegistrySendsObservedInOrder,
	string Evidence);

public sealed record FindGroupMutationPostGuardedFixtureResultContract(
	FindGroupMutationPostGuardedFixtureResultContractStatus Status,
	IReadOnlyList<FindGroupMutationPostGuardedFixtureResultRequirement> Requirements,
	IReadOnlyList<FindGroupMutationPostGuardedFixtureCandidateRow> CandidateRows,
	IReadOnlyList<FindGroupMutationPostGuardedFixtureCandidateRow> AcceptedLiveRows,
	string FixtureClassName,
	string TraceGuardName,
	string TraceName,
	bool RequiresExplicitTraceGuard,
	bool IsProductionCmFindGroupDispatchEnabled,
	bool ShouldSendPacketsByDefault,
	bool HasActionTwoLiveRow,
	bool HasActionSixLiveRow,
	bool ReadyForComparisonHandoff,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live result contract for future CM_FIND_GROUP action
/// 2/6 guarded boundary fixture rows. The contract classifies supplied rows only;
/// it does not invoke ProcessPacketAsync, execute sends, or create live evidence.
/// </summary>
public static class FindGroupMutationPostGuardedFixtureResultContractService
{
	public static FindGroupMutationPostGuardedFixtureResultContract Create(
		FindGroupMutationPostGuardedLiveBoundaryFixtureSkeleton? skeleton = null,
		IReadOnlyList<FindGroupDirectPacketMutationPostBoundaryTraceExport>? candidateRows = null)
	{
		skeleton ??= FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.Create();
		candidateRows ??= [];

		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var classifiedRows = candidateRows
			.Select((row, index) => ClassifyRow(index + 1, row, schema))
			.ToArray();
		var acceptedRows = classifiedRows.Where(row => row.IsLiveBoundaryEvidence).ToArray();
		var hasActionTwo = acceptedRows.Any(row => row.Action == 2);
		var hasActionSix = acceptedRows.Any(row => row.Action == 6);
		var status = hasActionTwo && hasActionSix
			? FindGroupMutationPostGuardedFixtureResultContractStatus.ReadyForComparisonHandoff
			: FindGroupMutationPostGuardedFixtureResultContractStatus.BlockedMissingGuardedFixtureRows;

		var requirements = new List<FindGroupMutationPostGuardedFixtureResultRequirement>();
		Add(requirements,
			FindGroupMutationPostGuardedFixtureResultRequirementKind.ExplicitTraceGuard,
			action: null,
			FindGroupMutationPostGuardedFixtureResultRequirementStatus.SatisfiedByContract,
			blocks: false,
			$"traceGuard={FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.TraceGuardName}; requiresGuard={skeleton.RequiresExplicitTraceGuard}",
			skeleton.JavaSource,
			skeleton.FixtureClassName,
			"Fixture rows must be captured only under the explicit trace guard.");
		Add(requirements,
			FindGroupMutationPostGuardedFixtureResultRequirementKind.ProductionDispatchGuard,
			action: null,
			FindGroupMutationPostGuardedFixtureResultRequirementStatus.SatisfiedByContract,
			blocks: false,
			$"productionDispatch={skeleton.IsProductionCmFindGroupDispatchEnabled}; sendsPacketsByDefault=False",
			"CM_FIND_GROUP.runImpl action 2/6 dispatch remains Java source of truth.",
			"GameServerConnection.ProcessPacketAsync case CmFindGroup",
			"Production CmFindGroup dispatch must remain deferred while this fixture contract is non-live.");
		AddActionRequirement(requirements, 2, hasActionTwo, "FindGroupService.addRecruitment(player, message, groupType)");
		AddActionRequirement(requirements, 6, hasActionSix, "FindGroupService.addApplication(player, message, groupType, classId, level)");
		Add(requirements,
			FindGroupMutationPostGuardedFixtureResultRequirementKind.ExecutorObservation,
			action: null,
			acceptedRows.All(row => row.ExecutorInvokedFromBoundary) && acceptedRows.Length == 2
				? FindGroupMutationPostGuardedFixtureResultRequirementStatus.SatisfiedByLiveBoundaryRow
				: FindGroupMutationPostGuardedFixtureResultRequirementStatus.BlockedMissingLiveBoundaryRow,
			blocks: acceptedRows.Length != 2,
			$"acceptedRows={acceptedRows.Length}; executorRows={acceptedRows.Count(row => row.ExecutorInvokedFromBoundary)}",
			"Java PacketSendUtility.sendPacket calls occur from CM_FIND_GROUP.runImpl action 2/6.",
			"FindGroupSideEffectDispatchExecutorService guarded boundary observation",
			"Executor observation must come from the guarded boundary, not from an opt-in executor call outside the boundary.");
		Add(requirements,
			FindGroupMutationPostGuardedFixtureResultRequirementKind.RegistryObservation,
			action: null,
			acceptedRows.All(row => row.RegistrySendsObservedInOrder) && acceptedRows.Length == 2
				? FindGroupMutationPostGuardedFixtureResultRequirementStatus.SatisfiedByLiveBoundaryRow
				: FindGroupMutationPostGuardedFixtureResultRequirementStatus.BlockedMissingLiveBoundaryRow,
			blocks: acceptedRows.Length != 2,
			$"acceptedRows={acceptedRows.Length}; registryRows={acceptedRows.Count(row => row.RegistrySendsObservedInOrder)}",
			"Java posted system message send precedes the refreshed SM_FIND_GROUP list send.",
			"IGameClientConnectionRegistry direct-send observation",
			"Registry observation must prove posted system message before refreshed list for actions 2 and 6.");
		Add(requirements,
			FindGroupMutationPostGuardedFixtureResultRequirementKind.SideEffectGuard,
			action: null,
			acceptedRows.Length == 2
				? FindGroupMutationPostGuardedFixtureResultRequirementStatus.SatisfiedByLiveBoundaryRow
				: FindGroupMutationPostGuardedFixtureResultRequirementStatus.BlockedMissingLiveBoundaryRow,
			blocks: acceptedRows.Length != 2,
			$"acceptedRows={acceptedRows.Length}; rejectedSideEffects={classifiedRows.Count(row => row.Status == FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedUnexpectedSideEffects)}",
			"FindGroupService.addRecruitment/addApplication emit direct packets only for mutation-post traces.",
			"FindGroupDirectPacketMutationPostBoundaryTraceExport",
			"World broadcast and invite counts must remain zero for this fixture contract.");
		Add(requirements,
			FindGroupMutationPostGuardedFixtureResultRequirementKind.ComparisonHandoff,
			action: null,
			status == FindGroupMutationPostGuardedFixtureResultContractStatus.ReadyForComparisonHandoff
				? FindGroupMutationPostGuardedFixtureResultRequirementStatus.SatisfiedByLiveBoundaryRow
				: FindGroupMutationPostGuardedFixtureResultRequirementStatus.BlockedMissingLiveBoundaryRow,
			blocks: status != FindGroupMutationPostGuardedFixtureResultContractStatus.ReadyForComparisonHandoff,
			$"action2Live={hasActionTwo}; action6Live={hasActionSix}; candidateRows={classifiedRows.Length}",
			schema.JavaSource,
			"FindGroupMutationPostComparisonInputEnvelopeService",
			"Only accepted live boundary rows should be handed to the comparison envelope.");

		return new FindGroupMutationPostGuardedFixtureResultContract(
			status,
			requirements.ToArray(),
			classifiedRows,
			acceptedRows,
			skeleton.FixtureClassName,
			FindGroupMutationPostGuardedLiveBoundaryFixtureSkeletonService.TraceGuardName,
			skeleton.TraceName,
			skeleton.RequiresExplicitTraceGuard,
			skeleton.IsProductionCmFindGroupDispatchEnabled,
			ShouldSendPacketsByDefault: false,
			hasActionTwo,
			hasActionSix,
			status == FindGroupMutationPostGuardedFixtureResultContractStatus.ReadyForComparisonHandoff,
			schema.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostGuardedFixtureCandidateRow ClassifyRow(
		int order,
		FindGroupDirectPacketMutationPostBoundaryTraceExport row,
		FindGroupDirectPacketMutationPostBoundaryTraceSchema schema)
	{
		var status = DetermineRowStatus(row, schema);
		var isShapeValid = status != FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedUnsupportedAction
			&& status != FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedUnexpectedPacketShape;
		var isLive = status == FindGroupMutationPostGuardedFixtureCandidateRowStatus.AcceptedLiveBoundaryRow;

		return new FindGroupMutationPostGuardedFixtureCandidateRow(
			order,
			row.Action,
			row.MutationKind,
			status,
			isShapeValid,
			isLive,
			row.BoundaryAccepted,
			row.ExecutorInvokedFromBoundary,
			row.RegistrySendsObservedInOrder,
			$"source={row.TraceSource}; boundary={row.BoundaryAccepted}; executor={row.ExecutorInvokedFromBoundary}; registry={row.RegistrySendsObservedInOrder}; posted={row.PostedSystemMessageId}; refreshed={row.RefreshedListAction}; broadcasts={row.WorldBroadcastCount}; invites={row.InviteDispatchCount}");
	}

	private static FindGroupMutationPostGuardedFixtureCandidateRowStatus DetermineRowStatus(
		FindGroupDirectPacketMutationPostBoundaryTraceExport row,
		FindGroupDirectPacketMutationPostBoundaryTraceSchema schema)
	{
		var action = schema.SupportedActions.SingleOrDefault(item => item.Action == row.Action);
		if (action == null)
			return FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedUnsupportedAction;
		if (row.PostedSystemMessageType != "SmSystemMessage"
			|| row.PostedSystemMessageId != action.PostedSystemMessageId
			|| row.RefreshedListPacketType != "SmFindGroup"
			|| row.RefreshedListAction != action.RefreshedShowListAction)
		{
			return FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedUnexpectedPacketShape;
		}
		if (row.TraceSource != FindGroupDirectPacketMutationPostTraceSource.CSharp)
			return FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedNonCSharpSource;
		if (!row.BoundaryAccepted)
			return FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedMissingBoundaryAcceptance;
		if (!row.ExecutorInvokedFromBoundary)
			return FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedMissingExecutorObservation;
		if (!row.RegistrySendsObservedInOrder)
			return FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedMissingRegistryObservation;
		if (row.WorldBroadcastCount != 0 || row.InviteDispatchCount != 0)
			return FindGroupMutationPostGuardedFixtureCandidateRowStatus.RejectedUnexpectedSideEffects;

		return FindGroupMutationPostGuardedFixtureCandidateRowStatus.AcceptedLiveBoundaryRow;
	}

	private static void AddActionRequirement(
		ICollection<FindGroupMutationPostGuardedFixtureResultRequirement> requirements,
		int action,
		bool hasLiveRow,
		string javaMethod)
	{
		Add(requirements,
			action == 2
				? FindGroupMutationPostGuardedFixtureResultRequirementKind.ActionTwoLiveBoundaryRow
				: FindGroupMutationPostGuardedFixtureResultRequirementKind.ActionSixLiveBoundaryRow,
			action,
			hasLiveRow
				? FindGroupMutationPostGuardedFixtureResultRequirementStatus.SatisfiedByLiveBoundaryRow
				: FindGroupMutationPostGuardedFixtureResultRequirementStatus.BlockedMissingLiveBoundaryRow,
			blocks: !hasLiveRow,
			$"action={action}; liveBoundaryRow={hasLiveRow}",
			javaMethod,
			"FindGroupDirectPacketMutationPostBoundaryTraceExport",
			"Future fixture result must hold one accepted C# live boundary row for this Java action.");
	}

	private static void Add(
		ICollection<FindGroupMutationPostGuardedFixtureResultRequirement> requirements,
		FindGroupMutationPostGuardedFixtureResultRequirementKind kind,
		int? action,
		FindGroupMutationPostGuardedFixtureResultRequirementStatus status,
		bool blocks,
		string evidence,
		string javaSource,
		string csharpTarget,
		string notes)
	{
		requirements.Add(new FindGroupMutationPostGuardedFixtureResultRequirement(
			requirements.Count + 1,
			kind,
			action,
			status,
			blocks,
			evidence,
			javaSource,
			csharpTarget,
			notes));
	}
}
