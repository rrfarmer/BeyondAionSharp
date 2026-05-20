using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class ItemTemplateTable
{
	private readonly IReadOnlyDictionary<int, ItemTemplateSummary> _templatesById;

	public ItemTemplateTable(IReadOnlyList<ItemTemplateSummary> templates)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, ItemTemplateSummary>(
			templates.ToDictionary(template => template.TemplateId));
	}

	public IReadOnlyList<ItemTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public ItemTemplateSummary? GetItemTemplate(int itemId)
	{
		return _templatesById.GetValueOrDefault(itemId);
	}
}

public sealed record ItemTemplateSummary(
	int TemplateId,
	string Name,
	int Level,
	string ItemGroup,
	string ItemType,
	string Quality,
	string Race,
	int MaxStackCount,
	long Price,
	long ValidEquipmentSlots)
{
	public bool IsEquipment => ValidEquipmentSlots != 0;
}
