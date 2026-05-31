using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillBuffStatFormulaServiceTests
{
	[Fact]
	public void GetCurrent_TruncatesLikeJavaStat2()
	{
		var state = SkillBuffStatFormulaService.CreateState(
			baseValue: 99.9f,
			bonus: 9.9f,
			baseRate: 1f,
			bonusRate: 1f,
			fixedBonusRate: 0f);

		Assert.Equal(109, SkillBuffStatFormulaService.GetCurrent(state));
	}

	[Fact]
	public void ApplyAdd_MatchesJavaAdditionAndReverseStatBranches()
	{
		var addition = SkillBuffStatFormulaService.ApplyAdd(
			SkillBuffStatFormulaService.CreateState(100, bonusRate: 2f),
			SkillBuffStatFormulaMode.Addition,
			value: 7,
			isBonus: true);
		Assert.Equal(14, addition.Bonus);

		var reverseBase = SkillBuffStatFormulaService.ApplyAdd(
			SkillBuffStatFormulaService.CreateState(5),
			SkillBuffStatFormulaMode.Reverse,
			value: 10,
			isBonus: false);
		Assert.Equal(0, reverseBase.Base);

		var reverseBonus = SkillBuffStatFormulaService.ApplyAdd(
			SkillBuffStatFormulaService.CreateState(100, bonusRate: 2f),
			SkillBuffStatFormulaMode.Reverse,
			value: 7,
			isBonus: true);
		Assert.Equal(-14, reverseBonus.Bonus);
	}

	[Fact]
	public void ApplyRate_MatchesJavaPercentAndNegativeSpeedBranches()
	{
		var baseRate = SkillBuffStatFormulaService.ApplyRate(
			SkillBuffStatFormulaService.CreateState(99.9f),
			SkillBuffStatFormulaMode.Addition,
			statName: "BOOST_DROP_RATE",
			value: 10,
			isBonus: false);
		Assert.Equal(108.9f, baseRate.Base, precision: 3);

		var reverseBaseRate = SkillBuffStatFormulaService.ApplyRate(
			SkillBuffStatFormulaService.CreateState(100),
			SkillBuffStatFormulaMode.Reverse,
			statName: "SPEED",
			value: 150,
			isBonus: false);
		Assert.Equal(0, reverseBaseRate.Base);

		var negativeSpeed = SkillBuffStatFormulaService.ApplyRate(
			SkillBuffStatFormulaService.CreateState(100, bonus: -10),
			SkillBuffStatFormulaMode.Addition,
			statName: "SPEED",
			value: -50,
			isBonus: true);
		Assert.Equal(-55, negativeSpeed.Bonus);
		Assert.Equal(45, SkillBuffStatFormulaService.GetCurrent(negativeSpeed));
	}

	[Fact]
	public void ApplySet_MatchesJavaBaseAndBonusSetBranches()
	{
		var baseSet = SkillBuffStatFormulaService.ApplySet(
			SkillBuffStatFormulaService.CreateState(100, bonus: 12),
			value: 80,
			isBonus: false);
		Assert.Equal(80, baseSet.Base);
		Assert.Equal(12, baseSet.Bonus);

		var bonusSet = SkillBuffStatFormulaService.ApplySet(
			SkillBuffStatFormulaService.CreateState(100, bonus: 12),
			value: 30,
			isBonus: true);
		Assert.Equal(100, bonusSet.Base);
		Assert.Equal(30, bonusSet.Bonus);
	}
}
