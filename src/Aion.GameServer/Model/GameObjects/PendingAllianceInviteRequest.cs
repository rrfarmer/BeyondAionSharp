namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingAllianceInviteRequest(
	int RequesterObjectId,
	string RequesterName,
	int SelectedObjectId,
	string SelectedName,
	int RequestTargetObjectId,
	string RequestTargetName);
