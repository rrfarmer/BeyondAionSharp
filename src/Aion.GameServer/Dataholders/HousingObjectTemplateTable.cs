using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class HousingObjectTemplateTable
{
	private readonly IReadOnlyDictionary<int, HousingObjectTemplateSummary> _templatesById;

	public HousingObjectTemplateTable(IReadOnlyList<HousingObjectTemplateSummary> templates)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, HousingObjectTemplateSummary>(
			templates.ToDictionary(template => template.TemplateId));
	}

	public IReadOnlyList<HousingObjectTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public HousingObjectTemplateSummary? GetTemplate(int templateId)
	{
		return _templatesById.GetValueOrDefault(templateId);
	}
}

// Java parity: model/templates/housing/PlaceableHouseObject plus concrete getTypeId implementations.
public sealed record HousingObjectTemplateSummary(
	int TemplateId,
	byte TypeId,
	string Kind,
	string Area,
	string Location,
	string Limit,
	string Category,
	int UseDays,
	bool CanDye,
	int NpcId = 0,
	int WarehouseId = 0,
	bool OwnerOnly = false,
	int CooldownSeconds = 0,
	int DelayMilliseconds = 0,
	int UseCount = 0,
	int RequiredItemId = 0,
	int EmblemLevel = 0,
	int UseActionCheckType = 0);
