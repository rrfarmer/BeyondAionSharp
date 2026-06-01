using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerAllianceInviteRequestServiceTests
{
	[Fact]
	public void HandleResponse_AcceptCreatesAllianceRecordsFindGroupJoinedTeamForRequesterAndInvited()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var observed = new List<FindGroupJoinedTeamPlan>();
		var service = new PlayerAllianceInviteRequestService(
			new FindGroupJoinedTeamLifecycleRecorder(findGroupService, () => 333, serverId: 5, observed.Add));
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var requester = CreatePlayer(1001, "Requester");
		var invited = CreatePlayer(1002, "Invited");
		findGroupService.AddRecruitment(requester, "Alliance leader", groupType: 6, nowEpochSeconds: 220);
		findGroupService.AddApplication(invited, "Alliance invite me", groupType: 2, classId: 5, level: 45, nowEpochSeconds: 221);
		service.SendInvite(requester, invited, groups, alliances, Resolve(requester, invited));

		var response = service.HandleResponse(
			invited,
			SmQuestionWindow.AllianceInvite,
			response: 1,
			groups,
			alliances,
			() => 88001,
			Resolve(requester, invited));

		Assert.Equal(AllianceInviteResponseStatus.Accepted, response.Status);
		Assert.Equal(2, response.FindGroupJoinedTeamPlans.Count);
		Assert.Same(response.FindGroupJoinedTeamPlans[0], observed[0]);
		Assert.All(response.FindGroupJoinedTeamPlans, plan => Assert.False(plan.DispatchLiveSideEffects));
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, response.FindGroupJoinedTeamPlans[0].SoloRecruitmentRemoval.Status);
		Assert.NotNull(response.FindGroupJoinedTeamPlans[0].TeamRecruitmentAdd);
		Assert.Equal(88001, response.FindGroupJoinedTeamPlans[0].TeamRecruitmentAdd!.CurrentRecruitment?.ObjectId);
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, response.FindGroupJoinedTeamPlans[1].ApplicationRemoval.Status);
		Assert.Equal([1001, 1002], response.AllianceSnapshot?.MemberObjectIds);
	}

	[Fact]
	public void HandleResponse_AcceptMergesGroupsRecordsEveryAddedAllianceMemberAfterGroupRemoval()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var service = new PlayerAllianceInviteRequestService(
			new FindGroupJoinedTeamLifecycleRecorder(findGroupService, () => 333, serverId: 5));
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var requester = CreatePlayer(1001, "Requester");
		var requesterMember = CreatePlayer(1002, "RequesterMember");
		var invitedLeader = CreatePlayer(1003, "InvitedLeader");
		var selected = CreatePlayer(1004, "Selected");
		groups.CreateOrUpdateGroup(77001, [requester, requesterMember]);
		groups.CreateOrUpdateGroup(77002, [invitedLeader, selected]);
		findGroupService.AddApplication(requesterMember, "Requester member app", groupType: 2, classId: 5, level: 45, nowEpochSeconds: 221);
		findGroupService.AddApplication(selected, "Selected app", groupType: 2, classId: 5, level: 45, nowEpochSeconds: 222);
		service.SendInvite(requester, selected, groups, alliances, Resolve(requester, requesterMember, invitedLeader, selected));

		var response = service.HandleResponse(
			invitedLeader,
			SmQuestionWindow.AllianceInvite,
			response: 1,
			groups,
			alliances,
			() => 88001,
			Resolve(requester, requesterMember, invitedLeader, selected));

		Assert.Equal(AllianceInviteResponseStatus.Accepted, response.Status);
		Assert.Equal(4, response.FindGroupJoinedTeamPlans.Count);
		Assert.Equal([1001, 1002, 1003, 1004], response.AllianceSnapshot?.MemberObjectIds);
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, response.FindGroupJoinedTeamPlans[1].ApplicationRemoval.Status);
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, response.FindGroupJoinedTeamPlans[3].ApplicationRemoval.Status);
		Assert.All(response.FindGroupJoinedTeamPlans, plan => Assert.False(plan.DispatchLiveSideEffects));
	}

	private static Func<int, Player?> Resolve(params Player[] players)
	{
		return objectId => players.FirstOrDefault(player => player.ObjectId == objectId);
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = true,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 45,
			Position = new WorldPosition(210010000, objectId, 20, 30, 0),
		};
	}
}
