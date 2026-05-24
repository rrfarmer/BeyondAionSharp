using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerGroupInviteRequestService
{
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

		if (!dispatch.Accepted)
			return GroupInviteResponseResult.Denied(request, SmSystemMessage.PartyHeRejectInvitation(invited.Name));

		var inviterGroup = groupRuntime.Resolve(inviter);
		var snapshot = inviterGroup == null
			? groupRuntime.CreateOrUpdateGroup(newGroupId, [inviter, invited])
			: groupRuntime.AddMember(inviterGroup.TeamId, invited);
		var enteredPlan = groupRuntime.CreateEnteredPacketPlan(snapshot.TeamId, invited);
		return GroupInviteResponseResult.Accepted(request, snapshot, enteredPlan);
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
	PlayerGroupEnteredPacketPlan? EnteredPacketPlan)
{
	public static GroupInviteResponseResult Ignored()
	{
		return new GroupInviteResponseResult(GroupInviteResponseStatus.Ignored, null, null, null, null);
	}

	public static GroupInviteResponseResult MissingRequest()
	{
		return new GroupInviteResponseResult(GroupInviteResponseStatus.MissingRequest, null, null, null, null);
	}

	public static GroupInviteResponseResult Denied(PendingGroupInviteRequest request, SmSystemMessage denyMessage)
	{
		return new GroupInviteResponseResult(GroupInviteResponseStatus.Denied, request, denyMessage, null, null);
	}

	public static GroupInviteResponseResult Accepted(
		PendingGroupInviteRequest request,
		PlayerGroupSnapshot groupSnapshot,
		PlayerGroupEnteredPacketPlan? enteredPacketPlan)
	{
		return new GroupInviteResponseResult(
			GroupInviteResponseStatus.Accepted,
			request,
			null,
			groupSnapshot,
			enteredPacketPlan);
	}
}

public enum GroupInviteResponseStatus
{
	Ignored,
	MissingRequest,
	Denied,
	Accepted,
}
