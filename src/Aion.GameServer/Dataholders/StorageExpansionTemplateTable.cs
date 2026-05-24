using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class StorageExpansionTemplateTable
{
	private readonly IReadOnlyDictionary<int, StorageExpansionTemplateSummary> _templatesByNpcId;

	public StorageExpansionTemplateTable(IReadOnlyList<StorageExpansionTemplateSummary> templates)
	{
		Templates = templates;
		_templatesByNpcId = new ReadOnlyDictionary<int, StorageExpansionTemplateSummary>(
			templates
				.SelectMany(template => template.NpcIds.Select(npcId => (npcId, template)))
				.ToDictionary(entry => entry.npcId, entry => entry.template));
	}

	public IReadOnlyList<StorageExpansionTemplateSummary> Templates { get; }

	public int Count => _templatesByNpcId.Count;

	public StorageExpansionTemplateSummary? GetTemplateByNpcId(int npcId)
	{
		return _templatesByNpcId.GetValueOrDefault(npcId);
	}
}

public sealed record StorageExpansionTemplateSummary(
	IReadOnlyList<int> NpcIds,
	IReadOnlyList<StorageExpansionPrice> Expansions)
{
	public int MinExpansionLevel => Expansions.Count == 0 ? 0 : Expansions.Min(expansion => expansion.Level);

	public int MaxExpansionLevel => Expansions.Count == 0 ? 0 : Expansions.Max(expansion => expansion.Level);

	public int? GetPrice(int level)
	{
		return Expansions.FirstOrDefault(expansion => expansion.Level == level)?.Price;
	}
}

public sealed record StorageExpansionPrice(int Level, int Price);
