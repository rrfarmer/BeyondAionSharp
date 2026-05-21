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
}

public sealed record ItemRandomBonusSummary(
	string Type,
	int SetId,
	IReadOnlyList<IReadOnlyList<ItemStatModifier>> ModifierGroups);
