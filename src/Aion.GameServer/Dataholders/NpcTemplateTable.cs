using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class NpcTemplateTable
{
	private readonly IReadOnlyDictionary<int, NpcTemplateSummary> _templatesById;
	private readonly HashSet<int> _functionDialogIds;
	private readonly IReadOnlyList<NpcTemplateUnknownDialogActionSummary> _unknownFunctionDialogIds;

	public NpcTemplateTable(IReadOnlyList<NpcTemplateSummary> templates)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, NpcTemplateSummary>(
			templates.ToDictionary(template => template.TemplateId));
		// Java parity: dataholders/NpcData.init aggregates all TalkInfo.funcDialogIds
		// into functionDialogIds for NpcData.isFunctionDialog.
		_functionDialogIds = templates
			.SelectMany(template => template.FunctionDialogIds ?? [])
			.ToHashSet();
		_unknownFunctionDialogIds = templates
			.SelectMany(
				template => (template.FunctionDialogIds ?? [])
					.Where(dialogActionId => !Services.DialogActionRegistry.NameOf(dialogActionId).IsKnown)
					.Select(dialogActionId => new NpcTemplateUnknownDialogActionSummary(template.TemplateId, dialogActionId)))
			.ToArray();
	}

	public IReadOnlyList<NpcTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public NpcTemplateSummary? GetNpcTemplate(int npcId)
	{
		return _templatesById.GetValueOrDefault(npcId);
	}

	public bool IsFunctionDialog(int dialogActionId)
	{
		return _functionDialogIds.Contains(dialogActionId);
	}

	public IReadOnlyList<NpcTemplateUnknownDialogActionSummary> GetUnknownFunctionDialogIds()
	{
		// Java parity: dataholders/NpcData.init logs "Unknown dialog action {id} for Npc {templateId}".
		return _unknownFunctionDialogIds;
	}
}

public sealed record NpcTemplateUnknownDialogActionSummary(int NpcId, int DialogActionId);

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
