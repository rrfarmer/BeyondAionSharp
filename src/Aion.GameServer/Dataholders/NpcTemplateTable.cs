using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class NpcTemplateTable
{
	private readonly IReadOnlyDictionary<int, NpcTemplateSummary> _templatesById;

	public NpcTemplateTable(IReadOnlyList<NpcTemplateSummary> templates)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, NpcTemplateSummary>(
			templates.ToDictionary(template => template.TemplateId));
	}

	public IReadOnlyList<NpcTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public NpcTemplateSummary? GetNpcTemplate(int npcId)
	{
		return _templatesById.GetValueOrDefault(npcId);
	}
}

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
	int State = 0)
{
	public bool SupportsDialogAction(int dialogActionId)
	{
		// Java parity: model/templates/npc/NpcTemplate.supportsAction checks TalkInfo.funcDialogIds.
		return FunctionDialogIds?.Contains(dialogActionId) == true;
	}
}
