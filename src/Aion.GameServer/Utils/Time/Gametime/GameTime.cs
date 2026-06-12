namespace Aion.GameServer.Utils.Time.Gametime;

/// <summary>
/// Represents the internal Aion world clock — minutes since midnight 01.01.0000.
/// Java parity: utils/time/gametime/GameTime.
/// </summary>
/// <remarks>
/// Java's private enum Month has 12 members each with an int days value and
/// instance/static methods. Ported as a private static helper class containing
/// a MonthKind enum plus the same methods (C# enums cannot carry fields or methods).
///
/// onHourChange() omits calls to TemporarySpawnEngine.onHourChange() and
/// WeatherService.checkWeathersTime() — both are upward observer callbacks that
/// belong to layers not yet ported. Tracked as backlog items to wire once those
/// services are faithfully ported.
/// </remarks>
public class GameTime : ICloneable
{
    private const int MinutesInHour = 60;
    private const int MinutesInDay = MinutesInHour * 24;

    // Initialised via static method so Month is available at field-init time.
    private static readonly int MinutesInYear = Month.GetDaysOfYear() * MinutesInDay;

    private int _gameTime;
    private DayTime _dayTime;

    // Java parity: private enum Month — in-game calendar with 12 months of 31 days each.
    // C# enums cannot carry fields or instance methods; modelled as a static helper class
    // with a nested enum so calling code mirrors the Java (Month.values(), m.getDays(), etc.).
    private static class Month
    {
        // Java parity: Month constants (all 31 days)
        public enum Kind
        {
            January, February, March, April, May, June,
            July, August, September, October, November, December,
        }

        // Java parity: Month::getDays() — all months are 31 days in Aion's calendar
        public static int GetDays(Kind m) => 31;

        // Java parity: Month::getDaysOfYear() — sum of all months' days (12 × 31 = 372)
        public static int GetDaysOfYear() => 12 * 31;

        // Java parity: Month.values()
        public static IEnumerable<Kind> Values() => (Kind[])Enum.GetValues(typeof(Kind));
    }

    // Java parity: GameTime(Integer time)
    public GameTime(int? time)
    {
        if (time.HasValue && time.Value < 0)
            throw new ArgumentException("Time must be >= 0", nameof(time));
        _gameTime = time ?? 0;
        _dayTime = CalculateDayTime();
    }

    // Java parity: getTime()
    public int GetTime() => _gameTime;

    // Java parity: addMinutes(int minutes)
    public void AddMinutes(int minutes)
    {
        if (minutes != 0)
        {
            _gameTime += minutes;
            if (GetMinute() == 0)
                OnHourChange(minutes == 1);
        }
    }

    // Java parity: getDayTime()
    public DayTime GetDayTime() => _dayTime;

    // Java parity: setDayTime(DayTime) — returns true if changed
    public bool SetDayTime(DayTime dayTime)
    {
        if (_dayTime == dayTime)
            return false;
        _dayTime = dayTime;
        return true;
    }

    // Java parity: getYear()
    public int GetYear() => _gameTime / MinutesInYear;

    // Java parity: getMonth() — 1 to 12
    public int GetMonth()
    {
        int month = 0;
        int minutesOfThisYear = _gameTime % MinutesInYear;
        foreach (var m in Month.Values())
        {
            month += 1;
            minutesOfThisYear -= Month.GetDays(m) * MinutesInDay;
            if (minutesOfThisYear < 0)
                break;
        }
        return month;
    }

    // Java parity: getDay() — 1 to Month.getDays()
    public int GetDay()
    {
        int day = 1;
        int minutesInYear = _gameTime % MinutesInYear;
        foreach (var m in Month.Values())
        {
            int minutesInMonth = Month.GetDays(m) * MinutesInDay;
            if (minutesInYear > minutesInMonth)
            {
                minutesInYear -= minutesInMonth;
            }
            else
            {
                if (minutesInYear < minutesInMonth) // if equal → day 1 of following month
                    day += minutesInYear / MinutesInDay;
                break;
            }
        }
        return day;
    }

    // Java parity: getHour() — 0-23
    public int GetHour() => (_gameTime % MinutesInDay) / MinutesInHour;

    // Java parity: getMinute() — 0-59
    public int GetMinute() => _gameTime % MinutesInHour;

    // Java parity: onHourChange(boolean changedByClock)
    // Upward observer callbacks omitted as backlog:
    //   TODO-backlog: TemporarySpawnEngine.onHourChange() — wire when TemporarySpawnEngine is ported
    //   TODO-backlog: WeatherService.getInstance().checkWeathersTime() — wire when WeatherService is ported
    //   (changedByClock guards the weather call; preserved here for when it's wired)
    private void OnHourChange(bool changedByClock)
    {
        SetDayTime(CalculateDayTime());
        _ = changedByClock; // suppress unused-param warning; used by weather call (backlog)
    }

    // Java parity: calculateDayTime()
    private DayTime CalculateDayTime()
    {
        int hour = GetHour();
        if (hour > 21 || hour < 4)
            return DayTime.NIGHT;
        if (hour > 16)
            return DayTime.EVENING;
        if (hour > 8)
            return DayTime.AFTERNOON;
        return DayTime.MORNING;
    }

    // Java parity: minus(GameTime gt)
    public GameTime Minus(GameTime gt) => new(GetTime() - gt.GetTime());

    // Java parity: plus(GameTime gt)
    public GameTime Plus(GameTime gt) => new(GetTime() + gt.GetTime());

    // Java parity: isGreaterThan(GameTime gt)
    public bool IsGreaterThan(GameTime gt) => GetTime() > gt.GetTime();

    // Java parity: isLessThan(GameTime gt)
    public bool IsLessThan(GameTime gt) => GetTime() < gt.GetTime();

    // Java parity: hashCode()
    public override int GetHashCode()
    {
        const int prime = 31;
        int result = 1;
        result = prime * result + _gameTime;
        return result;
    }

    // Java parity: equals(Object)
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not GameTime other) return false;
        return _gameTime == other._gameTime;
    }

    // Java parity: clone()
    public object Clone() => new GameTime(_gameTime);

    public override string ToString() => $"GameTime[{_gameTime}]";
}
