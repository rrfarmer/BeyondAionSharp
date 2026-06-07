using System.Xml.Serialization;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>
/// Holds the time-window rules for a conditional spawn — weekdays, spawn/despawn time expressions.
/// Java parity: model/templates/spawns/TemporarySpawn.
/// </summary>
/// <remarks>
/// Java uses JAXB @XmlAttribute on List&lt;DayOfWeek&gt; (space-separated in XML) and a
/// afterUnmarshal() callback to parse spawnTime/despawnTime strings into component int? fields.
/// C# XmlSerializer does not support List&lt;T&gt; on [XmlAttribute] or afterUnmarshal callbacks;
/// both are handled via property setters that parse on assignment.
/// </remarks>
[XmlType("TemporarySpawn")]
public class TemporarySpawn
{
    // Backing fields for parsed time components (null = wildcard / any)
    private int? _spawnHour;
    private int? _spawnDay;
    private int? _spawnMonth;
    private int? _despawnHour;
    private int? _despawnDay;
    private int? _despawnMonth;
    private List<DayOfWeek>? _weekdays;

    // Java parity: @XmlAttribute(name = "weekdays") private List<DayOfWeek> weekdays
    // XML value: space-separated Java DayOfWeek names, e.g. "MONDAY TUESDAY FRIDAY"
    // Java DayOfWeek names (ISO-8601) map directly to C# System.DayOfWeek enum values.
    [XmlAttribute("weekdays")]
    public string? WeekdaysRaw
    {
        get => _weekdays == null ? null : string.Join(" ", _weekdays.Select(static d => d.ToString().ToUpperInvariant()));
        set => _weekdays = ParseWeekdays(value);
    }

    // Java parity: @XmlAttribute(name = "spawn_time") private String spawnTime
    // Format: "hour.day.month" — each part is * (any), /n (every nth), or an integer.
    // afterUnmarshal parses it into _spawnHour/_spawnDay/_spawnMonth then nulls the string.
    [XmlAttribute("spawn_time")]
    public string? SpawnTime
    {
        get => null; // nulled after parse (Java parity: spawnTime = null in afterUnmarshal)
        set
        {
            if (value != null)
            {
                _spawnHour = ParseTime(value, 0);
                _spawnDay = ParseTime(value, 1);
                _spawnMonth = ParseTime(value, 2);
            }
        }
    }

    // Java parity: @XmlAttribute(name = "despawn_time") private String despawnTime
    [XmlAttribute("despawn_time")]
    public string? DespawnTime
    {
        get => null; // nulled after parse
        set
        {
            if (value != null)
            {
                _despawnHour = ParseTime(value, 0);
                _despawnDay = ParseTime(value, 1);
                _despawnMonth = ParseTime(value, 2);
            }
        }
    }

    // Java parity: parseTime(String time, int type)
    private static int? ParseTime(string time, int type)
    {
        string result = time.Split('.')[type];
        if (result == "*")
            return null;
        if (result.StartsWith('/'))
            result = "-" + result[1..]; // /2 → -2 (every 2nd)
        return int.Parse(result);
    }

    // Java parity: parseWeekdays — converts space-separated Java DayOfWeek names to C# enum list
    private static List<DayOfWeek>? ParseWeekdays(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                  .Select(static s => s switch
                  {
                      "MONDAY" => DayOfWeek.Monday,
                      "TUESDAY" => DayOfWeek.Tuesday,
                      "WEDNESDAY" => DayOfWeek.Wednesday,
                      "THURSDAY" => DayOfWeek.Thursday,
                      "FRIDAY" => DayOfWeek.Friday,
                      "SATURDAY" => DayOfWeek.Saturday,
                      "SUNDAY" => DayOfWeek.Sunday,
                      _ => throw new ArgumentException($"Unknown DayOfWeek value in XML: '{s}'"),
                  })
                  .ToList();
    }

    // Java parity: isTime(Integer hour, Integer day, Integer month)
    private static bool IsTime(int? hour, int? day, int? month)
    {
        var gameTime = GameTimeService.GetInstance().GetGameTime();
        if (hour != null)
        {
            int h = gameTime.GetHour();
            if (hour >= 0 && h != hour || hour < 0 && h % (-hour.Value) != 0)
                return false;
        }
        if (day != null)
        {
            int d = gameTime.GetDay();
            if (day >= 0 && d != day || day < 0 && d % (-day.Value) != 0)
                return false;
        }
        if (month != null)
        {
            int m = gameTime.GetMonth();
            if (month >= 0 && m != month || month < 0 && m % (-month.Value) != 0)
                return false;
        }
        return true;
    }

    // Java parity: canSpawn()
    public bool CanSpawn()
    {
        if (_weekdays != null && _weekdays.Count > 0 && !_weekdays.Contains(ServerTime.Now().DayOfWeek))
            return false;
        return IsTime(_spawnHour, _spawnDay, _spawnMonth);
    }

    // Java parity: canDespawn()
    public bool CanDespawn()
    {
        if (_weekdays != null && _weekdays.Count > 0 && !_weekdays.Contains(ServerTime.Now().DayOfWeek))
            return true;
        return IsTime(_despawnHour, _despawnDay, _despawnMonth);
    }

    // Java parity: isInSpawnTime()
    public bool IsInSpawnTime()
    {
        if (_weekdays != null && _weekdays.Count > 0 && !_weekdays.Contains(ServerTime.Now().DayOfWeek))
            return false;

        var gameTime = GameTimeService.GetInstance().GetGameTime();

        if (_spawnMonth != null && !CheckDate(gameTime.GetMonth(), _spawnMonth.Value, _despawnMonth))
            return false;
        if (_spawnDay != null && !CheckDate(gameTime.GetDay(), _spawnDay.Value, _despawnDay))
            return false;
        if (_spawnHour != null && !CheckHour(gameTime.GetHour(), _spawnHour.Value, _despawnHour))
            return false;

        return true;
    }

    // Java parity: checkDate(int currentDate, int spawnDate, Integer despawnDate)
    private static bool CheckDate(int currentDate, int spawnDate, int? despawnDate)
    {
        if (despawnDate != null && despawnDate < 0)
            return CheckWithDespawnExpression(currentDate, spawnDate, -despawnDate.Value);
        if (spawnDate < 0)
            spawnDate = -spawnDate;
        if (despawnDate == null)
            return currentDate >= spawnDate;
        if (spawnDate <= despawnDate)
            return currentDate >= spawnDate && currentDate <= despawnDate;
        return currentDate >= spawnDate || currentDate <= despawnDate;
    }

    // Java parity: checkHour(int currentHour, int spawnHour, Integer despawnHour)
    private static bool CheckHour(int currentHour, int spawnHour, int? despawnHour)
    {
        if (despawnHour != null && despawnHour < 0)
            return CheckWithDespawnExpression(currentHour, spawnHour, -despawnHour.Value);
        if (spawnHour < 0)
            spawnHour = -spawnHour;
        if (despawnHour == null)
            return currentHour >= spawnHour;
        if (spawnHour < despawnHour)
            return currentHour >= spawnHour && currentHour < despawnHour;
        if (spawnHour > despawnHour)
            return currentHour >= spawnHour || currentHour < despawnHour;
        return true; // spawnHour == despawnHour
    }

    // Java parity: checkWithDespawnExpression(int currentDate, int spawnTimeOrExpression, int despawnExpression)
    private static bool CheckWithDespawnExpression(int currentDate, int spawnTimeOrExpression, int despawnExpression)
    {
        if (spawnTimeOrExpression < 0)
            spawnTimeOrExpression = -spawnTimeOrExpression;
        return currentDate >= spawnTimeOrExpression && spawnTimeOrExpression == despawnExpression;
    }
}
