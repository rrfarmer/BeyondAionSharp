using System.Collections.ObjectModel;
using Aion.GameServer.Model.Enchants;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders;

public sealed class EnchantTable
{
	private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyList<EnchantStatSummary>>> _templates;

	public EnchantTable(IEnumerable<EnchantGroupSummary> groups)
	{
		var templates = new Dictionary<string, IReadOnlyDictionary<int, IReadOnlyList<EnchantStatSummary>>>(StringComparer.Ordinal);
		foreach (var group in groups)
		{
			var levels = new Dictionary<int, IReadOnlyList<EnchantStatSummary>>();
			foreach (var level in group.Levels)
				levels[level.Level] = level.Stats;
			templates[group.ItemGroup] = new ReadOnlyDictionary<int, IReadOnlyList<EnchantStatSummary>>(levels);
		}

		_templates = new ReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyList<EnchantStatSummary>>>(templates);
	}

	public int Count => _templates.Count;

	/// <summary>Java parity: dataholders/EnchantData.getTemplates(ItemTemplate).</summary>
	public Dictionary<int, List<EnchantStat>>? GetTemplates(ItemTemplate itemTemplate)
	{
		var enchantName = itemTemplate.GetEnchantName();
		var key = enchantName ?? itemTemplate.GetItemGroup().ToString();
		if (!_templates.TryGetValue(key, out var levels))
			return null;

		var result = new Dictionary<int, List<EnchantStat>>(levels.Count);
		foreach (var (level, stats) in levels)
		{
			var converted = new List<EnchantStat>(stats.Count);
			foreach (var stat in stats)
			{
				if (Enum.TryParse<StatEnum>(stat.Name, out var statEnum))
					converted.Add(new EnchantStat(statEnum, stat.Value));
			}

			result[level] = converted;
		}

		return result;
	}

}

public sealed record EnchantGroupSummary(
	string ItemGroup,
	IReadOnlyList<EnchantLevelSummary> Levels);

public sealed record EnchantLevelSummary(
	int Level,
	IReadOnlyList<EnchantStatSummary> Stats);

public sealed record EnchantStatSummary(
	string Name,
	int Value);
