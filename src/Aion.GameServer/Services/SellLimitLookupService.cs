namespace Aion.GameServer.Services;

public sealed record SellLimitEntry(int MinLevel, int MaxLevel, long BaseLimit);

public sealed record SellLimitLookupPlan(
	int PlayerLevel,
	long? BaseLimit,
	bool Found,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class SellLimitLookupService
{
	// Java parity: model/SellLimit enum entries.
	public static readonly IReadOnlyList<SellLimitEntry> Entries =
	[
		new(1,  30,  5_300_047L), // LIMIT_1_30
		new(31, 40,  7_100_047L), // LIMIT_31_40
		new(41, 55, 12_050_047L), // LIMIT_41_55
		new(56, 60, 14_600_047L), // LIMIT_56_60
		new(61, 65, 17_150_047L), // LIMIT_61_65
	];

	public static SellLimitLookupPlan CreatePlan(int playerMaxLevel)
	{
		// Java parity: model/SellLimit.getSellLimit(Player).
		// Finds the matching level-range entry. Java throws on no match; C# returns not-found.
		foreach (var entry in Entries)
		{
			if (entry.MinLevel <= playerMaxLevel && entry.MaxLevel >= playerMaxLevel)
			{
				return new SellLimitLookupPlan(
					playerMaxLevel, entry.BaseLimit, Found: true,
					$"SellLimit.getSellLimit -> level {playerMaxLevel} in [{entry.MinLevel}-{entry.MaxLevel}] -> baseLimit={entry.BaseLimit} [before SELL_LIMIT rate]"
				);
			}
		}

		return new SellLimitLookupPlan(
			playerMaxLevel, BaseLimit: null, Found: false,
			$"SellLimit.getSellLimit -> level {playerMaxLevel} not in any range -> NoSuchElementException in Java"
		);
	}
}
