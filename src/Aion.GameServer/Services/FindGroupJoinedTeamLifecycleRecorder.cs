using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class FindGroupJoinedTeamLifecycleRecorder(
	FindGroupRecruitmentPlanService findGroupService,
	Func<int> nowEpochSeconds,
	byte serverId = 0,
	Action<FindGroupJoinedTeamPlan>? planObserver = null)
{
	public FindGroupJoinedTeamPlan RecordGroupJoin(
		Player player,
		PlayerGroupRuntime groupRuntime,
		int teamId)
	{
		// Java parity: PlayerGroupEnteredEvent.handleEvent calls PlayerGroupService.addPlayerToGroup,
		// which records FindGroupService.onJoinedTeam before entered-packet fanout. This recorder
		// intentionally keeps live find-group dispatch disabled.
		var members = ResolveGroupMembers(groupRuntime, teamId);
		var subject = CreateSubject(teamId, members, groupRuntime.GetDescriptor(teamId)?.LeaderObjectId);
		var plan = findGroupService.OnJoinedTeam(
			player,
			subject,
			groupRuntime.IsLeader(teamId, player),
			groupRuntime.IsFull(teamId),
			nowEpochSeconds(),
			serverId);
		planObserver?.Invoke(plan);
		return plan;
	}

	public FindGroupJoinedTeamPlan RecordAllianceJoin(
		Player player,
		PlayerAllianceRuntime allianceRuntime,
		int allianceId)
	{
		// Java parity: PlayerAllianceEnteredEvent.handleEvent calls PlayerAllianceService.addPlayerToAlliance,
		// which records FindGroupService.onJoinedTeam before entered-packet fanout. This recorder
		// intentionally keeps live find-group dispatch disabled.
		var members = ResolveAllianceMembers(allianceRuntime, allianceId);
		var subject = CreateSubject(allianceId, members, allianceRuntime.GetDescriptor(allianceId)?.LeaderObjectId);
		var plan = findGroupService.OnJoinedTeam(
			player,
			subject,
			allianceRuntime.IsLeader(allianceId, player),
			allianceRuntime.IsFull(allianceId),
			nowEpochSeconds(),
			serverId);
		planObserver?.Invoke(plan);
		return plan;
	}

	private static IReadOnlyList<Player> ResolveGroupMembers(PlayerGroupRuntime groupRuntime, int teamId)
	{
		return groupRuntime.GetMemberObjectIds(teamId)
			.Select(objectId => groupRuntime.GetMember(teamId, objectId)?.Player)
			.Where(player => player != null)
			.Cast<Player>()
			.ToArray();
	}

	private static IReadOnlyList<Player> ResolveAllianceMembers(PlayerAllianceRuntime allianceRuntime, int allianceId)
	{
		return allianceRuntime.GetMemberObjectIds(allianceId)
			.Select(objectId => allianceRuntime.GetMember(allianceId, objectId)?.Player)
			.Where(player => player != null)
			.Cast<Player>()
			.ToArray();
	}

	private static FindGroupRecruitmentSubject CreateSubject(
		int teamId,
		IReadOnlyList<Player> members,
		int? leaderObjectId)
	{
		if (members.Count == 0)
			throw new InvalidOperationException("Find-group joined-team recording requires the current team members.");

		var leader = leaderObjectId.HasValue
			? members.FirstOrDefault(member => member.ObjectId == leaderObjectId.Value)
			: null;
		leader ??= members[0];
		return new FindGroupRecruitmentSubject(
			teamId,
			leader.Race,
			IsSoloPlayer: false,
			leader.Name,
			members.Count,
			members.Min(member => member.Level),
			members.Max(member => member.Level),
			FindGroupRecruitmentSubject.ToJavaClassId(leader.PlayerClass));
	}
}
