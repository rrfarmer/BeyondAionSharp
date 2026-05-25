namespace Aion.GameServer.Dataholders;

public sealed class ItemRandomBonusTable
{
	private readonly Dictionary<string, Dictionary<int, ItemRandomBonusSummary>> _bonuses;

	public ItemRandomBonusTable(IEnumerable<ItemRandomBonusSummary> bonuses)
	{
		_bonuses = bonuses
			.GroupBy(bonus => bonus.Type, StringComparer.Ordinal)
			.ToDictionary(
				group => group.Key,
				group => group.ToDictionary(bonus => bonus.SetId),
				StringComparer.Ordinal);
	}

	public int Count => _bonuses.Values.Sum(group => group.Count);

	public bool AreBonusSetsEqual(string type, int statBonusSetId1, int statBonusSetId2)
	{
		// Java parity: dataholders/ItemRandomBonusData.areBonusSetsEqual uses group-count equality
		// for different set ids because some purified-item sets have equivalent modifier content.
		if (statBonusSetId1 == statBonusSetId2)
			return true;

		var set1 = GetBonusSet(type, statBonusSetId1);
		var set2 = GetBonusSet(type, statBonusSetId2);
		if (set1 == null || set2 == null)
			return set1 == set2;

		return set1.ModifierGroups.Count == set2.ModifierGroups.Count;
	}

	public IReadOnlyList<ItemStatModifier> GetModifiers(string type, int statBonusSetId, int statBonusId)
	{
		// Java parity: dataholders/ItemRandomBonusData.getTemplate(StatBonusType, setId, bonusId).
		if (statBonusSetId == 0 || statBonusId <= 0)
			return Array.Empty<ItemStatModifier>();

		if (!_bonuses.TryGetValue(type, out var typedBonuses)
			|| !typedBonuses.TryGetValue(statBonusSetId, out var set)
			|| statBonusId > set.ModifierGroups.Count)
		{
			return Array.Empty<ItemStatModifier>();
		}

		return set.ModifierGroups[statBonusId - 1];
	}

	public int SelectRandomBonusNumber(string type, int statBonusSetId, Func<double>? random = null)
	{
		// Java parity: dataholders/ItemRandomBonusData.selectRandomBonusNumber.
		if (statBonusSetId == 0
			|| !_bonuses.TryGetValue(type, out var typedBonuses)
			|| !typedBonuses.TryGetValue(statBonusSetId, out var set)
			|| set.ModifierGroups.Count == 0)
		{
			return 0;
		}

		var chances = set.Chances ?? Enumerable.Repeat(1d, set.ModifierGroups.Count).ToArray();
		var totalChance = 0d;
		for (var i = 0; i < set.ModifierGroups.Count; i++)
		{
			var chance = i < chances.Count ? chances[i] : 0d;
			if (chance > 0)
				totalChance += chance;
		}

		if (totalChance <= 0)
			return 0;

		var roll = Math.Clamp(random?.Invoke() ?? Random.Shared.NextDouble(), 0d, 0.999999999999d) * totalChance;
		var cumulativeChance = 0d;
		var lastPositiveGroup = 0;
		for (var i = 0; i < set.ModifierGroups.Count; i++)
		{
			var chance = i < chances.Count ? chances[i] : 0d;
			if (chance <= 0)
				continue;

			lastPositiveGroup = i + 1;
			cumulativeChance += chance;
			if (cumulativeChance >= roll)
				return i + 1;
		}

		return lastPositiveGroup;
	}

	private ItemRandomBonusSummary? GetBonusSet(string type, int statBonusSetId)
	{
		return _bonuses.TryGetValue(type, out var typedBonuses)
			&& typedBonuses.TryGetValue(statBonusSetId, out var set)
				? set
				: null;
	}
}

public sealed record ItemRandomBonusSummary(
	string Type,
	int SetId,
	IReadOnlyList<IReadOnlyList<ItemStatModifier>> ModifierGroups,
	IReadOnlyList<double>? Chances = null);
