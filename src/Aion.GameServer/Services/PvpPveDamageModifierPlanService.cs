namespace Aion.GameServer.Services;

public sealed record PvpPveDamageModifierPlan(
	bool IsPvpTarget,
	float InputDamage,
	float DamageAfterPvpFlat,
	int ClampedAttackBonus,
	int ClampedDefenseBonus,
	float RatioMultiplier,
	float FinalDamage,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PvpPveDamageModifierPlanService
{
	// Java parity: utils/stats/StatFunctions.adjustDamageByPvpOrPveModifiers constants.
	public const float PvpFlatModifier = 0.42f;
	public const float MinDamageMultiplier = 0.1f;

	public static PvpPveDamageModifierPlan CreatePlan(
		bool isPvpTarget,
		float baseDamage,
		int pvpDamage,
		bool useTemplateDmg,
		bool isPlayerAttacker,
		float npcLevelDiffMod,
		int rawAttackBonus,
		int rawDefenseBonus)
	{
		// Java parity: utils/stats/StatFunctions.adjustDamageByPvpOrPveModifiers.
		float damage = baseDamage;

		if (isPvpTarget)
		{
			if (pvpDamage > 0)
				damage *= pvpDamage * 0.01f;
			damage *= PvpFlatModifier;
		}
		else if (!useTemplateDmg && isPlayerAttacker)
		{
			// Java: if (attacker instanceof Player) damage *= 1f - getNpcLevelDiffMod(target, attacker)
			damage *= 1f - npcLevelDiffMod;
		}

		var mode = isPvpTarget ? CombatMode.Pvp : CombatMode.Pve;
		var clampedAttack = StatCapFormulaService.CreatePvpPveRatioClampPlan(mode, RatioType.Attack, rawAttackBonus).ClampedValue;
		var clampedDefense = StatCapFormulaService.CreatePvpPveRatioClampPlan(mode, RatioType.Defense, rawDefenseBonus).ClampedValue;

		var multiplier = Math.Max(MinDamageMultiplier, 1f + (clampedAttack - clampedDefense) / 1000f);
		var finalDamage = damage * multiplier;

		return new PvpPveDamageModifierPlan(
			isPvpTarget, baseDamage, damage,
			clampedAttack, clampedDefense, multiplier, finalDamage,
			$"StatFunctions.adjustDamageByPvpOrPveModifiers -> {(isPvpTarget ? "PvP" : "PvE")} base={baseDamage} afterFlat={damage} mult={multiplier:F3} final={finalDamage:F1}"
		);
	}
}
