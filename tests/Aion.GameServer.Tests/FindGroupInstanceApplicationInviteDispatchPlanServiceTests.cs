using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class FindGroupInstanceApplicationInviteDispatchPlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_GroupInviteIntentRegistersPartyQuestionWithoutLiveDispatch()
	{
		var service = new FindGroupInstanceApplicationInviteDispatchPlanService();
		var inviter = CreatePlayer(1001, "Responder");
		var invited = CreatePlayer(1002, "Applicant");
		var intent = new FindGroupInstanceInviteIntent(
			FindGroupInstanceInviteKind.Group,
			inviter.ObjectId,
			invited.ObjectId,
			"PlayerGroupService.inviteToGroup(responder, applicant)");

		var plan = service.CreateDisabledPlan(
			intent,
			Resolve(inviter, invited),
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());

		Assert.Equal(FindGroupInstanceApplicationInviteDispatchStatus.GroupInvitePlanned, plan.Status);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.NotNull(plan.GroupInviteRequest);
		Assert.Null(plan.AllianceInviteRequest);
		Assert.Equal(GroupInviteRequestStatus.Requested, plan.GroupInviteRequest!.Status);
		Assert.Equal(inviter.ObjectId, plan.GroupInviteRequest.Request.InviterObjectId);
		Assert.Equal(SmQuestionWindow.PartyInvite, plan.GroupInviteRequest.QuestionWindow?.Code);
		Assert.Equal(1, invited.ResponseRequester.Count);
	}

	[Fact]
	public void CreateDisabledPlan_AllianceInviteIntentRegistersAllianceQuestionWithoutLiveDispatch()
	{
		var service = new FindGroupInstanceApplicationInviteDispatchPlanService();
		var inviter = CreatePlayer(1001, "Responder");
		var invited = CreatePlayer(1002, "Applicant");
		var intent = new FindGroupInstanceInviteIntent(
			FindGroupInstanceInviteKind.Alliance,
			inviter.ObjectId,
			invited.ObjectId,
			"PlayerAllianceService.inviteToAlliance(responder, applicant)");

		var plan = service.CreateDisabledPlan(
			intent,
			Resolve(inviter, invited),
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());

		Assert.Equal(FindGroupInstanceApplicationInviteDispatchStatus.AllianceInvitePlanned, plan.Status);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Null(plan.GroupInviteRequest);
		Assert.NotNull(plan.AllianceInviteRequest);
		Assert.Equal(AllianceInviteRequestStatus.Requested, plan.AllianceInviteRequest!.Status);
		Assert.Equal(inviter.ObjectId, plan.AllianceInviteRequest.Request?.RequesterObjectId);
		Assert.Equal(SmQuestionWindow.AllianceInvite, plan.AllianceInviteRequest.QuestionWindow?.Code);
		Assert.Equal(1, invited.ResponseRequester.Count);
		Assert.NotNull(invited.PendingAllianceInviteRequest);
	}

	[Fact]
	public void CreateDisabledPlan_MissingApplicantSkipsWithoutQuestionMutation()
	{
		var service = new FindGroupInstanceApplicationInviteDispatchPlanService();
		var inviter = CreatePlayer(1001, "Responder");
		var intent = new FindGroupInstanceInviteIntent(
			FindGroupInstanceInviteKind.Group,
			inviter.ObjectId,
			404,
			"PlayerGroupService.inviteToGroup(responder, applicant)");

		var plan = service.CreateDisabledPlan(
			intent,
			Resolve(inviter),
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());

		Assert.Equal(FindGroupInstanceApplicationInviteDispatchStatus.SkippedMissingPlayer, plan.Status);
		Assert.False(plan.MissingInviter);
		Assert.True(plan.MissingInvited);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Equal(0, inviter.ResponseRequester.Count);
	}

	[Fact]
	public void CreateDisabledPlan_MissingIntentSkipsWithoutLiveDispatch()
	{
		var service = new FindGroupInstanceApplicationInviteDispatchPlanService();

		var plan = service.CreateDisabledPlan(
			inviteIntent: null,
			_ => null,
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());

		Assert.Equal(FindGroupInstanceApplicationInviteDispatchStatus.SkippedMissingIntent, plan.Status);
		Assert.False(plan.DispatchLiveSideEffects);
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
			Level = 65,
			Position = new WorldPosition(210010000, objectId, 20, 30, 0),
		};
	}
}
