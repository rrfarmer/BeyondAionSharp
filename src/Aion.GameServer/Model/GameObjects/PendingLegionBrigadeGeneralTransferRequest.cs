namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingLegionBrigadeGeneralTransferRequest(
	int RequesterObjectId,
	string RequesterName,
	int TargetObjectId,
	string TargetName,
	int LegionId);
