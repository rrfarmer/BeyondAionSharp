namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingExchangeRequest(
	int RequesterObjectId,
	int TargetObjectId,
	string RequesterName,
	string TargetName,
	int QuestionId);
