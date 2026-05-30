using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class StatCapFormulaServiceTests
{
	// --- getDifferenceLimit ---

	[Theory]
	[InlineData("BLOCK", 500)]
	[InlineData("PHYSICAL_CRITICAL", 500)]
	[InlineData("MAGICAL_CRITICAL", 500)]
	[InlineData("MAGICAL_RESIST", 900)]
	[InlineData("EVASION", 300)]
	[InlineData("PARRY", 400)]
	[InlineData("BOOST_MAGICAL_SKILL", 2900)]
	public void GetDifferenceLimit_ReturnsJavaValues(string stat, int expected)
	{
		Assert.Equal(expected, StatCapFormulaService.GetDifferenceLimit(stat));
	}

	[Fact]
	public void GetDifferenceLimit_UnknownStatIsMaxInt()
	{
		Assert.Equal(int.MaxValue, StatCapFormulaService.GetDifferenceLimit("SOME_OTHER_STAT"));
	}

	// --- PvP/PvE ratio clamping ---

	[Theory]
	[InlineData(CombatMode.Pvp, RatioType.Attack, 1200, 1000)]  // clamped to max 1000
	[InlineData(CombatMode.Pvp, RatioType.Attack, -1000, -900)] // clamped to min -900
	[InlineData(CombatMode.Pvp, RatioType.Defense, 1000, 900)]  // clamped to max 900
	[InlineData(CombatMode.Pve, RatioType.Attack, 6000, 5000)]  // clamped to max 5000
	[InlineData(CombatMode.Pve, RatioType.Defense, -6000, -5000)] // clamped
	public void CreatePvpPveRatioClampPlan_ClampsCorrectly(CombatMode mode, RatioType rType, int input, int expected)
	{
		var plan = StatCapFormulaService.CreatePvpPveRatioClampPlan(mode, rType, input);

		Assert.Equal(expected, plan.ClampedValue);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePvpPveRatioClampPlan_WithinBoundsIsUnchanged()
	{
		var plan = StatCapFormulaService.CreatePvpPveRatioClampPlan(CombatMode.Pvp, RatioType.Attack, 500);

		Assert.Equal(500, plan.ClampedValue);
	}

	// --- Elemental defense cap ---

	[Fact]
	public void CreateElementalDefenseCapPlan_NpcAlwaysBase1300()
	{
		var plan = StatCapFormulaService.CreateElementalDefenseCapPlan(isPlayer: false);

		Assert.Equal(1300, plan.Cap);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateElementalDefenseCapPlan_PlayerBelow50LevelIs1000()
	{
		var plan = StatCapFormulaService.CreateElementalDefenseCapPlan(isPlayer: true, playerLevel: 30);

		Assert.Equal(1000, plan.Cap); // max(0, 30-50) = 0 → 1000 + 0 = 1000
	}

	[Fact]
	public void CreateElementalDefenseCapPlan_PlayerAt60LevelIsBonus()
	{
		// 1000 + max(0, 60-50)*10 = 1000 + 100 = 1100
		var plan = StatCapFormulaService.CreateElementalDefenseCapPlan(isPlayer: true, playerLevel: 60);

		Assert.Equal(1100, plan.Cap);
	}
}
