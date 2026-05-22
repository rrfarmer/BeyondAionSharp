using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class NpcSpawnTable
{
	private readonly IReadOnlyDictionary<int, IReadOnlyList<NpcSpawnSummary>> _spawnsByMapId;
	private readonly IReadOnlyDictionary<string, NpcSpawnSummary> _riftSpawnsByAnchor;

	public NpcSpawnTable(IReadOnlyList<NpcSpawnSummary> spawns)
	{
		Spawns = spawns;
		_spawnsByMapId = new ReadOnlyDictionary<int, IReadOnlyList<NpcSpawnSummary>>(
			spawns
				.GroupBy(spawn => spawn.MapId)
				.ToDictionary(
					group => group.Key,
					group => (IReadOnlyList<NpcSpawnSummary>)group.ToArray()));
		_riftSpawnsByAnchor = new ReadOnlyDictionary<string, NpcSpawnSummary>(
			BuildRiftAnchorLookup(spawns));
	}

	public IReadOnlyList<NpcSpawnSummary> Spawns { get; }

	public int Count => Spawns.Count;

	public IReadOnlyList<NpcSpawnSummary> GetSpawnsForMap(int mapId)
	{
		return _spawnsByMapId.GetValueOrDefault(mapId) ?? Array.Empty<NpcSpawnSummary>();
	}

	public bool TryGetRiftSpawnByAnchor(string anchor, out NpcSpawnSummary? spawn)
	{
		return _riftSpawnsByAnchor.TryGetValue(anchor, out spawn);
	}

	private static Dictionary<string, NpcSpawnSummary> BuildRiftAnchorLookup(IReadOnlyList<NpcSpawnSummary> spawns)
	{
		// Java parity: SpawnEngine registers ordinary spawn groups with handler="RIFT" through RiftManager.addRiftSpawnTemplate.
		var anchors = new Dictionary<string, NpcSpawnSummary>(StringComparer.Ordinal);
		foreach (var group in spawns
			.Where(spawn => string.Equals(spawn.Handler, "RIFT", StringComparison.OrdinalIgnoreCase))
			.GroupBy(spawn => new { spawn.MapId, spawn.NpcId, spawn.PoolSize }))
		{
			var orderedGroup = group.ToArray();
			var candidates = orderedGroup[0].PoolSize > 0
				? orderedGroup.Take(1)
				: orderedGroup;
			foreach (var candidate in candidates)
			{
				if (!string.IsNullOrEmpty(candidate.Anchor))
					anchors[candidate.Anchor] = candidate;
			}
		}

		return anchors;
	}
}

public sealed record NpcSpawnSummary(
	int MapId,
	int NpcId,
	float X,
	float Y,
	float Z,
	byte Heading,
	int RespawnSeconds,
	int PoolSize,
	byte DifficultId,
	string Handler,
	int StaticId,
	int RandomWalkRange,
	string WalkerId,
	int WalkerIndex,
	string Anchor,
	int State,
	string AiName,
	bool Custom,
	TemporarySpawnSchedule? GroupTemporarySchedule,
	TemporarySpawnSchedule? SpotTemporarySchedule)
{
	public bool HasTemporarySchedule => GroupTemporarySchedule != null || SpotTemporarySchedule != null;
}

public sealed class NpcRiftSpawnTable
{
	private readonly IReadOnlyDictionary<int, IReadOnlyList<NpcRiftSpawnSummary>> _spawnsByRiftId;
	private readonly IReadOnlyDictionary<string, NpcRiftSpawnSummary> _spawnsByAnchor;

	public NpcRiftSpawnTable(IReadOnlyList<NpcRiftSpawnSummary> spawns)
	{
		Spawns = spawns;
		_spawnsByRiftId = new ReadOnlyDictionary<int, IReadOnlyList<NpcRiftSpawnSummary>>(
			spawns
				.GroupBy(spawn => spawn.RiftId)
				.ToDictionary(
					group => group.Key,
					group => (IReadOnlyList<NpcRiftSpawnSummary>)group.ToArray()));
		_spawnsByAnchor = new ReadOnlyDictionary<string, NpcRiftSpawnSummary>(
			BuildAnchorLookup(spawns));
	}

	public IReadOnlyList<NpcRiftSpawnSummary> Spawns { get; }

	public int Count => Spawns.Count;

	public IReadOnlyList<NpcRiftSpawnSummary> GetSpawnsForRift(int riftId)
	{
		return _spawnsByRiftId.GetValueOrDefault(riftId) ?? Array.Empty<NpcRiftSpawnSummary>();
	}

	public bool TryGetSpawnByAnchor(string anchor, out NpcRiftSpawnSummary? spawn)
	{
		return _spawnsByAnchor.TryGetValue(anchor, out spawn);
	}

	private static Dictionary<string, NpcRiftSpawnSummary> BuildAnchorLookup(
		IReadOnlyList<NpcRiftSpawnSummary> spawns)
	{
		// Java parity: services/rift/RiftManager.addRiftSpawnTemplate maps every anchor, except pooled groups map only their first template.
		var anchors = new Dictionary<string, NpcRiftSpawnSummary>(StringComparer.Ordinal);
		foreach (var group in spawns.GroupBy(spawn => new { spawn.MapId, spawn.RiftId, spawn.SpawnGroupIndex }))
		{
			var orderedGroup = group.OrderBy(spawn => spawn.SpotIndex).ToArray();
			var candidates = orderedGroup[0].PoolSize > 0
				? orderedGroup.Take(1)
				: orderedGroup;
			foreach (var candidate in candidates)
			{
				if (!string.IsNullOrEmpty(candidate.Anchor))
					anchors[candidate.Anchor] = candidate;
			}
		}

		return anchors;
	}
}

public sealed record NpcRiftSpawnSummary(
	int MapId,
	int RiftId,
	int SpawnGroupIndex,
	int SpotIndex,
	int NpcId,
	float X,
	float Y,
	float Z,
	byte Heading,
	int RespawnSeconds,
	int PoolSize,
	int StaticId,
	int RandomWalkRange,
	string WalkerId,
	int WalkerIndex,
	string Anchor,
	int State,
	string AiName);

public sealed record TemporarySpawnSchedule(
	int WeekdayMask,
	int? SpawnHour,
	int? SpawnDay,
	int? SpawnMonth,
	int? DespawnHour,
	int? DespawnDay,
	int? DespawnMonth)
{
	public bool IsInSpawnTime(int gameMinutes, DayOfWeek serverDayOfWeek)
	{
		// Java parity: model/templates/spawns/TemporarySpawn.isInSpawnTime.
		if (WeekdayMask != 0 && (WeekdayMask & (1 << (int)serverDayOfWeek)) == 0)
			return false;

		var gameTime = AionGameTime.FromMinutes(gameMinutes);
		if (SpawnMonth.HasValue && !CheckDate(gameTime.Month, SpawnMonth.Value, DespawnMonth))
			return false;
		if (SpawnDay.HasValue && !CheckDate(gameTime.Day, SpawnDay.Value, DespawnDay))
			return false;
		if (SpawnHour.HasValue && !CheckHour(gameTime.Hour, SpawnHour.Value, DespawnHour))
			return false;

		return true;
	}

	public bool CanSpawn(int gameMinutes, DayOfWeek serverDayOfWeek)
	{
		// Java parity: model/templates/spawns/TemporarySpawn.canSpawn.
		return (WeekdayMask == 0 || (WeekdayMask & (1 << (int)serverDayOfWeek)) != 0)
			&& IsTime(gameMinutes, SpawnHour, SpawnDay, SpawnMonth);
	}

	public bool CanDespawn(int gameMinutes, DayOfWeek serverDayOfWeek)
	{
		// Java parity: model/templates/spawns/TemporarySpawn.canDespawn.
		return (WeekdayMask != 0 && (WeekdayMask & (1 << (int)serverDayOfWeek)) == 0)
			|| IsTime(gameMinutes, DespawnHour, DespawnDay, DespawnMonth);
	}

	public static TemporarySpawnSchedule FromAttributes(string? weekdays, string? spawnTime, string? despawnTime)
	{
		var spawn = ParseTime(spawnTime);
		var despawn = ParseTime(despawnTime);
		return new TemporarySpawnSchedule(
			ParseWeekdays(weekdays),
			spawn.Hour,
			spawn.Day,
			spawn.Month,
			despawn.Hour,
			despawn.Day,
			despawn.Month);
	}

	private static bool IsTime(int gameMinutes, int? hour, int? day, int? month)
	{
		var gameTime = AionGameTime.FromMinutes(gameMinutes);
		return MatchesTimePart(gameTime.Hour, hour)
			&& MatchesTimePart(gameTime.Day, day)
			&& MatchesTimePart(gameTime.Month, month);
	}

	private static bool MatchesTimePart(int current, int? expected)
	{
		if (!expected.HasValue)
			return true;
		var value = expected.Value;
		return value >= 0
			? current == value
			: current % value == 0;
	}

	private static bool CheckDate(int currentDate, int spawnDate, int? despawnDate)
	{
		if (despawnDate is < 0)
			return CheckWithDespawnExpression(currentDate, spawnDate, -despawnDate.Value);
		if (spawnDate < 0)
			spawnDate = -spawnDate;
		if (!despawnDate.HasValue)
			return currentDate >= spawnDate;
		return spawnDate <= despawnDate.Value
			? currentDate >= spawnDate && currentDate <= despawnDate.Value
			: currentDate >= spawnDate || currentDate <= despawnDate.Value;
	}

	private static bool CheckHour(int currentHour, int spawnHour, int? despawnHour)
	{
		if (despawnHour is < 0)
			return CheckWithDespawnExpression(currentHour, spawnHour, -despawnHour.Value);
		if (spawnHour < 0)
			spawnHour = -spawnHour;
		if (!despawnHour.HasValue)
			return currentHour >= spawnHour;
		if (spawnHour < despawnHour.Value)
			return currentHour >= spawnHour && currentHour < despawnHour.Value;
		if (spawnHour > despawnHour.Value)
			return currentHour >= spawnHour || currentHour < despawnHour.Value;
		return true;
	}

	private static bool CheckWithDespawnExpression(int currentDate, int spawnTimeOrExpression, int despawnExpression)
	{
		// Java parity: TemporarySpawn.checkWithDespawnExpression keeps this intentionally narrow.
		if (spawnTimeOrExpression < 0)
			spawnTimeOrExpression = -spawnTimeOrExpression;
		return currentDate >= spawnTimeOrExpression && spawnTimeOrExpression == despawnExpression;
	}

	private static TemporarySpawnTimeParts ParseTime(string? time)
	{
		if (string.IsNullOrWhiteSpace(time))
			return default;

		var parts = time.Split('.');
		return new TemporarySpawnTimeParts(
			ParsePart(parts, 0),
			ParsePart(parts, 1),
			ParsePart(parts, 2));
	}

	private static int? ParsePart(string[] parts, int index)
	{
		if (index >= parts.Length)
			return null;
		var value = parts[index];
		if (string.IsNullOrWhiteSpace(value) || value == "*")
			return null;
		if (value.StartsWith("/", StringComparison.Ordinal))
			value = "-" + value[1..];
		return int.TryParse(value, out var parsed) ? parsed : null;
	}

	private static int ParseWeekdays(string? weekdays)
	{
		if (string.IsNullOrWhiteSpace(weekdays))
			return 0;

		var mask = 0;
		foreach (var value in weekdays.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (Enum.TryParse<DayOfWeek>(value, ignoreCase: true, out var day))
				mask |= 1 << (int)day;
		}

		return mask;
	}

	private readonly record struct TemporarySpawnTimeParts(int? Hour, int? Day, int? Month);

	private readonly record struct AionGameTime(int Hour, int Day, int Month)
	{
		private const int MinutesInHour = 60;
		private const int MinutesInDay = MinutesInHour * 24;
		private const int DaysInMonth = 31;
		private const int MonthsInYear = 12;
		private const int MinutesInMonth = DaysInMonth * MinutesInDay;
		private const int MinutesInYear = MonthsInYear * MinutesInMonth;

		public static AionGameTime FromMinutes(int gameMinutes)
		{
			if (gameMinutes < 0)
				gameMinutes = 0;

			var minutesOfYear = gameMinutes % MinutesInYear;
			var month = minutesOfYear / MinutesInMonth + 1;
			var day = minutesOfYear % MinutesInMonth / MinutesInDay + 1;
			var hour = gameMinutes % MinutesInDay / MinutesInHour;
			return new AionGameTime(hour, day, month);
		}
	}
}
