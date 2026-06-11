namespace Aion.GameServer.Controllers.Effects;

/// <summary>
/// Per-target diminishing-returns tracker for a cumulative abnormal-state resistance.
/// Java parity: controllers/effect/CumulativeResist (package-private → internal).
/// </summary>
internal sealed class CumulativeResist
{
    private int _level;
    private long _expirationTime;

    // Java parity: tryIncrementLevel(long)
    internal void TryIncrementLevel(long maxDurationMillis)
    {
        ResetIfExpired();
        if (_level < 5)
            _level++;
        _expirationTime = NowMillis() + maxDurationMillis;
    }

    // Java parity: getDurationMultiplier() — time_value* from repeated_abnormal_status_immune.xml retail file
    internal float GetDurationMultiplier() => _level switch
    {
        0 or 1 => 1,
        2 => 0.9f,
        3 => 0.85f,
        4 => 0.8f,
        _ => 0,
    };

    // Java parity: getCooldownTimeOffset(CumulativeResistType) — holding_time2 from retail file
    internal int GetCooldownTimeOffset(CumulativeResistType type) => type switch
    {
        CumulativeResistType.SLEEP or CumulativeResistType.PARALYZE => 0,
        CumulativeResistType.FEAR => 2000,
        _ => 0,
    };

    // Java parity: getResistance() — resist_value* from retail file
    internal int GetResistance()
    {
        ResetIfExpired();
        return _level switch
        {
            0 or 1 or 2 => 0,
            3 => 200,
            4 => 400,
            _ => 1000,
        };
    }

    // Java parity: resetIfExpired()
    private void ResetIfExpired()
    {
        if (_level > 0 && NowMillis() > _expirationTime)
            _level = 0;
    }

    // Java parity: System.currentTimeMillis()
    private static long NowMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
