namespace Aion.GameServer.Services;

public sealed record CraftingXpPlan(
	int SkillPoint,
	int BonusPercent,
	int BaseXpReward,
	int BonusXp,
	int TotalXpReward,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class CraftingXpFormulaService
{
	public static int CalculateBaseXp(int skillPoint)
	{
		// Java parity: services/craft/CraftService.finishCrafting.
		// xpReward = (int)(0.008 * (skillLvl + 100) * (skillLvl + 100) + 60)
		return (int)(0.008 * (skillPoint + 100) * (skillPoint + 100) + 60);
	}

	public static CraftingXpPlan CreatePlan(int skillPoint, int bonusPercent)
	{
		// Java parity: services/craft/CraftService.finishCrafting XP calculation.
		// Base: floor(0.008 * (skillLvl + 100)^2 + 60)
		// With bonus: baseXp + baseXp * bonus / 100
		// Live Rates.SKILL_XP_CRAFTING.calcResult and boostStat are deferred.
		var baseXp = CalculateBaseXp(skillPoint);
		var bonusXp = baseXp * bonusPercent / 100;
		var total = baseXp + bonusXp;

		return new CraftingXpPlan(
			skillPoint, bonusPercent, baseXp, bonusXp, total,
			$"CraftService.finishCrafting -> base=(int)(0.008*({skillPoint}+100)^2+60)={baseXp} bonus={bonusPercent}% -> {total} [before SKILL_XP_CRAFTING rate and boost stat]"
		);
	}
}
