using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceInviteRequestService
{
	private readonly FindGroupJoinedTeamLifecycleRecorder? _findGroupJoinRecorder;

	public PlayerAllianceInviteRequestService(FindGroupJoinedTeamLifecycleRecorder? findGroupJoinRecorder = null)
	{
		_findGroupJoinRecorder = findGroupJoinRecorder;
	}

	public AllianceInviteRequestResult SendInvite(
		Player inviter,
		Player invited,
		PlayerGroupRuntime groupRuntime,
		PlayerAllianceRuntime allianceRuntime,
		Func<int, Player?> resolvePlayer)
	{
		// Java parity: model/team/alliance/PlayerAllianceService.inviteToAlliance gates through
		// PlayerRestrictions.canInviteToAlliance, redirects invited group members to the group leader,
		// then registers PlayerAllianceInvite under STR_PARTY_ALLIANCE_DO_YOU_ACCEPT_HIS_INVITATION.
		var restrictionMessage = CreateRepresentedRestrictionMessage(inviter, invited, groupRuntime, allianceRuntime);
		if (restrictionMessage != null)
			return AllianceInviteRequestResult.Rejected(restrictionMessage);

		var requesterMessages = new List<SmSystemMessage>();
		var requestTarget = invited;
		var invitedGroup = groupRuntime.Resolve(invited);
		if (invitedGroup != null)
		{
			var groupDescriptor = groupRuntime.GetDescriptor(invitedGroup.TeamId);
			var leader = groupDescriptor == null ? null : resolvePlayer(groupDescriptor.LeaderObjectId);
			if (leader != null && leader.ObjectId != invited.ObjectId)
			{
				requesterMessages.Add(SmSystemMessage.ForceInvitePartyHim(invited.Name, leader.Name));
				requesterMessages.Add(SmSystemMessage.ForceInviteParty(leader.Name, invitedGroup.MemberObjectIds.Count));
				requestTarget = leader;
			}
			else
			{
				requesterMessages.Add(SmSystemMessage.PartyAllianceInvitedHisParty(invited.Name));
			}
		}
		else
		{
			requesterMessages.Add(SmSystemMessage.ForceInvitedHim(invited.Name));
		}

		var request = new PendingAllianceInviteRequest(
			inviter.ObjectId,
			inviter.Name,
			invited.ObjectId,
			invited.Name,
			requestTarget.ObjectId,
			requestTarget.Name);
		if (!requestTarget.ResponseRequester.PutRequest(
			SmQuestionWindow.AllianceInvite,
			new QuestionResponseRequest(inviter.ObjectId, QuestionResponseRequestKind.AllianceInvite, request)))
		{
			return AllianceInviteRequestResult.Duplicate(request, requesterMessages);
		}

		requestTarget.PendingAllianceInviteRequest = request;
		return AllianceInviteRequestResult.Requested(
			request,
			requesterMessages,
			new SmQuestionWindow(SmQuestionWindow.AllianceInvite, 0, 0, inviter.Name));
	}

	public AllianceInviteResponseResult HandleResponse(
		Player responder,
		int questionId,
		int response,
		PlayerGroupRuntime groupRuntime,
		PlayerAllianceRuntime allianceRuntime,
		Func<int> allocateAllianceId,
		Func<int, Player?> resolvePlayer)
	{
		// Java parity: PlayerAllianceInvite.denyRequest sends STR_PARTY_ALLIANCE_HE_REJECT_INVITATION;
		// acceptRequest re-checks restrictions, collects requester/invited group members, removes their
		// groups, and creates/adds to the alliance.
		if (questionId != SmQuestionWindow.AllianceInvite)
			return AllianceInviteResponseResult.Ignored();

		var dispatch = responder.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.AllianceInvite)
			return AllianceInviteResponseResult.MissingRequest();

		var request = dispatch.Request.Payload as PendingAllianceInviteRequest;
		responder.PendingAllianceInviteRequest = null;
		if (request == null)
			return AllianceInviteResponseResult.MissingRequest();

		var requester = resolvePlayer(request.RequesterObjectId);
		if (requester == null)
			return AllianceInviteResponseResult.MissingRequest(request);

		if (!dispatch.Accepted)
			return AllianceInviteResponseResult.Denied(request, SmSystemMessage.PartyAllianceHeRejectInvitation(responder.Name));

		var restrictionMessage = CreateRepresentedRestrictionMessage(requester, responder, groupRuntime, allianceRuntime);
		if (restrictionMessage != null)
			return AllianceInviteResponseResult.Rejected(request, restrictionMessage);

		var playersToAdd = CollectPlayersToAdd(requester, responder, groupRuntime, allianceRuntime, resolvePlayer);
		foreach (var player in playersToAdd.GroupMembersToRemove)
			groupRuntime.RemoveMember(player);

		var requesterAlliance = allianceRuntime.Resolve(requester);
		PlayerAllianceSnapshot snapshot;
		var enteredPlans = new List<PlayerAllianceEnteredPlan>();
		var findGroupJoinedTeamPlans = new List<FindGroupJoinedTeamPlan>();
		if (requesterAlliance == null)
		{
			var newAllianceId = allocateAllianceId();
			if (newAllianceId <= 0)
				return AllianceInviteResponseResult.MissingRequest(request);

			allianceRuntime.CreateAlliance(newAllianceId, requester);
			RecordJoinedTeam(findGroupJoinedTeamPlans, requester, allianceRuntime, newAllianceId);
			snapshot = AddPlayersToAlliance(newAllianceId, playersToAdd.PlayersToAdd, allianceRuntime, enteredPlans, findGroupJoinedTeamPlans);
		}
		else
		{
			snapshot = AddPlayersToAlliance(requesterAlliance.AllianceId, playersToAdd.PlayersToAdd, allianceRuntime, enteredPlans, findGroupJoinedTeamPlans);
		}

		return AllianceInviteResponseResult.Accepted(
			request,
			snapshot,
			enteredPlans,
			findGroupJoinedTeamPlans);
	}

	private static AllianceInviteAcceptCollection CollectPlayersToAdd(
		Player requester,
		Player responder,
		PlayerGroupRuntime groupRuntime,
		PlayerAllianceRuntime allianceRuntime,
		Func<int, Player?> resolvePlayer)
	{
		// Java parity: PlayerAllianceInvite.collectPlayersToAdd first collects the requester group without
		// requester when no alliance exists, then collects the full invited group or the solo invited player.
		var playersToAdd = new List<Player>();
		var groupMembersToRemove = new List<Player>();
		var requesterAlliance = allianceRuntime.Resolve(requester);
		var requesterGroup = groupRuntime.Resolve(requester);
		if (requesterGroup != null && requesterAlliance == null)
		{
			foreach (var member in ResolveGroupMembers(requesterGroup, resolvePlayer))
			{
				groupMembersToRemove.Add(member);
				if (member.ObjectId != requester.ObjectId)
					playersToAdd.Add(member);
			}
		}

		var responderGroup = groupRuntime.Resolve(responder);
		if (responderGroup != null)
		{
			foreach (var member in ResolveGroupMembers(responderGroup, resolvePlayer))
			{
				groupMembersToRemove.Add(member);
				playersToAdd.Add(member);
			}
		}
		else
		{
			playersToAdd.Add(responder);
		}

		return new AllianceInviteAcceptCollection(
			playersToAdd
				.GroupBy(player => player.ObjectId)
				.Select(group => group.First())
				.ToArray(),
			groupMembersToRemove
				.GroupBy(player => player.ObjectId)
				.Select(group => group.First())
				.ToArray());
	}

	private static IReadOnlyList<Player> ResolveGroupMembers(PlayerGroupSnapshot group, Func<int, Player?> resolvePlayer)
	{
		return group.MemberObjectIds
			.Select(resolvePlayer)
			.Where(player => player != null)
			.Cast<Player>()
			.ToArray();
	}

	private PlayerAllianceSnapshot AddPlayersToAlliance(
		int allianceId,
		IReadOnlyList<Player> playersToAdd,
		PlayerAllianceRuntime allianceRuntime,
		List<PlayerAllianceEnteredPlan> enteredPlans,
		List<FindGroupJoinedTeamPlan> findGroupJoinedTeamPlans)
	{
		PlayerAllianceSnapshot? snapshot = null;
		foreach (var player in playersToAdd)
		{
			snapshot = allianceRuntime.AddMember(allianceId, player);
			RecordJoinedTeam(findGroupJoinedTeamPlans, player, allianceRuntime, allianceId);
			var enteredPlan = allianceRuntime.CreateEnteredPlan(allianceId, player);
			if (enteredPlan != null)
				enteredPlans.Add(enteredPlan);
		}

		return snapshot ?? allianceRuntime.GetSnapshot(allianceId) ?? throw new InvalidOperationException("Alliance invite accept produced no alliance snapshot.");
	}

	private void RecordJoinedTeam(
		List<FindGroupJoinedTeamPlan> joinedTeamPlans,
		Player player,
		PlayerAllianceRuntime allianceRuntime,
		int allianceId)
	{
		var plan = _findGroupJoinRecorder?.RecordAllianceJoin(player, allianceRuntime, allianceId);
		if (plan != null)
			joinedTeamPlans.Add(plan);
	}

	private static SmSystemMessage? CreateRepresentedRestrictionMessage(
		Player inviter,
		Player invited,
		PlayerGroupRuntime groupRuntime,
		PlayerAllianceRuntime allianceRuntime)
	{
		if (inviter.IsInState(PlayerCreatureState.Dead) || inviter.LifeStats?.CurrentHp <= 0)
			return SmSystemMessage.ForceCantInviteWhenDead();

		if (inviter.ObjectId == invited.ObjectId)
			return SmSystemMessage.ForceCanNotInviteSelf();

		var inviterAlliance = allianceRuntime.Resolve(inviter);
		if (inviterAlliance != null)
		{
			if (!allianceRuntime.IsLeader(inviterAlliance.AllianceId, inviter)
				&& !allianceRuntime.IsViceCaptain(inviterAlliance.AllianceId, inviter.ObjectId))
				return SmSystemMessage.ForceOnlyLeaderCanInvite();

			if (allianceRuntime.IsFull(inviterAlliance.AllianceId))
				return SmSystemMessage.ForceCantAddNewMember();
		}

		var invitedAlliance = allianceRuntime.Resolve(invited);
		if (invitedAlliance != null)
		{
			return inviterAlliance?.AllianceId == invitedAlliance.AllianceId
				? SmSystemMessage.ForceHeIsAlreadyMemberOfOurForce(invited.Name)
				: SmSystemMessage.ForceAlreadyOtherForce(invited.Name);
		}

		var invitedGroup = groupRuntime.Resolve(invited);
		if (inviterAlliance != null && invitedGroup != null)
		{
			var allianceSize = allianceRuntime.GetMemberObjectIds(inviterAlliance.AllianceId).Count;
			if (allianceSize + invitedGroup.MemberObjectIds.Count > PlayerAllianceDescriptor.JavaMaxMemberCount)
				return SmSystemMessage.ForceInviteFailedNotEnoughSlot();
		}

		return null;
	}
}

public sealed record AllianceInviteRequestResult(
	AllianceInviteRequestStatus Status,
	PendingAllianceInviteRequest? Request,
	IReadOnlyList<SmSystemMessage> RequesterMessages,
	SmQuestionWindow? QuestionWindow,
	SmSystemMessage? RejectionMessage)
{
	public static AllianceInviteRequestResult Requested(
		PendingAllianceInviteRequest request,
		IReadOnlyList<SmSystemMessage> requesterMessages,
		SmQuestionWindow questionWindow)
	{
		return new AllianceInviteRequestResult(
			AllianceInviteRequestStatus.Requested,
			request,
			requesterMessages,
			questionWindow,
			null);
	}

	public static AllianceInviteRequestResult Duplicate(
		PendingAllianceInviteRequest request,
		IReadOnlyList<SmSystemMessage> requesterMessages)
	{
		return new AllianceInviteRequestResult(
			AllianceInviteRequestStatus.DuplicateRequest,
			request,
			requesterMessages,
			null,
			null);
	}

	public static AllianceInviteRequestResult Rejected(SmSystemMessage rejectionMessage)
	{
		return new AllianceInviteRequestResult(
			AllianceInviteRequestStatus.Rejected,
			null,
			Array.Empty<SmSystemMessage>(),
			null,
			rejectionMessage);
	}
}

public enum AllianceInviteRequestStatus
{
	Requested,
	DuplicateRequest,
	Rejected,
}

public sealed record AllianceInviteResponseResult(
	AllianceInviteResponseStatus Status,
	PendingAllianceInviteRequest? Request,
	SmSystemMessage? Message,
	PlayerAllianceSnapshot? AllianceSnapshot,
	IReadOnlyList<PlayerAllianceEnteredPlan> EnteredPlans,
	IReadOnlyList<FindGroupJoinedTeamPlan> FindGroupJoinedTeamPlans)
{
	public static AllianceInviteResponseResult Ignored()
	{
		return new AllianceInviteResponseResult(AllianceInviteResponseStatus.Ignored, null, null, null, Array.Empty<PlayerAllianceEnteredPlan>(), Array.Empty<FindGroupJoinedTeamPlan>());
	}

	public static AllianceInviteResponseResult MissingRequest(PendingAllianceInviteRequest? request = null)
	{
		return new AllianceInviteResponseResult(AllianceInviteResponseStatus.MissingRequest, request, null, null, Array.Empty<PlayerAllianceEnteredPlan>(), Array.Empty<FindGroupJoinedTeamPlan>());
	}

	public static AllianceInviteResponseResult Denied(PendingAllianceInviteRequest request, SmSystemMessage denyMessage)
	{
		return new AllianceInviteResponseResult(AllianceInviteResponseStatus.Denied, request, denyMessage, null, Array.Empty<PlayerAllianceEnteredPlan>(), Array.Empty<FindGroupJoinedTeamPlan>());
	}

	public static AllianceInviteResponseResult Rejected(PendingAllianceInviteRequest request, SmSystemMessage rejectionMessage)
	{
		return new AllianceInviteResponseResult(AllianceInviteResponseStatus.Rejected, request, rejectionMessage, null, Array.Empty<PlayerAllianceEnteredPlan>(), Array.Empty<FindGroupJoinedTeamPlan>());
	}

	public static AllianceInviteResponseResult Accepted(
		PendingAllianceInviteRequest request,
		PlayerAllianceSnapshot allianceSnapshot,
		IReadOnlyList<PlayerAllianceEnteredPlan> enteredPlans,
		IReadOnlyList<FindGroupJoinedTeamPlan>? findGroupJoinedTeamPlans = null)
	{
		return new AllianceInviteResponseResult(
			AllianceInviteResponseStatus.Accepted,
			request,
			null,
			allianceSnapshot,
			enteredPlans,
			findGroupJoinedTeamPlans ?? Array.Empty<FindGroupJoinedTeamPlan>());
	}
}

public enum AllianceInviteResponseStatus
{
	Ignored,
	MissingRequest,
	Denied,
	Rejected,
	Accepted,
}

public sealed record AllianceInviteAcceptCollection(
	IReadOnlyList<Player> PlayersToAdd,
	IReadOnlyList<Player> GroupMembersToRemove);
