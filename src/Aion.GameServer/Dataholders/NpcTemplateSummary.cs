namespace Aion.GameServer.Dataholders;

// StaticData-internal projection retained as the input to ProcessGlobalDropRules
// (global-drop gd_npc_names expansion reads NpcTemplateSummary.Name/TemplateId).
// The dead NpcTemplateTable holder + model/packet consumers were retired; the
// faithful NPC-info path is DataManager.NPC_DATA + SM_NPC_INFO.
public sealed record NpcTemplateSummary(
	int TemplateId,
	string Name,
	int NameId,
	int Level,
	string Rank,
	string Rating,
	string Race,
	string Tribe,
	string Type,
	int TitleId = 0,
	float Height = 0,
	int AttackSpeed = 0,
	int MaxHp = 0,
	float RunSpeed = 0,
	float BoundRadius = 0,
	int TalkDistance = 2,
	IReadOnlyList<int>? FunctionDialogIds = null,
	int State = 0,
	string AiName = "",
	bool CanTalkInvisible = true,
	bool HasTalkInfo = false,
	bool IsDialogNpc = false,
	string GroupDrop = "",
	string AbyssType = "NONE",
	KiskStatsSummary? KiskStats = null,
	NpcSubDialogType? SubDialogType = null,
	int SubDialogValue = 0)
{
	public bool CanInteract => HasTalkInfo;

	public bool SupportsDialogAction(int dialogActionId)
	{
		// Java parity: model/templates/npc/NpcTemplate.supportsAction checks TalkInfo.funcDialogIds.
		return FunctionDialogIds?.Contains(dialogActionId) == true;
	}
}

public sealed record KiskStatsSummary(
	int UseMask = 4,
	int MaxMembers = 6,
	int MaxResurrects = 18);

public enum NpcSubDialogType
{
	FortCapture,
	SkillId,
	ItemId,
	Return,
	PcBang,
	PaidUser,
	Newbie,
	AbyssRank,
	AbyssRanking,
	Level,
	LevelLow,
	LevelHigh,
	LegionDominionNpc,
	TargetLegionDominion,
	Pack3,
	Pack4,
	Cash,
}
