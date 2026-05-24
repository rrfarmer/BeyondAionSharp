using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public sealed class PlayerLeagueInvitePlanner
{
	public PlayerLeagueInviteRequestSetupPlan CreateRequestSetupPlan(
		Player inviter,
		Player invited,
		PlayerAllianceRuntime allianceRuntime)
	{
		// Java parity: LeagueService.inviteToLeague after canInvite succeeds. If the selected player is not the
		// invited alliance leader, Java notifies the inviter and redirects the actual request to the leader.
		// It then registers SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME and sends invite confirmation/question packets.
		ArgumentNullException.ThrowIfNull(inviter);
		ArgumentNullException.ThrowIfNull(invited);
		ArgumentNullException.ThrowIfNull(allianceRuntime);

		var invitedAlliance = invited.CurrentAllianceSnapshot
			?? throw new InvalidOperationException("Invited player alliance should not be null");
		var invitedAllianceDescriptor = allianceRuntime.GetDescriptor(invitedAlliance.AllianceId)
			?? throw new InvalidOperationException($"Alliance should not be null: {invitedAlliance.AllianceId}");
		var leader = allianceRuntime.GetMember(invitedAlliance.AllianceId, invitedAllianceDescriptor.LeaderObjectId)
			?? throw new InvalidOperationException($"Alliance leader should not be null: {invitedAllianceDescriptor.LeaderObjectId}");

		var requesterIntents = new List<PlayerAllianceSystemMessageIntent>();
		if (leader.ObjectId != invited.ObjectId)
		{
			requesterIntents.Add(new PlayerAllianceSystemMessageIntent(
				inviter.ObjectId,
				SmSystemMessage.UnionInviteHisLeader(invited.Name, leader.Name)));
		}

		requesterIntents.Add(new PlayerAllianceSystemMessageIntent(
			inviter.ObjectId,
			SmSystemMessage.UnionInviteHim(leader.Name, invitedAlliance.AllianceGroupSize)));

		return new PlayerLeagueInviteRequestSetupPlan(
			InviterObjectId: inviter.ObjectId,
			SelectedPlayerObjectId: invited.ObjectId,
			RequestTargetObjectId: leader.ObjectId,
			RequestTargetName: leader.Name,
			InvitedAllianceId: invitedAlliance.AllianceId,
			InvitedAllianceSize: invitedAlliance.AllianceGroupSize,
			QuestionCode: SmQuestionWindow.UnionInviteMe,
			RequesterSystemMessages: requesterIntents,
			QuestionWindowIntent: new PlayerLeagueQuestionWindowIntent(
				leader.ObjectId,
				new SmQuestionWindow(SmQuestionWindow.UnionInviteMe, senderObjectId: 0, rangeOrCooldownSeconds: 0, inviter.Name)));
	}

	public PlayerLeagueInvitePendingRequestPlan TryPutPendingRequest(
		Player requestTarget,
		PlayerLeagueInviteRequestSetupPlan setupPlan)
	{
		// Java parity: ResponseRequester.putRequest(messageId, handler) uses putIfAbsent by question id.
		// This narrow model stores the league invite request metadata until CM_QUESTION_RESPONSE support is ported.
		ArgumentNullException.ThrowIfNull(requestTarget);
		ArgumentNullException.ThrowIfNull(setupPlan);

		if (requestTarget.PendingLeagueInviteRequest != null)
		{
			return new PlayerLeagueInvitePendingRequestPlan(
				requestTarget.ObjectId,
				setupPlan.QuestionCode,
				Registered: false,
				requestTarget.PendingLeagueInviteRequest);
		}

		var pendingRequest = new PendingLeagueInviteRequest(
			setupPlan.QuestionCode,
			setupPlan.InviterObjectId,
			setupPlan.RequestTargetObjectId,
			setupPlan.SelectedPlayerObjectId,
			setupPlan.InvitedAllianceId);
		requestTarget.PendingLeagueInviteRequest = pendingRequest;

		return new PlayerLeagueInvitePendingRequestPlan(
			requestTarget.ObjectId,
			setupPlan.QuestionCode,
			Registered: true,
			pendingRequest);
	}

	public PlayerLeagueInviteResponsePlan CreatePendingRequestResponsePlan(
		Player requester,
		Player responder,
		int questionId,
		int responseCode,
		PlayerLeagueRuntime leagueRuntime,
		PlayerAllianceRuntime allianceRuntime)
	{
		// Java parity: ResponseRequester.respond removes the handler by question id, then
		// RequestResponseHandler.handle dispatches response 0 to denyRequest and nonzero to acceptRequest.
		ArgumentNullException.ThrowIfNull(requester);
		ArgumentNullException.ThrowIfNull(responder);
		ArgumentNullException.ThrowIfNull(leagueRuntime);
		ArgumentNullException.ThrowIfNull(allianceRuntime);

		var pendingRequest = responder.PendingLeagueInviteRequest;
		if (pendingRequest == null || pendingRequest.QuestionId != questionId)
		{
			return new PlayerLeagueInviteResponsePlan(
				PlayerLeagueInviteResponseStatus.NoPendingRequest,
				questionId,
				responseCode,
				PendingRequest: pendingRequest,
				CanInvitePlan: null,
				AcceptPlan: null,
				DenyPlan: null);
		}

		responder.PendingLeagueInviteRequest = null;

		if (responseCode == 0)
		{
			return new PlayerLeagueInviteResponsePlan(
				PlayerLeagueInviteResponseStatus.Denied,
				questionId,
				responseCode,
				pendingRequest,
				CanInvitePlan: null,
				AcceptPlan: null,
				DenyPlan: CreateDenyPlan(requester.ObjectId, responder.Name));
		}

		var firstChecks = CreateCanInviteFirstChecksPlan(requester, responder);
		if (firstChecks.Status != PlayerLeagueCanInviteStatus.PassedRepresentedChecks)
		{
			return CreateAcceptBlockedResponsePlan(questionId, responseCode, pendingRequest, firstChecks);
		}

		var allianceChecks = CreateCanInviteAllianceChecksPlan(requester, responder, leagueRuntime);
		if (allianceChecks.Status != PlayerLeagueCanInviteStatus.PassedRepresentedChecks)
		{
			return CreateAcceptBlockedResponsePlan(questionId, responseCode, pendingRequest, allianceChecks);
		}

		var requesterAllianceId = requester.CurrentAllianceSnapshot?.AllianceId
			?? throw new InvalidOperationException("Requester alliance should not be null");
		var invitedAllianceId = responder.CurrentAllianceSnapshot?.AllianceId
			?? pendingRequest.InvitedAllianceId;
		var acceptPlan = CreateAcceptExistingLeaguePlan(
			requesterAllianceId,
			invitedAllianceId,
			leagueRuntime,
			allianceRuntime);

		return new PlayerLeagueInviteResponsePlan(
			acceptPlan.Status switch
			{
				PlayerLeagueInviteAcceptStatus.Joined => PlayerLeagueInviteResponseStatus.AcceptedJoined,
				PlayerLeagueInviteAcceptStatus.RequesterLeagueMissing => PlayerLeagueInviteResponseStatus.AcceptedRequesterLeagueMissing,
				PlayerLeagueInviteAcceptStatus.InvitedAlreadyInLeague => PlayerLeagueInviteResponseStatus.AcceptedInvitedAlreadyInLeague,
				_ => throw new ArgumentOutOfRangeException(nameof(acceptPlan.Status), acceptPlan.Status, "Unsupported league invite accept status."),
			},
			questionId,
			responseCode,
			pendingRequest,
			CanInvitePlan: allianceChecks,
			AcceptPlan: acceptPlan,
			DenyPlan: null);
	}

	public PlayerLeagueCanInvitePlan CreateCanInviteFirstChecksPlan(
		Player inviter,
		Player invited)
	{
		// Java parity: LeagueService.canInvite first failure checks, in source order:
		// inviter.isDead(), !invited.isOnline(), invited.getPlayerAlliance() == null.
		ArgumentNullException.ThrowIfNull(inviter);
		ArgumentNullException.ThrowIfNull(invited);

		if (inviter.IsInState(PlayerCreatureState.Dead))
		{
			return CreateCanInviteFailurePlan(
				inviter.ObjectId,
				PlayerLeagueCanInviteStatus.InviterDead,
				SmSystemMessage.UnionCantInviteWhenDead());
		}

		if (!invited.IsOnline)
		{
			return CreateCanInviteFailurePlan(
				inviter.ObjectId,
				PlayerLeagueCanInviteStatus.InvitedOffline,
				SmSystemMessage.UnionOfflineMember());
		}

		if (invited.TeamMembership != PlayerTeamMembership.Alliance || invited.CurrentAllianceSnapshot == null)
		{
			return CreateCanInviteFailurePlan(
				inviter.ObjectId,
				PlayerLeagueCanInviteStatus.InvitedWithoutAlliance,
				SmSystemMessage.UnionCantInviteWhenHeIsAskedQuestion(invited.Name));
		}

		return new PlayerLeagueCanInvitePlan(
			PlayerLeagueCanInviteStatus.PassedRepresentedChecks,
			SystemMessageIntent: null);
	}

	public PlayerLeagueCanInvitePlan CreateCanInviteAllianceChecksPlan(
		Player inviter,
		Player invited,
		PlayerLeagueRuntime leagueRuntime)
	{
		// Java parity: LeagueService.canInvite middle failure checks, after the dead/offline/no-alliance checks:
		// own alliance, invited already in league, and requester league full.
		ArgumentNullException.ThrowIfNull(inviter);
		ArgumentNullException.ThrowIfNull(invited);
		ArgumentNullException.ThrowIfNull(leagueRuntime);

		var inviterAlliance = inviter.CurrentAllianceSnapshot;
		var invitedAlliance = invited.CurrentAllianceSnapshot;
		if (inviterAlliance == null || invitedAlliance == null)
		{
			return new PlayerLeagueCanInvitePlan(
				PlayerLeagueCanInviteStatus.PassedRepresentedChecks,
				SystemMessageIntent: null);
		}

		if (inviterAlliance.MemberObjectIds.Contains(invited.ObjectId))
		{
			return CreateCanInviteFailurePlan(
				inviter.ObjectId,
				PlayerLeagueCanInviteStatus.InvitedInOwnAlliance,
				SmSystemMessage.UnionCantInviteSelf());
		}

		if (leagueRuntime.ResolveByAllianceId(invitedAlliance.AllianceId) != null)
		{
			return CreateCanInviteFailurePlan(
				inviter.ObjectId,
				PlayerLeagueCanInviteStatus.InvitedAlreadyInLeague,
				SmSystemMessage.UnionAlreadyMyUnion());
		}

		var inviterLeague = leagueRuntime.ResolveByAllianceId(inviterAlliance.AllianceId);
		if (inviterLeague != null && inviterLeague.AllianceIdsByPosition.Count >= 8)
		{
			return CreateCanInviteFailurePlan(
				inviter.ObjectId,
				PlayerLeagueCanInviteStatus.InviterLeagueFull,
				SmSystemMessage.UnionCantAddNewMember());
		}

		return new PlayerLeagueCanInvitePlan(
			PlayerLeagueCanInviteStatus.PassedRepresentedChecks,
			SystemMessageIntent: null);
	}

	public PlayerLeagueInviteAcceptPlan CreateAcceptExistingLeaguePlan(
		int requesterAllianceId,
		int invitedAllianceId,
		PlayerLeagueRuntime leagueRuntime,
		PlayerAllianceRuntime allianceRuntime)
	{
		// Java parity: LeagueInviteEvent.acceptRequest, after LeagueService.canInvite succeeds, reuses the
		// requester's existing League and calls LeagueService.addAlliance when the invited alliance is not in a league.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requesterAllianceId, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(invitedAllianceId, 0);

		var requesterLeague = leagueRuntime.ResolveByAllianceId(requesterAllianceId);
		if (requesterLeague == null)
		{
			return new PlayerLeagueInviteAcceptPlan(
				requesterAllianceId,
				invitedAllianceId,
				PlayerLeagueInviteAcceptStatus.RequesterLeagueMissing,
				JoinPlan: null);
		}

		if (leagueRuntime.ResolveByAllianceId(invitedAllianceId) != null)
		{
			return new PlayerLeagueInviteAcceptPlan(
				requesterAllianceId,
				invitedAllianceId,
				PlayerLeagueInviteAcceptStatus.InvitedAlreadyInLeague,
				JoinPlan: null);
		}

		var joinPlan = leagueRuntime.JoinAlliance(
			requesterLeague.LeagueId,
			invitedAllianceId,
			allianceRuntime);

		return new PlayerLeagueInviteAcceptPlan(
			requesterAllianceId,
			invitedAllianceId,
			joinPlan != null ? PlayerLeagueInviteAcceptStatus.Joined : PlayerLeagueInviteAcceptStatus.InvitedAlreadyInLeague,
			joinPlan);
	}

	public PlayerLeagueInviteDenyPlan CreateDenyPlan(
		int requesterObjectId,
		string responderName)
	{
		// Java parity: model/team/league/events/LeagueInviteEvent.denyRequest sends
		// SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_HE_REJECT_INVITATION(responder.getName()) to the requester.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requesterObjectId, 0);
		ArgumentException.ThrowIfNullOrWhiteSpace(responderName);

		return new PlayerLeagueInviteDenyPlan(
			requesterObjectId,
			responderName,
			new PlayerAllianceSystemMessageIntent(
				requesterObjectId,
				SmSystemMessage.PartyAllianceHeRejectInvitation(responderName)));
	}

	private static PlayerLeagueCanInvitePlan CreateCanInviteFailurePlan(
		int inviterObjectId,
		PlayerLeagueCanInviteStatus status,
		SmSystemMessage message)
	{
		return new PlayerLeagueCanInvitePlan(
			status,
			new PlayerAllianceSystemMessageIntent(inviterObjectId, message));
	}

	private static PlayerLeagueInviteResponsePlan CreateAcceptBlockedResponsePlan(
		int questionId,
		int responseCode,
		PendingLeagueInviteRequest pendingRequest,
		PlayerLeagueCanInvitePlan canInvitePlan)
	{
		return new PlayerLeagueInviteResponsePlan(
			PlayerLeagueInviteResponseStatus.AcceptBlockedByCanInvite,
			questionId,
			responseCode,
			pendingRequest,
			canInvitePlan,
			AcceptPlan: null,
			DenyPlan: null);
	}
}

public enum PlayerLeagueCanInviteStatus
{
	PassedRepresentedChecks,
	InviterDead,
	InvitedOffline,
	InvitedWithoutAlliance,
	InvitedInOwnAlliance,
	InvitedAlreadyInLeague,
	InviterLeagueFull,
}

public sealed record PlayerLeagueCanInvitePlan(
	PlayerLeagueCanInviteStatus Status,
	PlayerAllianceSystemMessageIntent? SystemMessageIntent);

public sealed record PlayerLeagueInviteRequestSetupPlan(
	int InviterObjectId,
	int SelectedPlayerObjectId,
	int RequestTargetObjectId,
	string RequestTargetName,
	int InvitedAllianceId,
	int InvitedAllianceSize,
	int QuestionCode,
	IReadOnlyList<PlayerAllianceSystemMessageIntent> RequesterSystemMessages,
	PlayerLeagueQuestionWindowIntent QuestionWindowIntent);

public sealed record PlayerLeagueQuestionWindowIntent(
	int RecipientObjectId,
	SmQuestionWindow QuestionWindow)
{
	public GameServerPacket CreatePacket()
	{
		return QuestionWindow;
	}
}

public sealed record PlayerLeagueInvitePendingRequestPlan(
	int RequestTargetObjectId,
	int QuestionCode,
	bool Registered,
	PendingLeagueInviteRequest PendingRequest);

public enum PlayerLeagueInviteResponseStatus
{
	NoPendingRequest,
	Denied,
	AcceptBlockedByCanInvite,
	AcceptedJoined,
	AcceptedRequesterLeagueMissing,
	AcceptedInvitedAlreadyInLeague,
}

public sealed record PlayerLeagueInviteResponsePlan(
	PlayerLeagueInviteResponseStatus Status,
	int QuestionId,
	int ResponseCode,
	PendingLeagueInviteRequest? PendingRequest,
	PlayerLeagueCanInvitePlan? CanInvitePlan,
	PlayerLeagueInviteAcceptPlan? AcceptPlan,
	PlayerLeagueInviteDenyPlan? DenyPlan);

public enum PlayerLeagueInviteAcceptStatus
{
	Joined,
	RequesterLeagueMissing,
	InvitedAlreadyInLeague,
}

public sealed record PlayerLeagueInviteAcceptPlan(
	int RequesterAllianceId,
	int InvitedAllianceId,
	PlayerLeagueInviteAcceptStatus Status,
	PlayerLeagueJoinPlan? JoinPlan);

public sealed record PlayerLeagueInviteDenyPlan(
	int RequesterObjectId,
	string ResponderName,
	PlayerAllianceSystemMessageIntent SystemMessageIntent);
