namespace Aion.GameServer.Services;

public sealed record ManastoneSocketChancePlan(
	int TargetItemLevel,
	int StoneLevel,
	int SlotLevel,
	bool StoneLevelTooHigh,
	int CurrentStoneCount,
	bool IsRareOrHigher,
	float BaseChance,
	float SocketDiff,
	float ChanceAfterDiffBonus,
	float SupplementChance,
	float FinalChance,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ManastoneSocketChancePlanService
{
	public static int CalculateSlotLevel(int targetItemLevel)
	{
		// Java parity: services/EnchantService.socketManastone.
		// slotLevel = (int)(10 * Math.ceil((targetItemLevel + 10) / 10d))
		return (int)(10 * Math.Ceiling((targetItemLevel + 10) / 10.0));
	}

	public static ManastoneSocketChancePlan CreatePlan(
		int targetItemLevel,
		int stoneLevel,
		int currentStoneCount,
		bool isRareOrHigherQuality,
		float baseChance,
		float supplementChance)
	{
		// Java parity: services/EnchantService.socketManastone success chance calculation.
		var slotLevel = CalculateSlotLevel(targetItemLevel);

		if (stoneLevel > slotLevel)
		{
			return new ManastoneSocketChancePlan(
				targetItemLevel, stoneLevel, slotLevel, StoneLevelTooHigh: true,
				currentStoneCount, isRareOrHigherQuality, baseChance,
				SocketDiff: 0, ChanceAfterDiffBonus: 0, SupplementChance: 0, FinalChance: 0,
				$"EnchantService.socketManastone -> stoneLevel({stoneLevel}) > slotLevel({slotLevel}) -> return false"
			);
		}

		// Rare+ quality reduces base chance by 20%
		var chance = isRareOrHigherQuality ? baseChance * 0.8f : baseChance;

		// Difficulty increases with more stones already socketed
		var socketDiff = currentStoneCount * 1.25f + 1.75f;

		// Level advantage bonus
		chance += (slotLevel - stoneLevel) / socketDiff;
		var afterDiff = chance;

		// Supplement boost
		if (supplementChance > 0)
			chance += supplementChance;

		return new ManastoneSocketChancePlan(
			targetItemLevel, stoneLevel, slotLevel, StoneLevelTooHigh: false,
			currentStoneCount, isRareOrHigherQuality, baseChance,
			socketDiff, afterDiff, supplementChance,
			FinalChance: chance,
			$"EnchantService.socketManastone -> slotLevel={slotLevel} diff={socketDiff:F2} base={baseChance} afterAdj={afterDiff:F2} final={chance:F2}%"
		);
	}
}
