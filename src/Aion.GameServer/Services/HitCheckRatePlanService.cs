namespace Aion.GameServer.Services;

public enum HitCheckType { Dodge, Parry, Block }

public sealed record HitCheckRatePlan(
	HitCheckType CheckType,
	float DefenderStat,
	float AttackerAccuracy,
	float NpcLevelDiffMod,
	bool IsNpcTarget,
	float BaseRate,
	float AdjustedRate,
	float EffectiveRate,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class HitCheckRatePlanService
{
	// Java parity: StatCapUtil.differenceLimitFor for each check type.
	public static int GetDifferenceLimit(HitCheckType checkType) => checkType switch
	{
		HitCheckType.Dodge => 300,   // EVASION
		HitCheckType.Parry => 400,   // PARRY
		HitCheckType.Block => 500,   // BLOCK
		_ => 300,
	};

	public static HitCheckRatePlan CreatePlan(
		HitCheckType checkType,
		float defenderStat,
		float attackerAccuracy,
		bool isNpcTarget,
		float npcLevelDiffMod = 0f)
	{
		// Java parity: StatFunctions.checkIsDodgedHit, checkIsParriedHit, checkIsBlockedHit.
		// base = defenderStat - attackerAccuracy (after movement modifier applied externally)
		// NPC dodge only: dodgeRate *= (1 + getNpcLevelDiffMod)
		// effective = min(differenceLimit, rate)
		var baseRate = defenderStat - attackerAccuracy;

		var adjustedRate = (checkType == HitCheckType.Dodge && isNpcTarget)
			? baseRate * (1f + npcLevelDiffMod)
			: baseRate;

		var limit = GetDifferenceLimit(checkType);
		var effectiveRate = Math.Min(limit, adjustedRate);

		return new HitCheckRatePlan(
			checkType, defenderStat, attackerAccuracy, npcLevelDiffMod, isNpcTarget,
			baseRate, adjustedRate, effectiveRate,
			$"StatFunctions.check{checkType}Hit -> base={baseRate} adjusted={adjustedRate} effective=min({limit},{adjustedRate})={effectiveRate}/1000"
		);
	}
}
