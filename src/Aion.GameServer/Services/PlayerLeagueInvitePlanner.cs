using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerLeagueInvitePlanner
{
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

public sealed record PlayerLeagueInviteDenyPlan(
	int RequesterObjectId,
	string ResponderName,
	PlayerAllianceSystemMessageIntent SystemMessageIntent);
