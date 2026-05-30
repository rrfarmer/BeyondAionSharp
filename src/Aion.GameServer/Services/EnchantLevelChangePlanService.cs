namespace Aion.GameServer.Services;

public sealed record EnchantLevelChangePlan(
	bool Success,
	int CurrentEnchant,
	int MaxEnchant,
	bool IsAmplified,
	int AddLevel,
	int NewEnchantLevel,
	bool WasDowngraded,
	bool WasAmplifiedReset,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class EnchantLevelChangePlanService
{
	// Java parity: services/EnchantService.enchantItemAct crit thresholds.
	public const float CritLevel3Threshold = 5f;   // < 5 → +3
	public const float CritLevel2Threshold = 10f;  // < 10 → +2
	public const int MaxEnchantForCrit = 20;

	public static int GetAddLevel(float randomChance, int currentEnchant)
	{
		// Java parity: services/EnchantService.enchantItemAct crit modifier.
		if (currentEnchant >= MaxEnchantForCrit)
			return 1;
		if (randomChance < CritLevel3Threshold)
			return 3;
		if (randomChance < CritLevel2Threshold)
			return 2;
		return 1;
	}

	public static EnchantLevelChangePlan CreatePlan(
		bool success,
		int currentEnchant,
		int maxEnchant,
		bool isAmplified,
		int addLevel)
	{
		// Java parity: services/EnchantService.enchantItemAct enchant level outcomes.
		int newLevel;
		bool downgraded = false;
		bool amplifiedReset = false;

		if (success)
		{
			// Success: cap at maxEnchant (unless amplified)
			if (!isAmplified && currentEnchant + addLevel > maxEnchant)
				newLevel = maxEnchant;
			else
				newLevel = currentEnchant + addLevel;
		}
		else
		{
			// Failure
			if (isAmplified)
			{
				newLevel = maxEnchant;
				amplifiedReset = true;
			}
			else if (currentEnchant > 10 && maxEnchant > 10)
			{
				newLevel = 10;
				downgraded = true;
			}
			else if (currentEnchant > 0)
			{
				newLevel = currentEnchant - 1;
				downgraded = true;
			}
			else
			{
				newLevel = 0;
			}
		}

		// Amplified items get max 255 cap
		var effectiveMax = isAmplified ? 255 : maxEnchant;
		newLevel = Math.Min(newLevel, effectiveMax);

		return new EnchantLevelChangePlan(
			success, currentEnchant, maxEnchant, isAmplified, addLevel,
			newLevel, downgraded, amplifiedReset,
			success
				? $"EnchantService.enchantItemAct -> SUCCESS +{addLevel} {currentEnchant}→{newLevel} (cap={effectiveMax})"
				: $"EnchantService.enchantItemAct -> FAILURE {currentEnchant}→{newLevel}{(amplifiedReset ? " amplifiedReset" : downgraded ? " downgraded" : "")}"
		);
	}
}
