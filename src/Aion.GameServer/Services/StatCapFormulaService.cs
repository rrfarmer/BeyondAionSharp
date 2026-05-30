namespace Aion.GameServer.Services;

public enum CombatMode { Pvp, Pve }
public enum RatioType { Attack, Defense }

public sealed record StatDifferenceLimitPlan(
	string StatName,
	int DifferenceLimit,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record PvpPveRatioClampPlan(
	CombatMode Mode,
	RatioType RatioType,
	int InputValue,
	int ClampedValue,
	int MinCap,
	int MaxCap,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record ElementalDefenseCapPlan(
	bool IsPlayer,
	int PlayerLevel,
	int Cap,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class StatCapFormulaService
{
	// Java parity: model/stats/calc/StatCapUtil.differenceLimitFor.
	public static int GetDifferenceLimit(string statName)
	{
		return statName.ToUpperInvariant() switch
		{
			"BLOCK" => 500,
			"PHYSICAL_CRITICAL" => 500,
			"MAGICAL_CRITICAL" => 500,
			"MAGICAL_RESIST" => 900,
			"EVASION" => 300,
			"PARRY" => 400,
			"BOOST_MAGICAL_SKILL" => 2900,
			_ => int.MaxValue,
		};
	}

	public static StatDifferenceLimitPlan CreateDifferenceLimitPlan(string statName)
	{
		var limit = GetDifferenceLimit(statName);
		return new StatDifferenceLimitPlan(
			statName.ToUpperInvariant(), limit,
			$"StatCapUtil.getDifferenceLimit({statName}) -> {limit}"
		);
	}

	public static PvpPveRatioClampPlan CreatePvpPveRatioClampPlan(CombatMode mode, RatioType ratioType, int value)
	{
		// Java parity: model/stats/calc/StatCapUtil.limitValueForPvpOrPveStat.
		var (min, max) = (mode, ratioType) switch
		{
			(CombatMode.Pvp, RatioType.Attack) => (-900, 1000),
			(CombatMode.Pvp, RatioType.Defense) => (-1000, 900),
			(CombatMode.Pve, RatioType.Attack) => (-900, 5000),
			(CombatMode.Pve, RatioType.Defense) => (-5000, 900),
			_ => (-900, 1000),
		};
		var clamped = Math.Clamp(value, min, max);
		return new PvpPveRatioClampPlan(
			mode, ratioType, value, clamped, min, max,
			$"StatCapUtil.limitValueForPvpOrPveStat({mode}, {ratioType}, {value}) -> clamp({min}..{max}) = {clamped}"
		);
	}

	public static ElementalDefenseCapPlan CreateElementalDefenseCapPlan(bool isPlayer, int playerLevel = 0)
	{
		// Java parity: model/stats/calc/StatCapUtil.getElementalDefenseCapForCreature.
		int cap;
		if (isPlayer)
			cap = 1000 + Math.Max(0, playerLevel - 50) * 10;
		else
			cap = 1300; // getElementalDefenseBaseValue()

		return new ElementalDefenseCapPlan(
			isPlayer, playerLevel, cap,
			isPlayer
				? $"StatCapUtil.getElementalDefenseCapForCreature(Player level={playerLevel}) -> 1000+max(0,{playerLevel}-50)*10 = {cap}"
				: $"StatCapUtil.getElementalDefenseCapForCreature(NPC) -> {cap} (base)"
		);
	}
}
