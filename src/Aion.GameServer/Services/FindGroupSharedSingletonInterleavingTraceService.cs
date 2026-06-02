namespace Aion.GameServer.Services;

public static class FindGroupSharedSingletonInterleavingTraceService
{
	public static FindGroupSharedSingletonInterleavingTrace CreateLogoutBeforeJoinedTeamTrace(
		FindGroupLogoutCleanupPlan logoutPlan,
		FindGroupJoinedTeamPlan joinedTeamPlan)
	{
		// Java parity: FindGroupService.onLogout removes player-keyed state before
		// FindGroupService.onJoinedTeam observes any remaining state for the same player.
		return new FindGroupSharedSingletonInterleavingTrace(
			FindGroupSharedSingletonInterleavingTraceKind.LogoutBeforeJoinedTeam,
			[
				new FindGroupSharedSingletonInterleavingTraceStep(
					1,
					FindGroupSharedSingletonCaller.LogoutCleanup,
					logoutPlan.PlayerObjectId,
					DescribeLogout(logoutPlan),
					"FindGroupService.onLogout(player)"),
				new FindGroupSharedSingletonInterleavingTraceStep(
					2,
					FindGroupSharedSingletonCaller.JoinedTeamCleanup,
					logoutPlan.PlayerObjectId,
					DescribeJoinedTeam(joinedTeamPlan),
					"FindGroupService.onJoinedTeam(player)"),
			],
			IsLiveRuntimeTrace: false,
			"Deterministic shared-singleton projection only; CM_FIND_GROUP live dispatch remains disabled.");
	}

	public static FindGroupSharedSingletonInterleavingTrace CreateJoinedTeamBeforeLogoutBeforeDisbandTrace(
		int playerObjectId,
		int teamObjectId,
		FindGroupJoinedTeamPlan joinedTeamPlan,
		FindGroupLogoutCleanupPlan logoutPlan,
		FindGroupRecruitmentMutationPlan disbandRemoval)
	{
		// Java parity: onJoinedTeam can re-key a leader recruitment to the team id; logout
		// removes player-keyed state only, leaving team-keyed recruitment for disband cleanup.
		return new FindGroupSharedSingletonInterleavingTrace(
			FindGroupSharedSingletonInterleavingTraceKind.JoinedTeamBeforeLogoutBeforeDisband,
			[
				new FindGroupSharedSingletonInterleavingTraceStep(
					1,
					FindGroupSharedSingletonCaller.JoinedTeamCleanup,
					playerObjectId,
					DescribeJoinedTeam(joinedTeamPlan),
					"FindGroupService.onJoinedTeam(player)"),
				new FindGroupSharedSingletonInterleavingTraceStep(
					2,
					FindGroupSharedSingletonCaller.LogoutCleanup,
					playerObjectId,
					DescribeLogout(logoutPlan),
					"FindGroupService.onLogout(player)"),
				new FindGroupSharedSingletonInterleavingTraceStep(
					3,
					FindGroupSharedSingletonCaller.DisbandCleanup,
					teamObjectId,
					$"recruitment={disbandRemoval.Status}",
					"FindGroupService.removeRecruitment(team)"),
			],
			IsLiveRuntimeTrace: false,
			"Deterministic shared-singleton projection only; CM_FIND_GROUP live dispatch remains disabled.");
	}

	private static string DescribeLogout(FindGroupLogoutCleanupPlan plan)
	{
		return string.Join(
			";",
			$"recruitment={(plan.RemovedRecruitment == null ? "Missing" : "Removed")}",
			$"application={(plan.RemovedApplication == null ? "Missing" : "Removed")}",
			$"instanceGroup={(plan.RemovedInstanceGroup == null ? "Missing" : "Removed")}");
	}

	private static string DescribeJoinedTeam(FindGroupJoinedTeamPlan plan)
	{
		var teamAddStatus = plan.TeamRecruitmentAdd?.Status.ToString() ?? "Skipped";
		var fullTeamRemovalStatus = plan.FullTeamRecruitmentRemoval?.Status.ToString() ?? "Skipped";
		return string.Join(
			";",
			$"instanceGroup={(plan.InstanceGroupRemoval.ShouldRemove ? "Removed" : "Retained")}",
			$"application={plan.ApplicationRemoval.Status}",
			$"soloRecruitment={plan.SoloRecruitmentRemoval.Status}",
			$"teamRecruitmentAdd={teamAddStatus}",
			$"fullTeamRecruitmentRemoval={fullTeamRemovalStatus}");
	}
}

public enum FindGroupSharedSingletonInterleavingTraceKind
{
	LogoutBeforeJoinedTeam,
	JoinedTeamBeforeLogoutBeforeDisband,
}

public enum FindGroupSharedSingletonCaller
{
	LogoutCleanup,
	JoinedTeamCleanup,
	DisbandCleanup,
}

public sealed record FindGroupSharedSingletonInterleavingTrace(
	FindGroupSharedSingletonInterleavingTraceKind Kind,
	IReadOnlyList<FindGroupSharedSingletonInterleavingTraceStep> Steps,
	bool IsLiveRuntimeTrace,
	string BoundaryNote);

public sealed record FindGroupSharedSingletonInterleavingTraceStep(
	int Sequence,
	FindGroupSharedSingletonCaller Caller,
	int SubjectObjectId,
	string Outcome,
	string JavaSource);
