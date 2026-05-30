namespace Aion.GameServer.Services;

public enum CriticalType { Physical, Magical }

public sealed record CriticalRatePlan(
	CriticalType CriticalType,
	float AttackerCritical,
	float DefenderCriticalResist,
	int CriticalProbPercent,
	float BaseCriticalRate,
	float AdjustedCriticalRate,
	float EffectiveCriticalRate,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class CriticalRatePlanService
{
	// Java parity: utils/stats/StatCapUtil.differenceLimitFor - PHYSICAL_CRITICAL and MAGICAL_CRITICAL both = 500.
	public const int CriticalDifferenceLimit = 500;

	public static CriticalRatePlan CreatePlan(
		CriticalType criticalType,
		float attackerCritical,
		float defenderCriticalResist,
		int criticalProbPercent)
	{
		// Java parity: StatFunctions.checkIsPhysicalCriticalHit and calculateMagicalCriticalRate.
		// base = attacker critical - defender critical resist
		var baseCriticalRate = attackerCritical - defenderCriticalResist;

		// criticalProb adjustment
		float adjusted;
		if (criticalType == CriticalType.Magical)
		{
			// Magical: if criticalProb != 100, min(1, critical) then * criticalProb/100
			if (criticalProbPercent != 100)
			{
				if (baseCriticalRate <= 0)
					adjusted = 1 * criticalProbPercent / 100f;
				else
					adjusted = baseCriticalRate * criticalProbPercent / 100f;
			}
			else
				adjusted = baseCriticalRate;
		}
		else
		{
			// Physical: if criticalProb != 100 AND criticalRate > 0 → apply; else unchanged
			if (criticalProbPercent != 100 && baseCriticalRate > 0)
				adjusted = baseCriticalRate * criticalProbPercent / 100f;
			else
				adjusted = baseCriticalRate;
		}

		// Cap at CriticalDifferenceLimit = 500
		var effective = Math.Min(CriticalDifferenceLimit, adjusted);

		return new CriticalRatePlan(
			criticalType, attackerCritical, defenderCriticalResist, criticalProbPercent,
			baseCriticalRate, adjusted, effective,
			$"StatFunctions.{(criticalType == CriticalType.Physical ? "checkIsPhysicalCriticalHit" : "calculateMagicalCriticalRate")} -> base={baseCriticalRate} adjusted={adjusted} effective={effective}/1000"
		);
	}
}
