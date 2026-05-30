namespace Aion.GameServer.Services;

public enum MovementDirection
{
	None,
	Forward,
	Sideways,
	Backward,
}

public sealed record MovementStatModifierPlan(
	string StatName,
	MovementDirection Direction,
	float InputValue,
	float ModifiedValue,
	bool WasModified,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class MovementStatModifierPlanService
{
	// Java parity: utils/stats/StatFunctions.adjustStatByMovementModifier(Creature, StatEnum, float).
	// Returns original value if creature is not a Player, stat is null, or direction is None.
	private static readonly IReadOnlySet<string> ElementalResistanceStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"FIRE_RESISTANCE", "EARTH_RESISTANCE", "WATER_RESISTANCE",
		"WIND_RESISTANCE", "LIGHT_RESISTANCE", "DARK_RESISTANCE",
	};

	public static MovementStatModifierPlan CreatePlan(string statName, MovementDirection direction, float value, bool isPlayer)
	{
		if (!isPlayer || direction == MovementDirection.None)
		{
			return new MovementStatModifierPlan(statName, direction, value, value, WasModified: false,
				"StatFunctions.adjustStatByMovementModifier -> not player or no stat -> return original value");
		}

		var modified = ApplyModifier(statName, direction, value);
		return new MovementStatModifierPlan(
			statName, direction, value, modified, WasModified: modified != value,
			$"StatFunctions.adjustStatByMovementModifier(Player, {statName}, {direction}) -> {value} -> {modified}"
		);
	}

	private static float ApplyModifier(string stat, MovementDirection direction, float value)
	{
		var statUpper = stat.ToUpperInvariant();
		return direction switch
		{
			MovementDirection.Forward => statUpper switch
			{
				"PHYSICAL_ATTACK" or "MAGICAL_ATTACK" => value * 1.1f,
				"MAGICAL_DEFEND" or "PHYSICAL_DEFENSE" => value * 0.8f,
				_ when ElementalResistanceStats.Contains(stat) => value - 50,
				_ => value,
			},
			MovementDirection.Sideways => statUpper switch
			{
				"PHYSICAL_ATTACK" or "MAGICAL_ATTACK" or "SPEED" => value * 0.8f,
				"EVASION" => value + 300,
				_ => value,
			},
			MovementDirection.Backward => statUpper switch
			{
				"PHYSICAL_ATTACK" or "MAGICAL_ATTACK" => value * 0.8f,
				"SPEED" => value * 0.6f,
				"PARRY" or "BLOCK" => value + 500,
				_ => value,
			},
			_ => value,
		};
	}
}
