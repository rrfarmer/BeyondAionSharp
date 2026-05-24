namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerPetSkillOrder(
	int SkillId,
	int SkillLevel,
	int TargetObjectId,
	int Hate,
	bool Release);
