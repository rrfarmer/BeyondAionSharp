using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerLeagueInvitePlanner
{
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
