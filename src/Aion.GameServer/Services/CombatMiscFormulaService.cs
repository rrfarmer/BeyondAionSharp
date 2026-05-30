namespace Aion.GameServer.Services;

public sealed record PvpApLostPlan(
	int WinnerLevel,
	int DefeatedLevel,
	int LevelDifference,
	int BasePointsLost,
	int AdjustedPointsLost,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record FallDamagePlan(
	float FallDistance,
	float MinimumDamageDistance,
	float MaximumDamageDistance,
	float FallDamagePercent,
	int CurrentHp,
	int MaxHp,
	int FallDamage,
	bool IsLethalFall,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class CombatMiscFormulaService
{
	public static PvpApLostPlan CreatePvpApLostPlan(
		int winnerLevel, int defeatedLevel,
		int defeatedBasePointsLost)
	{
		// Java parity: utils/stats/StatFunctions.calculatePvPApLost (base formula before Rates.AP_PVP_LOST).
		var diff = winnerLevel - defeatedLevel;
		int adjusted;
		if (diff >= 5)
			adjusted = (int)Math.Round(defeatedBasePointsLost * 0.1f);
		else if (diff == 4)
			adjusted = (int)Math.Round(defeatedBasePointsLost * 0.65f);
		else if (diff == 3)
			adjusted = (int)Math.Round(defeatedBasePointsLost * 0.85f);
		else
			adjusted = defeatedBasePointsLost;

		return new PvpApLostPlan(
			winnerLevel, defeatedLevel, diff,
			defeatedBasePointsLost, adjusted,
			$"StatFunctions.calculatePvPApLost -> levelDiff={diff} -> {adjusted} [before AP_PVP_LOST rate]"
		);
	}

	public static FallDamagePlan CreateFallDamagePlan(
		float fallDistance,
		float minimumDamageDistance,
		float maximumDamageDistance,
		float fallDamagePercent,
		int currentHp,
		int maxHp)
	{
		// Java parity: utils/stats/StatFunctions.calculateFallDamage.
		int fallDamage;
		bool isLethalFall;

		if (fallDistance >= maximumDamageDistance)
		{
			fallDamage = currentHp;
			isLethalFall = true;
		}
		else if (fallDistance >= minimumDamageDistance)
		{
			var dmgPerMeter = maxHp * fallDamagePercent / 100f;
			fallDamage = (int)(fallDistance * dmgPerMeter);
			isLethalFall = false;
		}
		else
		{
			fallDamage = 0;
			isLethalFall = false;
		}

		return new FallDamagePlan(
			fallDistance, minimumDamageDistance, maximumDamageDistance, fallDamagePercent,
			currentHp, maxHp, fallDamage, isLethalFall,
			isLethalFall
				? $"StatFunctions.calculateFallDamage -> distance({fallDistance}) >= max({maximumDamageDistance}) -> lethal, currentHp={currentHp}"
				: fallDamage > 0
					? $"StatFunctions.calculateFallDamage -> distance={fallDistance} * dmgPerMeter={maxHp}*{fallDamagePercent}%/100 = {fallDamage}"
					: $"StatFunctions.calculateFallDamage -> distance({fallDistance}) < min({minimumDamageDistance}) -> 0"
		);
	}
}
