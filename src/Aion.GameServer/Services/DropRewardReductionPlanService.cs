namespace Aion.GameServer.Services;

public sealed record DropRewardReductionPlan(
	int NpcLevel,
	int HighestPlayerLevel,
	int LevelDifference,
	int DropRewardPercent,
	float? ReductionRate,
	bool HasReduction,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class DropRewardReductionPlanService
{
	// Java parity: utils/stats/DropRewardEnum level-difference lookup table.
	public static int DropRewardFrom(int levelDifference)
	{
		// Java: if levelDifference <= -10 -> 0; if >= -5 -> 100; else lookup.
		if (levelDifference <= -10) return 0;
		if (levelDifference >= -5) return 100;
		return levelDifference switch
		{
			-9 => 40,
			-8 => 60,
			-7 => 70,
			-6 => 80,
			_ => 100,
		};
	}

	public static DropRewardReductionPlan CreatePlan(int npcLevel, int highestPlayerLevel)
	{
		// Java parity: services/drop/DropRegistrationService.getReductionDropRate.
		var levelDiff = npcLevel - highestPlayerLevel;
		var dropChance = DropRewardFrom(levelDiff);
		var hasReduction = dropChance < 100;
		var reductionRate = hasReduction ? (float?)dropChance / 100f : null;

		return new DropRewardReductionPlan(
			npcLevel, highestPlayerLevel, levelDiff, dropChance, reductionRate, hasReduction,
			hasReduction
				? $"DropRegistrationService.getReductionDropRate -> npcLevel({npcLevel})-highestLevel({highestPlayerLevel})={levelDiff} -> {dropChance}% -> rate={reductionRate:F2}"
				: $"DropRegistrationService.getReductionDropRate -> npcLevel({npcLevel})-highestLevel({highestPlayerLevel})={levelDiff} -> 100% -> null (no reduction)"
		);
	}
}
