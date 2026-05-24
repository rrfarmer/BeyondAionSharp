namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingLeagueInviteRequest(
	int QuestionId,
	int RequesterObjectId,
	int RequestTargetObjectId,
	int SelectedPlayerObjectId,
	int InvitedAllianceId);
