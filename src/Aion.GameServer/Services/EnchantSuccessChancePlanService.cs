namespace Aion.GameServer.Services;

public sealed record EnchantSuccessChancePlan(
	bool IsAmplified,
	float BaseChance,
	int StoneToItemLevelDiff,
	int StoneToItemQualityDiff,
	int CurrentEnchantLevel,
	float ChanceAfterAdjustments,
	float ChanceAfterLevelModifier,
	float ChanceAfterCap,
	float SupplementChance,
	int SupplementUseCount,
	float FinalChance,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class EnchantSuccessChancePlanService
{
	// Java parity: services/EnchantService constants.
	public const float MaxChanceWithoutSupplement = 80f;
	public const float MaxChanceWithSupplement = 95f;
	public const int MinEnchantLevelForDoubleSupplements = 10;

	public static EnchantSuccessChancePlan CreatePlan(
		bool isAmplified,
		float baseChance,
		int stoneToItemLevelDiff,
		int stoneToItemQualityDiff,
		int currentEnchantLevel,
		float supplementChance,
		int baseSupplementCount)
	{
		// Java parity: services/EnchantService.enchantItem success chance calculation.
		if (isAmplified)
		{
			return new EnchantSuccessChancePlan(
				IsAmplified: true,
				BaseChance: baseChance,
				StoneToItemLevelDiff: 0,
				StoneToItemQualityDiff: 0,
				CurrentEnchantLevel: currentEnchantLevel,
				ChanceAfterAdjustments: baseChance,
				ChanceAfterLevelModifier: baseChance,
				ChanceAfterCap: baseChance,
				SupplementChance: 0,
				SupplementUseCount: 0,
				FinalChance: baseChance,
				$"EnchantService.enchantItem -> isAmplified=true -> baseChance={baseChance}"
			);
		}

		var chance = baseChance;

		// Level and quality adjustments
		chance += stoneToItemLevelDiff;
		chance += stoneToItemQualityDiff * 5;
		var afterAdjust = chance;

		// Enchant level modifier
		if (currentEnchantLevel == 0)
			chance *= 1.2f;
		else if (currentEnchantLevel < 5)
			chance *= 1.1f;
		else if (currentEnchantLevel >= 10)
			chance *= 0.9f;
		var afterLevelMod = chance;

		// Cap without supplement
		if (chance >= MaxChanceWithoutSupplement)
			chance = MaxChanceWithoutSupplement;
		var afterCap = chance;

		// Supplement boost
		if (supplementChance > 0)
			chance += supplementChance;

		// Supplement count doubles at +10
		var supplementCount = baseSupplementCount;
		if (currentEnchantLevel >= MinEnchantLevelForDoubleSupplements)
			supplementCount *= 2;

		// Cap with supplement
		if (supplementChance > 0 && chance >= MaxChanceWithSupplement)
			chance = MaxChanceWithSupplement;

		return new EnchantSuccessChancePlan(
			IsAmplified: false,
			BaseChance: baseChance,
			stoneToItemLevelDiff, stoneToItemQualityDiff,
			currentEnchantLevel,
			afterAdjust, afterLevelMod, afterCap,
			supplementChance, supplementCount,
			FinalChance: chance,
			$"EnchantService.enchantItem -> base={baseChance} adj={afterAdjust} levelMod={afterLevelMod} cap={afterCap} supp={supplementChance} final={chance}%"
		);
	}
}
