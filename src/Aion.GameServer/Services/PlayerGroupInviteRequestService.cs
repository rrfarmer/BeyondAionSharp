using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerGroupInviteRequestService
{
	private readonly FindGroupJoinedTeamLifecycleRecorder? _findGroupJoinRecorder;

	public PlayerGroupInviteRequestService(FindGroupJoinedTeamLifecycleRecorder? findGroupJoinRecorder = null)
	{
		_findGroupJoinRecorder = findGroupJoinRecorder;
	}

	public GroupInviteRequestResult SendInvite(Player inviter, Player invited)
	{
		// Java parity: model/team/group/PlayerGroupService.inviteToGroup registers
		// PlayerGroupInvite under STR_PARTY_DO_YOU_ACCEPT_INVITATION.
		var request = new PendingGroupInviteRequest(inviter.ObjectId, inviter.Name);
		var inviterMessage = SmSystemMessage.PartyInvitedHim(invited.Name);
		if (!invited.ResponseRequester.PutRequest(
			SmQuestionWindow.PartyInvite,
			new QuestionResponseRequest(inviter.ObjectId, QuestionResponseRequestKind.GroupInvite, request)))
		{
			return GroupInviteRequestResult.Duplicate(request, inviterMessage);
		}

		return GroupInviteRequestResult.Requested(
			request,
			inviterMessage,
			new SmQuestionWindow(SmQuestionWindow.PartyInvite, 0, 0, inviter.Name));
	}

	public GroupInviteResponseResult HandleResponse(
		Player inviter,
		Player invited,
		int questionId,
		int response,
		PlayerGroupRuntime groupRuntime,
		int newGroupId)
	{
		// Java parity: PlayerGroupInvite.acceptRequest creates or joins the inviter's group;
		// denyRequest sends STR_PARTY_HE_REJECT_INVITATION(invited.getName()) to the inviter.
		if (questionId != SmQuestionWindow.PartyInvite)
			return GroupInviteResponseResult.Ignored();

		var dispatch = invited.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.GroupInvite)
			return GroupInviteResponseResult.MissingRequest();

		var request = dispatch.Request.Payload as PendingGroupInviteRequest;
		if (request == null || request.InviterObjectId != inviter.ObjectId)
			return GroupInviteResponseResult.MissingRequest();

		return HandleResolvedResponse(inviter, invited, dispatch.Accepted, request, groupRuntime, newGroupId);
	}

	public GroupInviteResponseResult HandleResponse(
		Player invited,
		int questionId,
		int response,
		PlayerGroupRuntime groupRuntime,
		Func<int> allocateGroupId,
		Func<int, Player?> resolveInviter)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond, which removes
		// the PlayerGroupInvite handler before invoking denyRequest or acceptRequest.
		if (questionId != SmQuestionWindow.PartyInvite)
			return GroupInviteResponseResult.Ignored();

		var dispatch = invited.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.GroupInvite)
			return GroupInviteResponseResult.MissingRequest();

		var request = dispatch.Request.Payload as PendingGroupInviteRequest;
		if (request == null)
			return GroupInviteResponseResult.MissingRequest();

		var inviter = resolveInviter(request.InviterObjectId);
		return inviter == null
			? GroupInviteResponseResult.MissingRequest(request)
			: HandleResolvedResponse(
				inviter,
				invited,
				dispatch.Accepted,
				request,
				groupRuntime,
				dispatch.Accepted && groupRuntime.Resolve(inviter) == null ? allocateGroupId() : 0);
	}

	private GroupInviteResponseResult HandleResolvedResponse(
		Player inviter,
		Player invited,
		bool accepted,
		PendingGroupInviteRequest request,
		PlayerGroupRuntime groupRuntime,
		int newGroupId)
	{
		if (!accepted)
			return GroupInviteResponseResult.Denied(request, SmSystemMessage.PartyHeRejectInvitation(invited.Name));

		var inviterGroup = groupRuntime.Resolve(inviter);
		if (inviterGroup == null && newGroupId <= 0)
			return GroupInviteResponseResult.MissingRequest(request);

		var joinedTeamPlans = new List<FindGroupJoinedTeamPlan>();
		var snapshot = inviterGroup == null
			? groupRuntime.CreateOrUpdateGroup(newGroupId, [inviter, invited])
			: groupRuntime.AddMember(inviterGroup.TeamId, invited);
		if (inviterGroup == null)
		{
			RecordJoinedTeam(joinedTeamPlans, inviter, groupRuntime, snapshot.TeamId);
			RecordJoinedTeam(joinedTeamPlans, invited, groupRuntime, snapshot.TeamId);
		}
		else
		{
			RecordJoinedTeam(joinedTeamPlans, invited, groupRuntime, snapshot.TeamId);
		}

		var enteredPlan = groupRuntime.CreateEnteredPacketPlan(snapshot.TeamId, invited);
		return GroupInviteResponseResult.Accepted(request, snapshot, enteredPlan, joinedTeamPlans);
	}

	private void RecordJoinedTeam(
		List<FindGroupJoinedTeamPlan> joinedTeamPlans,
		Player player,
		PlayerGroupRuntime groupRuntime,
		int teamId)
	{
		var plan = _findGroupJoinRecorder?.RecordGroupJoin(player, groupRuntime, teamId);
		if (plan != null)
			joinedTeamPlans.Add(plan);
	}
}

public sealed record PendingGroupInviteRequest(int InviterObjectId, string InviterName);

public sealed record GroupInviteRequestResult(
	GroupInviteRequestStatus Status,
	PendingGroupInviteRequest Request,
	SmSystemMessage InviterMessage,
	SmQuestionWindow? QuestionWindow)
{
	public static GroupInviteRequestResult Requested(
		PendingGroupInviteRequest request,
		SmSystemMessage inviterMessage,
		SmQuestionWindow questionWindow)
	{
		return new GroupInviteRequestResult(
			GroupInviteRequestStatus.Requested,
			request,
			inviterMessage,
			questionWindow);
	}

	public static GroupInviteRequestResult Duplicate(
		PendingGroupInviteRequest request,
		SmSystemMessage inviterMessage)
	{
		return new GroupInviteRequestResult(
			GroupInviteRequestStatus.DuplicateRequest,
			request,
			inviterMessage,
			null);
	}
}

public enum GroupInviteRequestStatus
{
	Requested,
	DuplicateRequest,
}

public sealed record GroupInviteResponseResult(
	GroupInviteResponseStatus Status,
	PendingGroupInviteRequest? Request,
	SmSystemMessage? DenyMessage,
	PlayerGroupSnapshot? GroupSnapshot,
	PlayerGroupEnteredPacketPlan? EnteredPacketPlan,
	IReadOnlyList<FindGroupJoinedTeamPlan> FindGroupJoinedTeamPlans)
{
	public static GroupInviteResponseResult Ignored()
	{
		return new GroupInviteResponseResult(GroupInviteResponseStatus.Ignored, null, null, null, null, Array.Empty<FindGroupJoinedTeamPlan>());
	}

	public static GroupInviteResponseResult MissingRequest()
	{
		return new GroupInviteResponseResult(GroupInviteResponseStatus.MissingRequest, null, null, null, null, Array.Empty<FindGroupJoinedTeamPlan>());
	}

	public static GroupInviteResponseResult MissingRequest(PendingGroupInviteRequest request)
	{
		return new GroupInviteResponseResult(GroupInviteResponseStatus.MissingRequest, request, null, null, null, Array.Empty<FindGroupJoinedTeamPlan>());
	}

	public static GroupInviteResponseResult Denied(PendingGroupInviteRequest request, SmSystemMessage denyMessage)
	{
		return new GroupInviteResponseResult(GroupInviteResponseStatus.Denied, request, denyMessage, null, null, Array.Empty<FindGroupJoinedTeamPlan>());
	}

	public static GroupInviteResponseResult Accepted(
		PendingGroupInviteRequest request,
		PlayerGroupSnapshot groupSnapshot,
		PlayerGroupEnteredPacketPlan? enteredPacketPlan,
		IReadOnlyList<FindGroupJoinedTeamPlan>? findGroupJoinedTeamPlans = null)
	{
		return new GroupInviteResponseResult(
			GroupInviteResponseStatus.Accepted,
			request,
			null,
			groupSnapshot,
			enteredPacketPlan,
			findGroupJoinedTeamPlans ?? Array.Empty<FindGroupJoinedTeamPlan>());
	}
}

public enum GroupInviteResponseStatus
{
	Ignored,
	MissingRequest,
	Denied,
	Accepted,
}
