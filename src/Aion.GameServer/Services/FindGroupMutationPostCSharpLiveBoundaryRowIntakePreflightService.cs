namespace Aion.GameServer.Services;

public enum FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus
{
	BlockedMissingAcceptedBoundaryRows,
	ReadyForJavaArtifactPairingRuntimeComparisonBlocked,
}

public enum FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate
{
	ActionTwoAcceptedBoundaryRow,
	ActionSixAcceptedBoundaryRow,
	BoundaryAccepted,
	ExecutorInvokedFromBoundary,
	RegistrySendsObservedInOrder,
	PostedSystemMessageBeforeRefreshedList,
	ZeroWorldBroadcasts,
	ZeroInviteDispatches,
	JavaArtifactPairingIdentity,
}

public sealed record FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightRow(
	int Order,
	FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate Gate,
	int? Action,
	bool Satisfied,
	bool BlocksRuntimeComparison,
	string RequiredEvidence,
	string CurrentEvidence,
	string JavaSource,
	string Notes);

public sealed record FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflight(
	FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus Status,
	IReadOnlyList<FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightRow> Rows,
	int AcceptedLiveRowCount,
	bool HasActionTwoAcceptedRow,
	bool HasActionSixAcceptedRow,
	bool HasBoundaryAcceptance,
	bool HasExecutorObservation,
	bool HasRegistryObservation,
	bool HasPostedBeforeRefreshedOrdering,
	bool HasZeroWorldBroadcasts,
	bool HasZeroInviteDispatches,
	bool HasJavaArtifactPairingIdentity,
	bool CanFeedRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live intake preflight for future accepted C#
/// CM_FIND_GROUP action 2/6 boundary rows. It names the row requirements that
/// must pass before explicit-root Java artifacts can feed runtime comparison;
/// it does not execute the boundary or compare Java/C# rows.
/// </summary>
public static class FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService
{
	public static FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflight Create(
		FindGroupMutationPostGuardedFixtureResultContract? guardedFixtureResult = null)
	{
		guardedFixtureResult ??= FindGroupMutationPostGuardedFixtureResultContractService.Create();
		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var acceptedRows = guardedFixtureResult.AcceptedLiveRows;
		var hasActionTwo = acceptedRows.Any(row => row.Action == 2);
		var hasActionSix = acceptedRows.Any(row => row.Action == 6);
		var hasBothRows = hasActionTwo && hasActionSix;
		var hasBoundaryAcceptance = hasBothRows && acceptedRows.All(row => row.BoundaryAccepted);
		var hasExecutorObservation = hasBothRows && acceptedRows.All(row => row.ExecutorInvokedFromBoundary);
		var hasRegistryObservation = hasBothRows && acceptedRows.All(row => row.RegistrySendsObservedInOrder);
		var hasPostedBeforeRefreshedOrdering = hasBothRows && acceptedRows.All(row => row.Status == FindGroupMutationPostGuardedFixtureCandidateRowStatus.AcceptedLiveBoundaryRow);
		var hasZeroWorldBroadcasts = hasBothRows && acceptedRows.All(row => BroadcastCount(row) == 0);
		var hasZeroInviteDispatches = hasBothRows && acceptedRows.All(row => InviteDispatchCount(row) == 0);
		var hasJavaArtifactPairingIdentity = hasBothRows && acceptedRows.All(row => schema.SupportedActions.Any(action =>
			action.Action == row.Action
			&& action.MutationKind == row.MutationKind));

		var rows = new List<FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightRow>();
		Add(rows,
			FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.ActionTwoAcceptedBoundaryRow,
			action: 2,
			hasActionTwo,
			"One accepted C# live boundary row for Java CM_FIND_GROUP action 2.",
			$"acceptedAction2={hasActionTwo}; acceptedRows={acceptedRows.Count}",
			"CM_FIND_GROUP.runImpl action 2 -> FindGroupService.addRecruitment(player, message, groupType)",
			"Action 2 row must be accepted by the guarded fixture result contract.");
		Add(rows,
			FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.ActionSixAcceptedBoundaryRow,
			action: 6,
			hasActionSix,
			"One accepted C# live boundary row for Java CM_FIND_GROUP action 6.",
			$"acceptedAction6={hasActionSix}; acceptedRows={acceptedRows.Count}",
			"CM_FIND_GROUP.runImpl action 6 -> FindGroupService.addApplication(player, message, groupType, classId, level)",
			"Action 6 row must be accepted by the guarded fixture result contract.");
		Add(rows,
			FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.BoundaryAccepted,
			action: null,
			hasBoundaryAcceptance,
			"Every accepted row must have boundaryAccepted=true from the guarded GameServerConnection boundary.",
			$"boundaryRows={acceptedRows.Count(row => row.BoundaryAccepted)}; acceptedRows={acceptedRows.Count}",
			"CM_FIND_GROUP.runImpl is invoked only after AionClientPacket boundary acceptance.",
			"Disabled plan rows are shape inputs only and cannot satisfy this gate.");
		Add(rows,
			FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.ExecutorInvokedFromBoundary,
			action: null,
			hasExecutorObservation,
			"Every accepted row must have executorInvokedFromBoundary=true.",
			$"executorRows={acceptedRows.Count(row => row.ExecutorInvokedFromBoundary)}; acceptedRows={acceptedRows.Count}",
			"FindGroupService.addRecruitment/addApplication sends packets from the CM_FIND_GROUP boundary call.",
			"Opt-in executor calls outside the guarded boundary remain insufficient.");
		Add(rows,
			FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.RegistrySendsObservedInOrder,
			action: null,
			hasRegistryObservation,
			"Every accepted row must have registrySendsObservedInOrder=true.",
			$"registryRows={acceptedRows.Count(row => row.RegistrySendsObservedInOrder)}; acceptedRows={acceptedRows.Count}",
			"Java PacketSendUtility.sendPacket observes posted system message before refreshed SM_FIND_GROUP.",
			"Registry observation must come from live send observation, not intent ordering alone.");
		Add(rows,
			FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.PostedSystemMessageBeforeRefreshedList,
			action: null,
			hasPostedBeforeRefreshedOrdering,
			"Action 2 requires SmSystemMessage 1400392 before SmFindGroup action 0; action 6 requires SmSystemMessage 1400393 before SmFindGroup action 4.",
			$"acceptedRows={acceptedRows.Count}; contractStatus={guardedFixtureResult.Status}",
			"FindGroupService.addRecruitment/addApplication call PacketSendUtility.sendPacket before showRecruitments/showApplications.",
			"The guarded fixture result contract accepts only rows with Java-shaped posted/refreshed packet fields.");
		Add(rows,
			FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.ZeroWorldBroadcasts,
			action: null,
			hasZeroWorldBroadcasts,
			"Every accepted row must have worldBroadcastCount=0.",
			$"acceptedRows={acceptedRows.Count}; nonZeroBroadcastRows={acceptedRows.Count(row => BroadcastCount(row) != 0)}",
			"FindGroupService.addRecruitment/addApplication emit direct packets, not world broadcasts.",
			"World broadcast evidence belongs to other CM_FIND_GROUP actions, not mutation-post action 2/6.");
		Add(rows,
			FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.ZeroInviteDispatches,
			action: null,
			hasZeroInviteDispatches,
			"Every accepted row must have inviteDispatchCount=0.",
			$"acceptedRows={acceptedRows.Count}; nonZeroInviteRows={acceptedRows.Count(row => InviteDispatchCount(row) != 0)}",
			"FindGroupService.addRecruitment/addApplication do not dispatch group/alliance invites.",
			"Invite evidence belongs to action 12, not mutation-post action 2/6.");
		Add(rows,
			FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.JavaArtifactPairingIdentity,
			action: null,
			hasJavaArtifactPairingIdentity,
			"Accepted C# rows must pair to Java artifacts by action and mutation kind.",
			$"action2={hasActionTwo}; action6={hasActionSix}; pairingRows={acceptedRows.Count(row => schema.SupportedActions.Any(action => action.Action == row.Action && action.MutationKind == row.MutationKind))}",
			schema.JavaSource,
			"Value comparison can only start after Java artifacts and accepted C# rows share action/mutation identity.");

		var canFeedRuntimeComparison = rows.All(row => row.Satisfied);
		var status = canFeedRuntimeComparison
			? FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus.ReadyForJavaArtifactPairingRuntimeComparisonBlocked
			: FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus.BlockedMissingAcceptedBoundaryRows;

		return new FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflight(
			status,
			rows,
			acceptedRows.Count,
			hasActionTwo,
			hasActionSix,
			hasBoundaryAcceptance,
			hasExecutorObservation,
			hasRegistryObservation,
			hasPostedBeforeRefreshedOrdering,
			hasZeroWorldBroadcasts,
			hasZeroInviteDispatches,
			hasJavaArtifactPairingIdentity,
			canFeedRuntimeComparison,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			guardedFixtureResult.TraceName,
			schema.JavaSource,
			IsLive: false);
	}

	private static int BroadcastCount(FindGroupMutationPostGuardedFixtureCandidateRow row)
	{
		var match = row.Evidence.Split(';').Select(part => part.Trim()).FirstOrDefault(part => part.StartsWith("broadcasts=", StringComparison.Ordinal));
		return match != null && int.TryParse(match["broadcasts=".Length..], out var value)
			? value
			: row.Status == FindGroupMutationPostGuardedFixtureCandidateRowStatus.AcceptedLiveBoundaryRow ? 0 : -1;
	}

	private static int InviteDispatchCount(FindGroupMutationPostGuardedFixtureCandidateRow row)
	{
		var match = row.Evidence.Split(';').Select(part => part.Trim()).FirstOrDefault(part => part.StartsWith("invites=", StringComparison.Ordinal));
		return match != null && int.TryParse(match["invites=".Length..], out var value)
			? value
			: row.Status == FindGroupMutationPostGuardedFixtureCandidateRowStatus.AcceptedLiveBoundaryRow ? 0 : -1;
	}

	private static void Add(
		ICollection<FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightRow> rows,
		FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate gate,
		int? action,
		bool satisfied,
		string requiredEvidence,
		string currentEvidence,
		string javaSource,
		string notes)
	{
		rows.Add(new FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightRow(
			rows.Count + 1,
			gate,
			action,
			satisfied,
			BlocksRuntimeComparison: !satisfied,
			requiredEvidence,
			currentEvidence,
			javaSource,
			notes));
	}

	private static string DecisionFor(
		FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus status)
	{
		return status switch
		{
			FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus.ReadyForJavaArtifactPairingRuntimeComparisonBlocked => "Accepted C# action 2/6 boundary rows can feed Java artifact pairing, but value projection, result materialization, runtime comparison, and verified parity remain blocked.",
			_ => "C# live-boundary row intake is blocked until accepted action 2 and action 6 rows prove boundary acceptance, executor invocation, registry send ordering, Java packet shape, zero broadcasts, zero invites, and Java artifact pairing identity.",
		};
	}
}
