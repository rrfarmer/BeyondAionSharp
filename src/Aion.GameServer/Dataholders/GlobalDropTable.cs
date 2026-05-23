namespace Aion.GameServer.Dataholders;

public sealed class GlobalDropTable
{
	public GlobalDropTable(IReadOnlyList<GlobalDropRuleSummary> rules)
	{
		Rules = rules;
	}

	public IReadOnlyList<GlobalDropRuleSummary> Rules { get; }

	public int Count => Rules.Count;
}

public sealed record GlobalDropRuleSummary(
	string RuleName,
	float Chance,
	bool DynamicChance,
	int MinDiff,
	int MaxDiff,
	string RestrictionRace,
	bool UseLevelBasedChanceReduction,
	int MemberLimit,
	int MaxDropRule,
	IReadOnlyList<GlobalDropItemSummary> Items,
	IReadOnlySet<string> WorldTypes,
	IReadOnlySet<string> Races,
	IReadOnlySet<string> Ratings,
	IReadOnlySet<int> MapIds,
	IReadOnlySet<string> Tribes,
	IReadOnlySet<int> NpcIds,
	IReadOnlyList<GlobalDropNpcNameSummary> NpcNames,
	IReadOnlySet<string> NpcGroups,
	IReadOnlySet<int> ExcludedNpcIds,
	IReadOnlySet<string> Zones)
{
	public bool HasNpcRestriction => NpcIds.Count > 0 || NpcNames.Count > 0;
}

public sealed record GlobalDropItemSummary(
	int ItemId,
	int MinCount,
	int MaxCount,
	float Chance);

public sealed record GlobalDropNpcNameSummary(string Function, string Value);

public sealed record GlobalNpcExclusionTable(
	IReadOnlySet<int> NpcIds,
	IReadOnlySet<string> NpcNames,
	IReadOnlySet<string> NpcTemplateTypes,
	IReadOnlySet<string> NpcTribes,
	IReadOnlySet<string> NpcAbyssTypes)
{
	public bool IsEmpty => NpcIds.Count == 0
		&& NpcNames.Count == 0
		&& NpcTemplateTypes.Count == 0
		&& NpcTribes.Count == 0
		&& NpcAbyssTypes.Count == 0;

	public static GlobalNpcExclusionTable Empty { get; } = new(
		new HashSet<int>(),
		new HashSet<string>(StringComparer.OrdinalIgnoreCase),
		new HashSet<string>(StringComparer.OrdinalIgnoreCase),
		new HashSet<string>(StringComparer.OrdinalIgnoreCase),
		new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
