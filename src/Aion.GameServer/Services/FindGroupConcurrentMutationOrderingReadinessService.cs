namespace Aion.GameServer.Services;

public static class FindGroupConcurrentMutationOrderingReadinessService
{
	public static FindGroupConcurrentMutationOrderingReadinessReport CreateReport()
	{
		return new FindGroupConcurrentMutationOrderingReadinessReport(
			FindGroupConcurrentMutationOrderingReadinessStatus.BlockedPendingLiveSingletonConcurrencyEvidence,
			"game-server/src/com/aionemu/gameserver/services/findgroup/FindGroupService.java",
			"dotnetConversion/src/Aion.GameServer/Services/FindGroupRecruitmentPlanService.cs",
			[
				new FindGroupConcurrentMutationOrderingEvidence(
					FindGroupConcurrentMutationOrderingEvidenceKind.JavaConcurrentMapShape,
					"Java FindGroupService stores recruitments, applications, and instanceGroups in independent ConcurrentHashMap instances.",
					FindGroupConcurrentMutationOrderingEvidenceStatus.Reviewed),
				new FindGroupConcurrentMutationOrderingEvidence(
					FindGroupConcurrentMutationOrderingEvidenceKind.JavaOnJoinedTeamMethodOrder,
					"Java onJoinedTeam reads/removes instanceGroups, removes applications, removes solo recruitment with unknown3=16, then either re-adds leader recruitment or removes the full team recruitment.",
					FindGroupConcurrentMutationOrderingEvidenceStatus.Reviewed),
				new FindGroupConcurrentMutationOrderingEvidence(
					FindGroupConcurrentMutationOrderingEvidenceKind.CSharpConcurrentDictionaryShape,
					"C# FindGroupRecruitmentPlanService uses independent ConcurrentDictionary stores for recruitments, applications, and instance groups.",
					FindGroupConcurrentMutationOrderingEvidenceStatus.EvidenceAvailable),
				new FindGroupConcurrentMutationOrderingEvidence(
					FindGroupConcurrentMutationOrderingEvidenceKind.CSharpSequentialOnJoinedTeamTests,
					"C# focused tests cover method-order outcomes for instance-group removal, application removal, solo recruitment removal, leader re-add priority, full-team removal, and post-join runtime member reads.",
					FindGroupConcurrentMutationOrderingEvidenceStatus.EvidenceAvailable),
				new FindGroupConcurrentMutationOrderingEvidence(
					FindGroupConcurrentMutationOrderingEvidenceKind.CSharpBasicConcurrentStoreTests,
					"C# focused tests exercise concurrent add/logout operations across the three stores, matching the broad Java ConcurrentHashMap storage shape.",
					FindGroupConcurrentMutationOrderingEvidenceStatus.EvidenceAvailable),
				new FindGroupConcurrentMutationOrderingEvidence(
					FindGroupConcurrentMutationOrderingEvidenceKind.LiveSingletonCallerInterleaving,
					"No live evidence proves CM_FIND_GROUP, logout, joined-team, and disband callers interleave against the same singleton state with Java-equivalent multi-step outcomes.",
					FindGroupConcurrentMutationOrderingEvidenceStatus.Blocked),
				new FindGroupConcurrentMutationOrderingEvidence(
					FindGroupConcurrentMutationOrderingEvidenceKind.RuntimeComparison,
					"No runtime or socket-level comparison has exercised competing live FindGroup mutations under concurrent player actions.",
					FindGroupConcurrentMutationOrderingEvidenceStatus.Blocked),
			],
			[
				"Do not claim Java-equivalent live concurrency from ConcurrentDictionary storage shape alone.",
				"Before live CmFindGroup dispatch, add focused tests or runtime traces for interleavings between CM_FIND_GROUP actions, logout cleanup, joined-team cleanup, and disband cleanup on the same shared service.",
				"Preserve Java method-order evidence for onJoinedTeam without introducing extra synchronization unless a parity review justifies it.",
			]);
	}
}

public enum FindGroupConcurrentMutationOrderingReadinessStatus
{
	BlockedPendingLiveSingletonConcurrencyEvidence,
	Ready,
}

public enum FindGroupConcurrentMutationOrderingEvidenceKind
{
	JavaConcurrentMapShape,
	JavaOnJoinedTeamMethodOrder,
	CSharpConcurrentDictionaryShape,
	CSharpSequentialOnJoinedTeamTests,
	CSharpBasicConcurrentStoreTests,
	LiveSingletonCallerInterleaving,
	RuntimeComparison,
}

public enum FindGroupConcurrentMutationOrderingEvidenceStatus
{
	Reviewed,
	EvidenceAvailable,
	Blocked,
	Ready,
}

public sealed record FindGroupConcurrentMutationOrderingReadinessReport(
	FindGroupConcurrentMutationOrderingReadinessStatus Status,
	string JavaFindGroupSource,
	string CSharpFindGroupSource,
	IReadOnlyList<FindGroupConcurrentMutationOrderingEvidence> Evidence,
	IReadOnlyList<string> NextRequiredEvidence)
{
	public bool IsReadyForLiveSingletonConcurrency =>
		Status == FindGroupConcurrentMutationOrderingReadinessStatus.Ready
		&& Evidence.All(evidence => evidence.Status == FindGroupConcurrentMutationOrderingEvidenceStatus.Ready);
}

public sealed record FindGroupConcurrentMutationOrderingEvidence(
	FindGroupConcurrentMutationOrderingEvidenceKind Kind,
	string Detail,
	FindGroupConcurrentMutationOrderingEvidenceStatus Status);
