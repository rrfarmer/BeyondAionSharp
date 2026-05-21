using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class TitleTemplateTable
{
	private readonly IReadOnlyDictionary<int, TitleTemplateSummary> _templatesById;

	public TitleTemplateTable(IReadOnlyList<TitleTemplateSummary> templates)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, TitleTemplateSummary>(
			templates.ToDictionary(template => template.TitleId));
	}

	public IReadOnlyList<TitleTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public TitleTemplateSummary? GetTitleTemplate(int titleId)
	{
		return _templatesById.GetValueOrDefault(titleId);
	}
}

// Java parity: model/templates/TitleTemplate.
public sealed record TitleTemplateSummary(
	int TitleId,
	int NameId,
	string Description,
	string Race,
	IReadOnlyList<ItemStatModifier> Modifiers);
