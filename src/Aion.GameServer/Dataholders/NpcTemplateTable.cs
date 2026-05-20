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
	string Type);
