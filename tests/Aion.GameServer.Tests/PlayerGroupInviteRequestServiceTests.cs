using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerGroupInviteRequestServiceTests
{
	[Fact]
	public void SendInvite_RegistersPartyQuestionAndReturnsInviterMessage()
	{
		var service = new PlayerGroupInviteRequestService();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");

		var result = service.SendInvite(inviter, invited);

		Assert.Equal(GroupInviteRequestStatus.Requested, result.Status);
		Assert.Equal(1001, result.Request.InviterObjectId);
		Assert.Equal("Inviter", result.Request.InviterName);
		Assert.Equal(1300173, result.InviterMessage.MessageId);
		Assert.Equal(SmQuestionWindow.PartyInvite, result.QuestionWindow?.Code);
		Assert.Equal(1, invited.ResponseRequester.Count);
	}

	[Fact]
	public void SendInvite_DuplicateLeavesOriginalRequestLikeJavaPutIfAbsent()
	{
		var service = new PlayerGroupInviteRequestService();
		var firstInviter = CreatePlayer(1001, "First");
		var secondInviter = CreatePlayer(1003, "Second");
		var invited = CreatePlayer(1002, "Invited");

		var first = service.SendInvite(firstInviter, invited);
		var duplicate = service.SendInvite(secondInviter, invited);

		Assert.Equal(GroupInviteRequestStatus.Requested, first.Status);
		Assert.Equal(GroupInviteRequestStatus.DuplicateRequest, duplicate.Status);
		Assert.Null(duplicate.QuestionWindow);
		Assert.Equal(1, invited.ResponseRequester.Count);
		var denied = service.HandleResponse(
			firstInviter,
			invited,
			SmQuestionWindow.PartyInvite,
			response: 0,
			new PlayerGroupRuntime(),
			newGroupId: 7001);
		Assert.Equal(1001, denied.Request?.InviterObjectId);
	}

	[Fact]
	public void HandleResponse_DenyConsumesRequestAndReturnsRejectMessage()
	{
		var service = new PlayerGroupInviteRequestService();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		service.SendInvite(inviter, invited);

		var response = service.HandleResponse(
			inviter,
			invited,
			SmQuestionWindow.PartyInvite,
			response: 0,
			new PlayerGroupRuntime(),
			newGroupId: 7001);

		Assert.Equal(GroupInviteResponseStatus.Denied, response.Status);
		Assert.Equal(1300161, response.DenyMessage?.MessageId);
		Assert.Equal(0, invited.ResponseRequester.Count);
		Assert.Null(inviter.CurrentGroupSnapshot);
		Assert.Null(invited.CurrentGroupSnapshot);
	}

	[Fact]
	public void HandleResponse_AcceptCreatesGroupWhenInviterHasNoGroup()
	{
		var service = new PlayerGroupInviteRequestService();
		var runtime = new PlayerGroupRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		service.SendInvite(inviter, invited);

		var response = service.HandleResponse(
			inviter,
			invited,
			SmQuestionWindow.PartyInvite,
			response: 1,
			runtime,
			newGroupId: 7001);

		Assert.Equal(GroupInviteResponseStatus.Accepted, response.Status);
		Assert.Equal(7001, response.GroupSnapshot?.TeamId);
		Assert.Equal(7001, inviter.CurrentGroupSnapshot?.TeamId);
		Assert.Equal(7001, invited.CurrentGroupSnapshot?.TeamId);
		Assert.Equal([1001, 1002], response.GroupSnapshot?.MemberObjectIds);
		Assert.NotNull(response.EnteredPacketPlan);
		Assert.True(response.EnteredPacketPlan.SendGroupInfoToEnteringPlayer);
		Assert.Equal(0, invited.ResponseRequester.Count);
	}

	[Fact]
	public void HandleResponse_AcceptCreatesGroupRecordsFindGroupJoinedTeamForBothEntrantsLikeJavaEvent()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var observed = new List<FindGroupJoinedTeamPlan>();
		var service = new PlayerGroupInviteRequestService(
			new FindGroupJoinedTeamLifecycleRecorder(findGroupService, () => 333, serverId: 5, observed.Add));
		var runtime = new PlayerGroupRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		findGroupService.AddRecruitment(inviter, "Leader solo", groupType: 4, nowEpochSeconds: 220);
		findGroupService.AddApplication(invited, "Invite me", groupType: 2, classId: 5, level: 45, nowEpochSeconds: 221);
		findGroupService.AddRecruitment(invited, "Invited solo", groupType: 4, nowEpochSeconds: 222);
		service.SendInvite(inviter, invited);

		var response = service.HandleResponse(
			inviter,
			invited,
			SmQuestionWindow.PartyInvite,
			response: 1,
			runtime,
			newGroupId: 7001);

		Assert.Equal(GroupInviteResponseStatus.Accepted, response.Status);
		Assert.Equal(2, response.FindGroupJoinedTeamPlans.Count);
		Assert.Same(response.FindGroupJoinedTeamPlans[0], observed[0]);
		Assert.All(response.FindGroupJoinedTeamPlans, plan => Assert.False(plan.DispatchLiveSideEffects));
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, response.FindGroupJoinedTeamPlans[0].SoloRecruitmentRemoval.Status);
		Assert.NotNull(response.FindGroupJoinedTeamPlans[0].TeamRecruitmentAdd);
		Assert.Equal(7001, response.FindGroupJoinedTeamPlans[0].TeamRecruitmentAdd!.CurrentRecruitment?.ObjectId);
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, response.FindGroupJoinedTeamPlans[1].ApplicationRemoval.Status);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, response.FindGroupJoinedTeamPlans[1].SoloRecruitmentRemoval.Status);
		Assert.Empty(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 400).Applications);
	}

	[Fact]
	public void HandleResponse_AcceptAddsInvitedToExistingInviterGroup()
	{
		var service = new PlayerGroupInviteRequestService();
		var runtime = new PlayerGroupRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var existing = CreatePlayer(1003, "Existing");
		var invited = CreatePlayer(1002, "Invited");
		runtime.CreateOrUpdateGroup(7001, [inviter, existing]);
		service.SendInvite(inviter, invited);

		var response = service.HandleResponse(
			inviter,
			invited,
			SmQuestionWindow.PartyInvite,
			response: 1,
			runtime,
			newGroupId: 9001);

		Assert.Equal(GroupInviteResponseStatus.Accepted, response.Status);
		Assert.Equal(7001, response.GroupSnapshot?.TeamId);
		Assert.Equal([1001, 1003, 1002], response.GroupSnapshot?.MemberObjectIds);
		Assert.Equal(7001, invited.CurrentGroupSnapshot?.TeamId);
		Assert.NotNull(response.EnteredPacketPlan);
	}

	[Fact]
	public void HandleResponse_AcceptAddsToExistingGroupRecordsFindGroupJoinedTeamOnlyForInvited()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var service = new PlayerGroupInviteRequestService(
			new FindGroupJoinedTeamLifecycleRecorder(findGroupService, () => 333, serverId: 5));
		var runtime = new PlayerGroupRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var existing = CreatePlayer(1003, "Existing");
		var invited = CreatePlayer(1002, "Invited");
		runtime.CreateOrUpdateGroup(7001, [inviter, existing]);
		findGroupService.AddApplication(invited, "Invite me", groupType: 2, classId: 5, level: 45, nowEpochSeconds: 221);
		service.SendInvite(inviter, invited);

		var response = service.HandleResponse(
			inviter,
			invited,
			SmQuestionWindow.PartyInvite,
			response: 1,
			runtime,
			newGroupId: 9001);

		var plan = Assert.Single(response.FindGroupJoinedTeamPlans);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, plan.ApplicationRemoval.Status);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Missing, plan.SoloRecruitmentRemoval.Status);
		Assert.Null(plan.TeamRecruitmentAdd);
	}

	[Fact]
	public void HandleResponse_WrongQuestionLeavesRequest()
	{
		var service = new PlayerGroupInviteRequestService();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		service.SendInvite(inviter, invited);

		var response = service.HandleResponse(
			inviter,
			invited,
			SmQuestionWindow.BuddyListAddBuddyRequest,
			response: 1,
			new PlayerGroupRuntime(),
			newGroupId: 7001);

		Assert.Equal(GroupInviteResponseStatus.Ignored, response.Status);
		Assert.Equal(1, invited.ResponseRequester.Count);
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 45,
			Position = new WorldPosition(210010000, objectId, 20, 30, 0),
		};
	}
}
