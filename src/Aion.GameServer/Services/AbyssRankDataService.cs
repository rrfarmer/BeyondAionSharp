using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: utils/stats/AbyssRankEnum — static rank data and L10n lookup.
/// </summary>
public sealed record AbyssRankData(
	int Id,
	int Ordinal,
	int PointsGained,
	int PointsLost,
	int RequiredAp,
	int RequiredGp)
{
	// Java parity: AbyssRankEnum.getRankL10n(Race).
	// ELYOS: l10n(901215 + ordinal); ASMODIANS: l10n(901233 + ordinal).
	public string? GetRankL10n(string race)
	{
		var baseId = string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase) ? 901215 : 901233;
		return ChatUtil.L10n(baseId + Ordinal);
	}
}

public static class AbyssRankDataService
{
	// Java parity: utils/stats/AbyssRankEnum entries.
	// (id, pointsGained, pointsLost, requiredAP, requiredGP)
	public static readonly IReadOnlyList<AbyssRankData> AllRanks =
	[
		new(1,  0, 300,  90,      0,  0),  // GRADE9_SOLDIER
		new(2,  1, 345, 103,   1200,  0),  // GRADE8_SOLDIER
		new(3,  2, 396, 118,   4220,  0),  // GRADE7_SOLDIER
		new(4,  3, 455, 136,  10990,  0),  // GRADE6_SOLDIER
		new(5,  4, 523, 156,  23500,  0),  // GRADE5_SOLDIER
		new(6,  5, 601, 180,  42780,  0),  // GRADE4_SOLDIER
		new(7,  6, 721, 216,  69700,  0),  // GRADE3_SOLDIER
		new(8,  7, 865, 259, 105600,  0),  // GRADE2_SOLDIER
		new(9,  8, 1038, 311, 150800, 0),  // GRADE1_SOLDIER
		new(10, 9, 1557, 467,      0, 1244), // STAR1_OFFICER
		new(11, 10, 1868, 560,    0, 1368), // STAR2_OFFICER
		new(12, 11, 2148, 644,    0, 1915), // STAR3_OFFICER
		new(13, 12, 2470, 741,    0, 3064), // STAR4_OFFICER
		new(14, 13, 3705, 1482,   0, 5210), // STAR5_OFFICER
		new(15, 14, 4075, 1630,   0, 8335), // GENERAL
		new(16, 15, 4482, 1792,   0, 10002), // GREAT_GENERAL
		new(17, 16, 4930, 1972,   0, 11503), // COMMANDER
		new(18, 17, 5916, 2366,   0, 12437), // SUPREME_COMMANDER
	];

	public static AbyssRankData? GetById(int rankId)
		=> AllRanks.FirstOrDefault(r => r.Id == rankId);

	public static string? GetRankL10n(string race, int rankId)
	{
		// Java parity: AbyssRankEnum.getRankL10n(Race, int).
		var rank = GetById(rankId);
		return rank?.GetRankL10n(race);
	}
}
