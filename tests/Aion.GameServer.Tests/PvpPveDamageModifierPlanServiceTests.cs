using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PvpPveDamageModifierPlanServiceTests
{
	[Fact]
	public void CreatePlan_PvENoModifiers_ReturnBaseWithNeutralMultiplier()
	{
		// PvE, no bonuses → multiplier = max(0.1, 1 + 0/1000) = 1.0
		var plan = PvpPveDamageModifierPlanService.CreatePlan(
			isPvpTarget: false, baseDamage: 1000f, pvpDamage: 0,
			useTemplateDmg: false, isPlayerAttacker: false,
			npcLevelDiffMod: 0f, rawAttackBonus: 0, rawDefenseBonus: 0);

		Assert.Equal(1000f, plan.FinalDamage, 3);
		Assert.Equal(1.0f, plan.RatioMultiplier, 6);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_PvP_Applies42PercentFlat()
	{
		// PvP, no pvpDamage, no bonuses → damage = 1000 * 0.42 = 420; mult=1.0 → final=420
		var plan = PvpPveDamageModifierPlanService.CreatePlan(
			isPvpTarget: true, baseDamage: 1000f, pvpDamage: 0,
			useTemplateDmg: false, isPlayerAttacker: true,
			npcLevelDiffMod: 0f, rawAttackBonus: 0, rawDefenseBonus: 0);

		Assert.Equal(420f, plan.DamageAfterPvpFlat, 3);
		Assert.Equal(420f, plan.FinalDamage, 3);
	}

	[Fact]
	public void CreatePlan_PvP_PvpDamageScalesBeforeFlat()
	{
		// pvpDamage=200 → damage = 1000 * 200 * 0.01 = 2000; then * 0.42 = 840
		var plan = PvpPveDamageModifierPlanService.CreatePlan(
			isPvpTarget: true, baseDamage: 1000f, pvpDamage: 200,
			useTemplateDmg: false, isPlayerAttacker: true,
			npcLevelDiffMod: 0f, rawAttackBonus: 0, rawDefenseBonus: 0);

		Assert.Equal(840f, plan.DamageAfterPvpFlat, 3);
	}

	[Fact]
	public void CreatePlan_PvE_PlayerAttackerAppliesLevelDiffMod()
	{
		// npcLevelDiffMod=0.3 → damage = 1000 * (1 - 0.3) = 700; mult=1.0 → final=700
		var plan = PvpPveDamageModifierPlanService.CreatePlan(
			isPvpTarget: false, baseDamage: 1000f, pvpDamage: 0,
			useTemplateDmg: false, isPlayerAttacker: true,
			npcLevelDiffMod: 0.3f, rawAttackBonus: 0, rawDefenseBonus: 0);

		Assert.Equal(700f, plan.DamageAfterPvpFlat, 3);
	}

	[Fact]
	public void CreatePlan_AttackBonusBoostsDamage()
	{
		// attackBonus=200, defenseBonus=0 → mult = max(0.1, 1 + 200/1000) = 1.2
		var plan = PvpPveDamageModifierPlanService.CreatePlan(
			isPvpTarget: false, baseDamage: 1000f, pvpDamage: 0,
			useTemplateDmg: false, isPlayerAttacker: false,
			npcLevelDiffMod: 0f, rawAttackBonus: 200, rawDefenseBonus: 0);

		Assert.Equal(1.2f, plan.RatioMultiplier, 6);
		Assert.Equal(1200f, plan.FinalDamage, 3);
	}

	[Fact]
	public void CreatePlan_MultiplierFloorsAt10Percent()
	{
		// Massive defense → mult would be negative → floor to 0.1
		var plan = PvpPveDamageModifierPlanService.CreatePlan(
			isPvpTarget: false, baseDamage: 1000f, pvpDamage: 0,
			useTemplateDmg: false, isPlayerAttacker: false,
			npcLevelDiffMod: 0f, rawAttackBonus: 0, rawDefenseBonus: 5000);

		Assert.Equal(PvpPveDamageModifierPlanService.MinDamageMultiplier, plan.RatioMultiplier, 6);
		Assert.Equal(100f, plan.FinalDamage, 3); // 1000 * 0.1
	}
}
