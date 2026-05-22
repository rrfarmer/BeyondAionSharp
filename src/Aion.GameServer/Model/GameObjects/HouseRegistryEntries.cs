namespace Aion.GameServer.Model.GameObjects;

// Java parity breadcrumbs: model/house/HouseRegistry inventory rows used by SM_HOUSE_REGISTRY.
public sealed record RegisteredHouseObjectSummary(
	int ObjectId,
	int TemplateId,
	int CooldownSeconds = 0,
	int ExpirationSeconds = 0,
	int? Color = null,
	byte TypeId = 0,
	byte[]? UsageData = null);

public sealed record RegisteredHouseDecorationSummary(int ObjectId, int TemplateId);
