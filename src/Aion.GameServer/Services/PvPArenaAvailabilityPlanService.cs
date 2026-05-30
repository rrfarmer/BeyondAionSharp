namespace Aion.GameServer.Services;

public enum PvPArenaType
{
	FfaOrSolo,
	Harmony,
	Glory,
	Other,
}

public sealed record PvPArenaAvailabilityPlan(
	PvPArenaType ArenaType,
	int Hour,
	int DayOfWeek,
	bool IsAvailable,
	string JavaSource
)
{
	public bool IsLive => false;
}

/// <summary>
/// PvP arena item requirements.
/// </summary>
public sealed record PvPArenaItemRequirementPlan(
	PvPArenaType ArenaType,
	int RequiredItemId,
	int RequiredCount,
	bool HasRequirement,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PvPArenaAvailabilityPlanService
{
	// Java parity: services/instance/PvPArenaService arena item IDs.
	public const int FfaSoloArenaTicketItemId = 186000135;
	public const int HarmonyArenaTicketItemId = 186000184;
	public const int GloryArenaTicketItemId = 186000185;
	public const int GloryArenaTicketMinCount = 3;

	public static PvPArenaAvailabilityPlan CreateAvailabilityPlan(PvPArenaType arenaType, int hour, int dayOfWeek)
	{
		// Java parity: services/instance/PvPArenaService.checkTime and each availability method.
		// dayOfWeek: 1=Monday...7=Sunday (Java DayOfWeek.getValue() 1-7)
		var available = arenaType switch
		{
			PvPArenaType.FfaOrSolo => IsPvPArenaAvailable(hour, dayOfWeek),
			PvPArenaType.Harmony => IsHarmonyArenaAvailable(hour, dayOfWeek),
			PvPArenaType.Glory => IsGloryArenaAvailable(hour, dayOfWeek),
			_ => true,
		};

		return new PvPArenaAvailabilityPlan(
			arenaType, hour, dayOfWeek, available,
			$"PvPArenaService.checkTime -> {arenaType} day={dayOfWeek} hour={hour} -> {available}"
		);
	}

	public static PvPArenaItemRequirementPlan CreateItemRequirementPlan(PvPArenaType arenaType)
	{
		// Java parity: services/instance/PvPArenaService.checkItem.
		return arenaType switch
		{
			PvPArenaType.FfaOrSolo => new PvPArenaItemRequirementPlan(arenaType, FfaSoloArenaTicketItemId, 1, true,
				"PvPArenaService.checkItem -> FFA/Solo -> inventory.getItemCountByItemId(186000135) > 0"),
			PvPArenaType.Harmony => new PvPArenaItemRequirementPlan(arenaType, HarmonyArenaTicketItemId, 1, true,
				"PvPArenaService.checkItem -> Harmony -> inventory.getItemCountByItemId(186000184) > 0"),
			PvPArenaType.Glory => new PvPArenaItemRequirementPlan(arenaType, GloryArenaTicketItemId, GloryArenaTicketMinCount, true,
				"PvPArenaService.checkItem -> Glory -> inventory.getItemCountByItemId(186000185) >= 3"),
			_ => new PvPArenaItemRequirementPlan(arenaType, 0, 0, false,
				"PvPArenaService.checkItem -> other -> no item requirement"),
		};
	}

	// Java: weekday vs weekend availability
	private static bool IsPvPArenaAvailable(int hour, int day)
	{
		// Java: day 6 (Saturday) or 7 (Sunday): 0-1 or 10-23; weekday: 0-1, 12-13, 18-23
		if (day == 6 || day == 7)
			return hour == 0 || hour == 1 || (hour >= 10 && hour <= 23);
		return hour == 0 || hour == 1 || hour == 12 || hour == 13 || (hour >= 18 && hour <= 23);
	}

	private static bool IsHarmonyArenaAvailable(int hour, int day)
	{
		// Java: Saturday 10+|1|2; Sunday 0|1|10+; weekday 10-13 or 18-23
		if (day == 6)
			return hour >= 10 || hour == 1 || hour == 2;
		if (day == 7)
			return hour == 0 || hour == 1 || hour >= 10;
		return (hour >= 10 && hour < 14) || (hour >= 18 && hour <= 23);
	}

	private static bool IsGloryArenaAvailable(int hour, int day)
	{
		// Java: (day==6||day==7) && hour>=20 && hour<22
		return (day == 6 || day == 7) && hour >= 20 && hour < 22;
	}
}
