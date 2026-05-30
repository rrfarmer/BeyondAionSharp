namespace Aion.GameServer.Services;

public sealed record MagicalResistRatePlan(
	int AttackerLevel,
	int DefenderLevel,
	int MagicResist,
	int MagicAccuracy,
	int AccMod,
	bool IsAlwaysResist,
	bool IsPvP,
	int LevelDifference,
	int BaseResistRate,
	int LevelBonusAdded,
	int FinalResistRate,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class MagicalResistRatePlanService
{
	// Java parity: utils/stats/StatFunctions.calculateMagicalResistRate.
	public const int AlwaysResistRate = 1000;
	public const int PvPResistCap = 500;
	public const int PvENpcResistCap = 900; // MAGICAL_RESIST diffLimit

	public static MagicalResistRatePlan CreatePlan(
		int attackerLevel, int defenderLevel,
		int mResist, int mAccuracy, int accMod,
		bool isAlwaysResist,
		bool isPvP)
	{
		if (isAlwaysResist)
		{
			return new MagicalResistRatePlan(
				attackerLevel, defenderLevel, mResist, mAccuracy, accMod,
				isAlwaysResist, isPvP, LevelDifference: 0,
				BaseResistRate: AlwaysResistRate, LevelBonusAdded: 0, FinalResistRate: AlwaysResistRate,
				"StatFunctions.calculateMagicalResistRate -> alwaysResist -> return 1000"
			);
		}

		var levelDiff = defenderLevel - attackerLevel;
		var baseResist = mResist - mAccuracy - accMod;

		// Level bonus: only if mResist > 0 and level difference > 4
		var levelBonus = (mResist > 0 && levelDiff > 4) ? (levelDiff - 4) * 100 : 0;
		var resistRate = baseResist + levelBonus;

		// Cap application
		var cap = isPvP ? PvPResistCap : PvENpcResistCap;
		var finalRate = Math.Min(cap, resistRate);

		return new MagicalResistRatePlan(
			attackerLevel, defenderLevel, mResist, mAccuracy, accMod,
			isAlwaysResist, isPvP, levelDiff,
			baseResist, levelBonus, finalRate,
			$"StatFunctions.calculateMagicalResistRate -> resistRate={baseResist}+levelBonus({levelBonus})={resistRate} cap={cap} -> {finalRate}"
		);
	}
}
