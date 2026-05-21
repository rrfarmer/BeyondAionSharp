using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class TemperingTable
{
	private const int PlumeHpBoost = 150;
	private const int PlumeMagicalBoost = 20;
	private const int PlumePhysicalAttack = 4;

	private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyList<TemperingStatSummary>>> _templates;

	public TemperingTable(IEnumerable<TemperingGroupSummary> groups)
	{
		var templates = new Dictionary<string, IReadOnlyDictionary<int, IReadOnlyList<TemperingStatSummary>>>(StringComparer.Ordinal);
		foreach (var group in groups)
		{
			var levels = new Dictionary<int, IReadOnlyList<TemperingStatSummary>>();
			foreach (var level in group.Levels)
				levels[level.Level] = level.Stats;
			templates[group.ItemGroup] = new ReadOnlyDictionary<int, IReadOnlyList<TemperingStatSummary>>(levels);
		}

		_templates = new ReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyList<TemperingStatSummary>>>(templates);
	}

	public int Count => _templates.Count;

	public IReadOnlyList<ItemStatModifier> GetModifiers(ItemTemplateSummary itemTemplate, int temperingLevel, int randomPlumeBonus)
	{
		// Java parity: model/enchants/TemperingEffect.apply.
		if (temperingLevel <= 0)
			return Array.Empty<ItemStatModifier>();

		if (itemTemplate.IsPlume)
			return GetPlumeModifiers(itemTemplate, temperingLevel, randomPlumeBonus);

		var templateName = string.IsNullOrEmpty(itemTemplate.TemperingName) ? itemTemplate.ItemGroup : itemTemplate.TemperingName;
		if (!_templates.TryGetValue(templateName, out var levels)
			|| !levels.TryGetValue(temperingLevel, out var stats)
			|| stats.Count == 0)
		{
			return Array.Empty<ItemStatModifier>();
		}

		return stats
			.Select(stat => new ItemStatModifier("add", stat.Name, stat.Value, Bonus: false))
			.ToArray();
	}

	private static IReadOnlyList<ItemStatModifier> GetPlumeModifiers(ItemTemplateSummary itemTemplate, int temperingLevel, int randomPlumeBonus)
	{
		// Java parity: model/enchants/TemperingEffect.addPlumeStatFunctions + model/stats/container/PlumStatEnum.
		var isPhysical = string.Equals(itemTemplate.TemperingName, "TSHIRT_PHYSICAL", StringComparison.Ordinal);
		var primaryStat = isPhysical ? "PHYSICAL_ATTACK" : "BOOST_MAGICAL_SKILL";
		var primaryBoost = isPhysical ? PlumePhysicalAttack : PlumeMagicalBoost;
		return
		[
			new ItemStatModifier("add", primaryStat, randomPlumeBonus + primaryBoost * temperingLevel, Bonus: true),
			new ItemStatModifier("add", "MAXHP", PlumeHpBoost * temperingLevel, Bonus: true),
		];
	}
}

public sealed record TemperingGroupSummary(
	string ItemGroup,
	IReadOnlyList<TemperingLevelSummary> Levels);

public sealed record TemperingLevelSummary(
	int Level,
	IReadOnlyList<TemperingStatSummary> Stats);

public sealed record TemperingStatSummary(
	string Name,
	int Value);
