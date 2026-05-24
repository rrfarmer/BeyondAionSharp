namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingExperienceRecoveryRequest(
	int NpcObjectId,
	long RecoverableExp,
	int Price);
