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
