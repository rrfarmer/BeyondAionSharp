namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerSummonKnownObject(
	int ObjectId,
	PlayerSummonKnownObjectKind Kind,
	int CreatorObjectId = 0,
	PlayerSummonKnownNpcTemplateType NpcTemplateType = PlayerSummonKnownNpcTemplateType.None);
