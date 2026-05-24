namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingDuelRequest(
	int RequesterObjectId,
	string RequesterName,
	int TargetObjectId,
	string TargetName);
