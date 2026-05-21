using System.Collections.ObjectModel;

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

	public IReadOnlyList<ItemStatModifier> GetModifiers(ItemTemplateSummary itemTemplate, int enchantLevel, long equipmentSlot)
	{
		// Java parity: dataholders/EnchantData.getTemplates + services/EnchantService.applyEnchantEffect + model/enchants/EnchantEffect.
		if (enchantLevel <= 0)
			return Array.Empty<ItemStatModifier>();

		var templateName = string.IsNullOrEmpty(itemTemplate.EnchantName) ? itemTemplate.ItemGroup : itemTemplate.EnchantName;
		if (!_templates.TryGetValue(templateName, out var levels) || levels.Count == 0)
			return Array.Empty<ItemStatModifier>();

		var maxTemplateLevel = levels.Keys.Max();
		var stats = ResolveStats(levels, enchantLevel, maxTemplateLevel);
		if (stats.Count == 0)
			return Array.Empty<ItemStatModifier>();

		return stats
			.Where(stat => IsApplicableToSlot(stat.Name, equipmentSlot))
			.Select(stat => new ItemStatModifier("add", stat.Name, stat.Value, Bonus: false))
			.ToArray();
	}

	private static IReadOnlyList<EnchantStatSummary> ResolveStats(
		IReadOnlyDictionary<int, IReadOnlyList<EnchantStatSummary>> levels,
		int enchantLevel,
		int maxTemplateLevel)
	{
		if (enchantLevel > maxTemplateLevel && maxTemplateLevel < 21)
			return Array.Empty<EnchantStatSummary>();

		if (enchantLevel < maxTemplateLevel)
			return levels.GetValueOrDefault(enchantLevel) ?? Array.Empty<EnchantStatSummary>();

		var stats = new List<EnchantStatSummary>();
		if (levels.TryGetValue(maxTemplateLevel - 1, out var maximumStats))
			stats.AddRange(maximumStats);
		if (levels.TryGetValue(maxTemplateLevel, out var limitlessStats))
		{
			for (var i = 0; i <= enchantLevel - maxTemplateLevel; i++)
				stats.AddRange(limitlessStats);
		}

		return stats;
	}

	private static bool IsApplicableToSlot(string statName, long equipmentSlot)
	{
		return statName != "BOOST_MAGICAL_SKILL" || equipmentSlot is 1 or 3;
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
