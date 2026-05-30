namespace Aion.GameServer.Services;

public sealed record BreakItemStoneSelectionPlan(
	int ItemEffectiveLevel,
	bool IsWeapon,
	int RandomBonus,
	int FinalEffectiveLevel,
	int ResultStoneId,
	string ResultStoneName,
	int MinRewardCount,
	int MaxRewardCount,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class BreakItemFormulaService
{
	// Java parity: services/EnchantService.calculateEffectiveLevel quality bonuses.
	public static int GetQualityLevelBonus(string itemQuality)
	{
		return itemQuality.ToUpperInvariant() switch
		{
			"COMMON" => 5,  // same as RARE in Java
			"RARE" => 5,
			"LEGEND" => 10,
			"UNIQUE" => 15,
			"EPIC" => 20,
			"MYTHIC" => 25,
			_ => 0,
		};
	}

	public static int CalculateEffectiveLevel(string itemQuality, int itemLevel)
	{
		// Java parity: services/EnchantService.calculateEffectiveLevel(ItemQuality, int).
		return itemLevel + GetQualityLevelBonus(itemQuality);
	}

	// Java parity: EnchantmentStone thresholds for stone selection.
	// ALPHA(20,RARE)→25, BETA(40,LEGEND)→50, GAMMA(55,UNIQUE)→70, DELTA(60,EPIC)→80, EPSILON(65,MYTHIC)→90
	public const int AlphaThreshold = 25;   // 20+5
	public const int BetaThreshold = 50;    // 40+10
	public const int GammaThreshold = 70;   // 55+15
	public const int DeltaThreshold = 80;   // 60+20
	public const int EpsilonThreshold = 90; // 65+25

	public static BreakItemStoneSelectionPlan CreatePlan(
		string itemQuality,
		int itemLevel,
		bool isWeapon,
		int randomBonus)
	{
		// Java parity: services/EnchantService.breakItem stone selection.
		// randomBonus represents Rnd.get(0, 10) [live]; weapons get additional +5.
		var baseEffective = CalculateEffectiveLevel(itemQuality, itemLevel);
		var finalEffective = baseEffective + randomBonus + (isWeapon ? 5 : 0);

		int stoneId;
		string stoneName;
		if (finalEffective >= EpsilonThreshold) { stoneId = 166000195; stoneName = "EPSILON"; }
		else if (finalEffective >= DeltaThreshold) { stoneId = 166000194; stoneName = "DELTA"; }
		else if (finalEffective >= GammaThreshold) { stoneId = 166000193; stoneName = "GAMMA"; }
		else if (finalEffective >= BetaThreshold) { stoneId = 166000192; stoneName = "BETA"; }
		else { stoneId = 166000191; stoneName = "ALPHA"; }

		// Java: weapon → Rnd.get(2,5), armor → Rnd.get(1,3)
		var (minCount, maxCount) = isWeapon ? (2, 5) : (1, 3);

		return new BreakItemStoneSelectionPlan(
			baseEffective, isWeapon, randomBonus, finalEffective,
			stoneId, stoneName, minCount, maxCount,
			$"EnchantService.breakItem -> effectiveLevel={baseEffective}+rnd({randomBonus}){(isWeapon ? "+5(weapon)" : "")}={finalEffective} → {stoneName}({stoneId}) count=[{minCount},{maxCount}]"
		);
	}
}
