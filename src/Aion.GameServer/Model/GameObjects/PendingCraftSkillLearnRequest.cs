namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingCraftSkillLearnRequest(
	int NpcObjectId,
	int NpcTemplateId,
	int SkillId,
	int CurrentSkillLevel,
	int TargetSkillLevel,
	int Price,
	string ProfessionName,
	int QuestionId);
