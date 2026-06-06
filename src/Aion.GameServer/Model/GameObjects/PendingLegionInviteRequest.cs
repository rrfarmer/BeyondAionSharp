namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingLegionInviteRequest(
	int InviterObjectId,
	string InviterName,
	int TargetObjectId,
	string TargetName,
	int LegionId,
	string LegionName,
	int LegionLevel);
