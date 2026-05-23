namespace Aion.GameServer.Services;

public sealed record PlayerGroupBrandUpdatePlan(
	int TeamId,
	int BrandId,
	int TargetObjectId,
	IReadOnlyList<PlayerGroupBrandIntent> BrandBroadcasts);
