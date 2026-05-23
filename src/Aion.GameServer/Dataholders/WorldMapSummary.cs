namespace Aion.GameServer.Dataholders;

public readonly record struct WorldMapSummary(
	int MapId,
	bool IsInstance,
	int TwinCount,
	string DropType = "NONE",
	WorldZoneAttributes Flags = WorldZoneAttributes.None)
{
	public bool AllowsFlight => HasAttribute(WorldZoneAttributes.Fly);

	public bool AllowsGlide => HasAttribute(WorldZoneAttributes.Glide);

	public bool HasAttribute(WorldZoneAttributes attribute)
	{
		return (Flags & attribute) != 0;
	}

	public bool HasOverriddenOption(WorldZoneAttributes option, WorldZoneAttributes currentFlags)
	{
		// Java parity: world/WorldMap.hasOverridenOption compares mutable worldOptions against WorldMapTemplate.flags.
		if ((Flags & option) == 0)
			return (currentFlags & option) != 0;
		return (currentFlags & option) == 0;
	}

	public bool IsFlightAllowed(WorldZoneAttributes currentFlags)
	{
		// Java parity: world/WorldMap.isFlightAllowed reads mutable worldOptions.
		return (currentFlags & WorldZoneAttributes.Fly) != 0;
	}

	public bool CanGlide(WorldZoneAttributes currentFlags)
	{
		// Java parity: world/WorldMap.canGlide reads mutable worldOptions.
		return (currentFlags & WorldZoneAttributes.Glide) != 0;
	}

	public static WorldZoneAttributes ParseFlags(string? flags)
	{
		// Java parity: world/zone/ZoneAttributes.fromList over WorldMapTemplate.flags.
		if (string.IsNullOrWhiteSpace(flags))
			return WorldZoneAttributes.None;

		var parsed = WorldZoneAttributes.None;
		foreach (var token in flags.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			parsed |= token switch
			{
				"BIND" => WorldZoneAttributes.Bind,
				"RECALL" => WorldZoneAttributes.Recall,
				"GLIDE" => WorldZoneAttributes.Glide,
				"FLY" => WorldZoneAttributes.Fly,
				"RIDE" => WorldZoneAttributes.Ride,
				"FLY_RIDE" => WorldZoneAttributes.FlyRide,
				"PVP" => WorldZoneAttributes.PvpEnabled,
				"DUEL_SAME_RACE" => WorldZoneAttributes.DuelSameRaceEnabled,
				"DUEL_OTHER_RACE" => WorldZoneAttributes.DuelOtherRaceEnabled,
				"NO_RETURN_BATTLE" => WorldZoneAttributes.NoReturnBattle,
				_ => WorldZoneAttributes.None,
			};
		}

		return parsed;
	}
}

[Flags]
public enum WorldZoneAttributes
{
	None = 0,
	Bind = 1 << 0,
	Recall = 1 << 1,
	Glide = 1 << 2,
	Fly = 1 << 3,
	Ride = 1 << 4,
	FlyRide = 1 << 5,
	PvpEnabled = 1 << 6,
	DuelSameRaceEnabled = 1 << 7,
	DuelOtherRaceEnabled = 1 << 8,
	NoReturnBattle = 1 << 9,
}
