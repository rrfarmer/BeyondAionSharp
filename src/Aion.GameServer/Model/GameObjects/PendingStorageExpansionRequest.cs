using Aion.GameServer.Services;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingStorageExpansionRequest(
	int NpcObjectId,
	int NpcTemplateId,
	InventoryExpansionStorage Storage,
	int TargetNpcExpands,
	int Price,
	int QuestionId);
