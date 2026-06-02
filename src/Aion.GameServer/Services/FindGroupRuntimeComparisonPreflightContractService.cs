namespace Aion.GameServer.Services;

public static class FindGroupRuntimeComparisonPreflightContractService
{
	public static FindGroupRuntimeComparisonPreflightContract Create()
	{
		var fields = new[]
		{
			Field(
				FindGroupRuntimeComparisonTraceFieldKind.ClientAction,
				"action",
				"Parsed CM_FIND_GROUP action value from the client payload."),
			Field(
				FindGroupRuntimeComparisonTraceFieldKind.ActivePlayer,
				"activePlayerObjectId/race/team",
				"Active player object id, race, current team id, and current team size at boundary entry."),
			Field(
				FindGroupRuntimeComparisonTraceFieldKind.ParsedPayload,
				"playerOrTeamId/message/groupType/classId/level/serverId/unknowns/instanceMaskId/minMembers/reply",
				"Action-specific parsed payload fields used by Java readImpl and runImpl."),
			Field(
				FindGroupRuntimeComparisonTraceFieldKind.SingletonStateBeforeAfter,
				"recruitments/applications/instanceGroups before and after",
				"FindGroup singleton map keys and relevant entry facts before and after the action."),
			Field(
				FindGroupRuntimeComparisonTraceFieldKind.DirectPackets,
				"direct packet recipient/order/payload",
				"PacketSendUtility.sendPacket-equivalent recipient, packet type/action, and canonical payload order."),
			Field(
				FindGroupRuntimeComparisonTraceFieldKind.WorldBroadcasts,
				"broadcast race/recipients/exclusions/order/payload",
				"PacketSendUtility.broadcastToWorld-equivalent race filter, included recipients, excluded opposite-race players, and payload order."),
			Field(
				FindGroupRuntimeComparisonTraceFieldKind.InviteRequests,
				"group/alliance invite request mutation/question-window order",
				"Action 12 accepted invite request mutation, missing-player status, and question-window packet ordering."),
			Field(
				FindGroupRuntimeComparisonTraceFieldKind.NoSideEffectBranches,
				"missing/no-run/no-send outcomes",
				"Missing recruitment/application/applicant/instance group, parsed-only actions 20/25, and update-missing no-side-effect outcomes."),
			Field(
				FindGroupRuntimeComparisonTraceFieldKind.EncryptedSocketFrames,
				"encrypted client/server frame order",
				"Real or deterministic encrypted socket frame order for client-observable packets after live dispatch exists."),
		};
		var scenarios = new[]
		{
			Scenario("show-list-direct", [0, 4, 10, 13, 15], "Direct packet show-list/member-info actions and action 10 optional action 26 ordering."),
			Scenario("mutation-direct", [2, 6, 8, 9, 17], "Direct packet mutation actions, including posted-message-before-refresh and missing update/removal outcomes."),
			Scenario("world-broadcast", [1, 5], "Race-filtered world broadcasts for removed recruitment/application and missing-branch no-send outcomes."),
			Scenario("instance-application", [11], "Resolved recruiter direct packet and missing-recipient no-send outcome."),
			Scenario("action-12-invite", [12], "Accepted group/alliance invite, declined whisper, missing applicant, and missing responder instance group outcomes."),
			Scenario("parsed-only-no-run", [20, 25], "Parsed Java readImpl actions with no runImpl branch and no side effects."),
			Scenario("shared-singleton-lifecycle", [], "Interleavings with logout cleanup, joined-team cleanup, and group/alliance disband cleanup sharing the same FindGroup singleton."),
		};
		var fixtureRows = new[]
		{
			new FindGroupRuntimeComparisonFixtureContractRow(
				"mutation-post-actions-2-6",
				Actions: [2, 6],
				TraceName: "cm-find-group-direct-mutation-post-boundary",
				JavaSource: "CM_FIND_GROUP.runImpl actions 2/6; FindGroupService.addRecruitment/addApplication",
				CSharpProjectionSource: "FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateExportFromDisabledPlan",
				"Compare action 2 and 6 mutation-post traces using schema version 1 fields: mutated entry id, posted system message id, refreshed show-list action, visible entry ids after mutation, and false executor/registry observations until live capture exists.",
				FindGroupRuntimeComparisonFixtureContractStatus.BlockedPendingJavaAndLiveCSharpTrace),
		};

		return new FindGroupRuntimeComparisonPreflightContract(
			FindGroupRuntimeComparisonPreflightStatus.BlockedPendingLiveDispatchAndTraceHarness,
			fields,
			scenarios,
			fixtureRows,
			ShouldInvokeLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			RequiresJavaRuntimeTrace: true,
			RequiresCSharpRuntimeTrace: true,
			RequiresEncryptedSocketCapture: true,
			"Preflight only; do not treat this as runtime parity evidence until Java and C# traces are captured and compared.",
			"Java sources reviewed: CM_FIND_GROUP.readImpl/runImpl and FindGroupService sendPacket/broadcast/invite/singleton call sites.",
			"C# sources expected in future comparison: CmFindGroup, GameServerConnection.ProcessPacketAsync, FindGroupRecruitmentPlanService, FindGroupSideEffectDispatchExecutorService, and FindGroupInstanceApplicationInviteDispatchPlanService.");
	}

	private static FindGroupRuntimeComparisonTraceField Field(
		FindGroupRuntimeComparisonTraceFieldKind kind,
		string name,
		string requirement)
	{
		return new FindGroupRuntimeComparisonTraceField(kind, name, requirement);
	}

	private static FindGroupRuntimeComparisonScenario Scenario(
		string name,
		IReadOnlyList<int> actions,
		string requirement)
	{
		return new FindGroupRuntimeComparisonScenario(name, actions, requirement);
	}
}

public enum FindGroupRuntimeComparisonPreflightStatus
{
	BlockedPendingLiveDispatchAndTraceHarness,
	ReadyForTraceCapture,
}

public enum FindGroupRuntimeComparisonTraceFieldKind
{
	ClientAction,
	ActivePlayer,
	ParsedPayload,
	SingletonStateBeforeAfter,
	DirectPackets,
	WorldBroadcasts,
	InviteRequests,
	NoSideEffectBranches,
	EncryptedSocketFrames,
}

public enum FindGroupRuntimeComparisonFixtureContractStatus
{
	BlockedPendingJavaAndLiveCSharpTrace,
	ReadyForRuntimeComparison,
}

public sealed record FindGroupRuntimeComparisonPreflightContract(
	FindGroupRuntimeComparisonPreflightStatus Status,
	IReadOnlyList<FindGroupRuntimeComparisonTraceField> RequiredTraceFields,
	IReadOnlyList<FindGroupRuntimeComparisonScenario> RequiredScenarios,
	IReadOnlyList<FindGroupRuntimeComparisonFixtureContractRow> RequiredFixtureRows,
	bool ShouldInvokeLiveSideEffects,
	bool IsCmFindGroupBoundaryWired,
	bool RequiresJavaRuntimeTrace,
	bool RequiresCSharpRuntimeTrace,
	bool RequiresEncryptedSocketCapture,
	string BoundaryNote,
	string JavaSource,
	string CSharpSource)
{
	public bool IsReadyForRuntimeComparison =>
		Status == FindGroupRuntimeComparisonPreflightStatus.ReadyForTraceCapture
		&& IsCmFindGroupBoundaryWired
		&& ShouldInvokeLiveSideEffects
		&& RequiresJavaRuntimeTrace
		&& RequiresCSharpRuntimeTrace
		&& RequiresEncryptedSocketCapture
		&& RequiredTraceFields.Count > 0
		&& RequiredScenarios.Count > 0
		&& RequiredFixtureRows.Count > 0
		&& RequiredFixtureRows.All(row => row.Status == FindGroupRuntimeComparisonFixtureContractStatus.ReadyForRuntimeComparison);
}

public sealed record FindGroupRuntimeComparisonTraceField(
	FindGroupRuntimeComparisonTraceFieldKind Kind,
	string Name,
	string Requirement);

public sealed record FindGroupRuntimeComparisonScenario(
	string Name,
	IReadOnlyList<int> Actions,
	string Requirement);

public sealed record FindGroupRuntimeComparisonFixtureContractRow(
	string Name,
	IReadOnlyList<int> Actions,
	string TraceName,
	string JavaSource,
	string CSharpProjectionSource,
	string Requirement,
	FindGroupRuntimeComparisonFixtureContractStatus Status);
