using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class InstanceCooltimeTable
{
	private readonly IReadOnlyDictionary<int, InstanceCooltimeSummary> _templatesByWorldId;

	public InstanceCooltimeTable(IReadOnlyList<InstanceCooltimeSummary> templates)
	{
		Templates = templates;
		_templatesByWorldId = new ReadOnlyDictionary<int, InstanceCooltimeSummary>(
			templates.ToDictionary(template => template.WorldId));
	}

	public IReadOnlyList<InstanceCooltimeSummary> Templates { get; }

	public int Count => Templates.Count;

	public InstanceCooltimeSummary? GetInstanceCooltimeByWorldId(int worldId)
	{
		return _templatesByWorldId.GetValueOrDefault(worldId);
	}

	public int GetMaxMemberCount(int worldId, string race)
	{
		// Java parity: InstanceCooltimeData.getMaxMemberCount returns light capacity only for Race.ELYOS.
		var template = GetInstanceCooltimeByWorldId(worldId);
		if (template == null)
			return 0;

		return string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase)
			? template.MaxMemberLight
			: template.MaxMemberDark;
	}

	public int GetEnterMinLevel(int worldId, string race)
	{
		// Java parity: model/templates/InstanceCooltime enterMinLevelLight/enterMinLevelDark.
		var template = GetInstanceCooltimeByWorldId(worldId);
		if (template == null)
			return 0;

		return string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase)
			? template.EnterMinLevelLight
			: template.EnterMinLevelDark;
	}

	public int GetEnterMaxLevel(int worldId, string race)
	{
		// Java parity: model/templates/InstanceCooltime enterMaxLevelLight/enterMaxLevelDark.
		var template = GetInstanceCooltimeByWorldId(worldId);
		if (template == null)
			return 0;

		return string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase)
			? template.EnterMaxLevelLight
			: template.EnterMaxLevelDark;
	}

	public long CalculateInstanceEntranceCooltime(
		int worldId,
		DateTimeOffset now,
		int instanceCooldownRate = 1)
	{
		// Java parity: InstanceCooltimeData.calculateInstanceEntranceCooltime; caller supplies InstanceService.getInstanceRate result.
		var template = GetInstanceCooltimeByWorldId(worldId);
		if (template == null || template.MaxCount == 0)
			return 0;

		var instanceCoolTime = template.CoolTimeType.ToUpperInvariant() switch
		{
			"DAILY" => CalculateDailyReset(template, now).ToUnixTimeMilliseconds(),
			"WEEKLY" => CalculateWeeklyReset(template, now).ToUnixTimeMilliseconds(),
			"RELATIVE" => template.EntCoolTime == 0
				? 0
				: now.AddMinutes(template.EntCoolTime).ToUnixTimeMilliseconds(),
			_ => 0,
		};

		if (instanceCoolTime == 0 || instanceCooldownRate == 1)
			return instanceCoolTime;

		var nowMillis = now.ToUnixTimeMilliseconds();
		return nowMillis + ((instanceCoolTime - nowMillis) / instanceCooldownRate);
	}

	private static DateTimeOffset CalculateDailyReset(InstanceCooltimeSummary template, DateTimeOffset now)
	{
		var repeatDate = AtEntranceTime(now, template.EntCoolTime);
		return now > repeatDate ? repeatDate.AddDays(1) : repeatDate;
	}

	private static DateTimeOffset CalculateWeeklyReset(InstanceCooltimeSummary template, DateTimeOffset now)
	{
		var repeatDate = CalculateDailyReset(template, now);
		return repeatDate.AddDays(CalculateDaysUntilReset(template, repeatDate.DayOfWeek));
	}

	private static DateTimeOffset AtEntranceTime(DateTimeOffset now, int entCoolTime)
	{
		var hour = entCoolTime / 100;
		var minute = entCoolTime % 100;
		return new DateTimeOffset(now.Year, now.Month, now.Day, hour, minute, 0, now.Offset);
	}

	private static int CalculateDaysUntilReset(InstanceCooltimeSummary template, DayOfWeek day)
	{
		var resetDays = template.TypeValue
			.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(GetJavaDayValue)
			.Order()
			.ToArray();
		if (resetDays.Length == 0)
			return 0;

		var javaDay = GetJavaDayValue(day);
		foreach (var resetDay in resetDays)
		{
			if (resetDay >= javaDay)
				return resetDay - javaDay;
		}

		return (7 - javaDay) + resetDays[0];
	}

	private static int GetJavaDayValue(DayOfWeek day)
	{
		return day == DayOfWeek.Sunday ? 7 : (int)day;
	}

	private static int GetJavaDayValue(string day)
	{
		return day switch
		{
			"Mon" => 1,
			"Tue" => 2,
			"Wed" => 3,
			"Thu" => 4,
			"Fri" => 5,
			"Sat" => 6,
			"Sun" => 7,
			_ => throw new ArgumentException($"Invalid Day: {day}", nameof(day)),
		};
	}
}

public sealed record InstanceCooltimeSummary(
	int Id,
	int WorldId,
	string Race,
	int MaxCount,
	int MaxMemberLight = 0,
	int MaxMemberDark = 0,
	int EnterMinLevelLight = 0,
	int EnterMaxLevelLight = 0,
	int EnterMinLevelDark = 0,
	int EnterMaxLevelDark = 0,
	string CoolTimeType = "",
	string TypeValue = "",
	int EntCoolTime = 0);
