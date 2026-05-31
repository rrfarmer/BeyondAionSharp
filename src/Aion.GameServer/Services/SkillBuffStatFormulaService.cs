namespace Aion.GameServer.Services;

public static class SkillBuffStatFormulaService
{
	public static SkillBuffStatFormulaState CreateState(
		float baseValue,
		float bonus = 0f,
		float baseRate = 1f,
		float bonusRate = 1f,
		float fixedBonusRate = 0f)
	{
		return new SkillBuffStatFormulaState(baseValue, bonus, baseRate, bonusRate, fixedBonusRate);
	}

	public static int GetCurrent(SkillBuffStatFormulaState state)
	{
		// Java parity: model/stats/calc/Stat2.getCurrent.
		return (int)(state.Base * state.BaseRate + state.Bonus * state.BonusRate + state.Base * state.FixedBonusRate);
	}

	public static SkillBuffStatFormulaState ApplyAdd(
		SkillBuffStatFormulaState state,
		SkillBuffStatFormulaMode mode,
		int value,
		bool isBonus)
	{
		// Java parity: model/stats/calc/functions/StatAddFunction.apply.
		return isBonus
			? AddToBonus(state, mode, value)
			: AddToBase(state, mode, value);
	}

	public static SkillBuffStatFormulaState ApplyRate(
		SkillBuffStatFormulaState state,
		SkillBuffStatFormulaMode mode,
		string statName,
		int value,
		bool isBonus)
	{
		// Java parity: model/stats/calc/functions/StatRateFunction.apply.
		if (isBonus)
		{
			var baseValue = (int)state.Base;
			if (string.Equals(statName, "SPEED", StringComparison.Ordinal) && value < 0 && (int)state.Bonus < 0)
				baseValue = GetCurrent(state);
			return AddToBonus(state, mode, baseValue * value / 100f);
		}

		return state with { Base = (int)state.Base * CalculatePercent(mode, value) };
	}

	public static SkillBuffStatFormulaState ApplySet(
		SkillBuffStatFormulaState state,
		int value,
		bool isBonus)
	{
		// Java parity: model/stats/calc/functions/StatSetFunction.apply.
		return isBonus
			? state with { Bonus = value }
			: state with { Base = value };
	}

	public static SkillBuffStatFormulaState AddToBase(
		SkillBuffStatFormulaState state,
		SkillBuffStatFormulaMode mode,
		float value)
	{
		// Java parity: AdditionStat.addToBase / ReverseStat.addToBase.
		return mode switch
		{
			SkillBuffStatFormulaMode.Addition => state with { Base = state.Base + value },
			SkillBuffStatFormulaMode.Reverse => state with { Base = MathF.Max(0f, state.Base - value) },
			_ => state,
		};
	}

	public static SkillBuffStatFormulaState AddToBonus(
		SkillBuffStatFormulaState state,
		SkillBuffStatFormulaMode mode,
		float value)
	{
		// Java parity: AdditionStat.addToBonus / ReverseStat.addToBonus.
		return mode switch
		{
			SkillBuffStatFormulaMode.Addition => state with { Bonus = state.Bonus + state.BonusRate * value },
			SkillBuffStatFormulaMode.Reverse => state with { Bonus = state.Bonus - state.BonusRate * value },
			_ => state,
		};
	}

	public static float CalculatePercent(SkillBuffStatFormulaMode mode, int delta)
	{
		// Java parity: AdditionStat.calculatePercent / ReverseStat.calculatePercent.
		return mode switch
		{
			SkillBuffStatFormulaMode.Addition => (100 + delta) / 100f,
			SkillBuffStatFormulaMode.Reverse => MathF.Max(0f, (100 - delta) / 100f),
			_ => 1f,
		};
	}
}

public enum SkillBuffStatFormulaMode
{
	Addition,
	Reverse,
}

public sealed record SkillBuffStatFormulaState(
	float Base,
	float Bonus,
	float BaseRate,
	float BonusRate,
	float FixedBonusRate);
