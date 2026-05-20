namespace Aion.GameServer.Model.Account;

public sealed record CharacterCreationResult(int ResponseCode, CharacterSelectionEntry? Character = null);

public sealed record NewCharacterRecord(
	CharacterSelectionEntry Character,
	string AccountName,
	string Gender,
	string Race,
	string PlayerClass,
	DateTime CreationDate,
	IReadOnlyList<NewCharacterItem> StartingItems,
	IReadOnlyList<NewCharacterSkill> StartingSkills);

public sealed record NewCharacterItem(
	int ObjectId,
	int ItemId,
	long Count,
	bool IsEquipped,
	long EquipmentSlot,
	int ItemLocation,
	int ItemSkin);

public sealed record NewCharacterSkill(int SkillId, int SkillLevel);
