using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Network.Sequrity;

/// <summary>Java parity: network/sequrity/FloodManager (NB4L1). Per-key sliding-window flood detector. Java non-static inner LogEntry -> nested class with explicit owner ref; ReentrantLock -> lock; TimeUnit.DAYS.toMillis(1) -> 86400000. NetFlusher red-tolerated.</summary>
public sealed class FloodManager
{
    public sealed class FloodFilter
    {
        private readonly int _warnLimit;
        private readonly int _rejectLimit;
        private readonly int _tickLimit;

        public FloodFilter(int warnLimit, int rejectLimit, int tickLimit)
        {
            _warnLimit = warnLimit;
            _rejectLimit = rejectLimit;
            _tickLimit = tickLimit;
        }

        public int GetRejectLimit()
        {
            return _rejectLimit;
        }

        public int GetTickLimit()
        {
            return _tickLimit;
        }

        public int GetWarnLimit()
        {
            return _warnLimit;
        }
    }

    private sealed class LogEntry
    {
        private readonly FloodManager _owner;
        private readonly short[] _ticks;

        private int _lastTick;

        public LogEntry(FloodManager owner)
        {
            _owner = owner;
            _ticks = new short[owner._tickAmount];
            _lastTick = GetCurrentTick();
        }

        public int GetCurrentTick()
        {
            return (int)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ZERO) / _owner._tickLength);
        }

        public bool IsActive()
        {
            return GetCurrentTick() - _lastTick < _owner._tickAmount * 10;
        }

        public Result IsFlooding(bool increment)
        {
            int currentTick = GetCurrentTick();

            if (currentTick - _lastTick >= _ticks.Length)
            {
                _lastTick = currentTick;
                Array.Fill(_ticks, (short)0);
            }
            else if (_lastTick > currentTick)
            {
                _owner.log.LogWarning(new InvalidOperationException(), "The current tick (" + currentTick + ") is smaller than the last (" + _lastTick + ")!");
                _lastTick = currentTick;
            }
            else
                while (currentTick != _lastTick)
                {
                    _lastTick++;
                    _ticks[_lastTick % _ticks.Length] = 0;
                }

            if (increment)
                _ticks[_lastTick % _ticks.Length]++;

            foreach (FloodFilter filter in _owner._filters)
            {
                int previousSum = 0;
                int currentSum = 0;

                for (int i = 0; i <= filter.GetTickLimit(); i++)
                {
                    int value = _ticks[(_lastTick - i) % _ticks.Length];

                    if (i != 0)
                        previousSum += value;

                    if (i != filter.GetTickLimit())
                        currentSum += value;
                }

                if (previousSum > filter.GetRejectLimit() || currentSum > filter.GetRejectLimit())
                    return Result.REJECTED;

                if (previousSum > filter.GetWarnLimit() || currentSum > filter.GetWarnLimit())
                    return Result.WARNED;
            }

            return Result.ACCEPTED;
        }
    }

    public enum Result
    {
        ACCEPTED,
        WARNED,
        REJECTED,
    }

    public static Result Max(Result r1, Result r2)
    {
        if ((int)r1 > (int)r2)
            return r1;

        return r2;
    }

    public readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(FloodManager));

    private static readonly long ZERO = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 86400000;

    private readonly Dictionary<string, LogEntry> _entries = new Dictionary<string, LogEntry>();
    private readonly object _lock = new object();

    private readonly int _tickLength;

    private readonly int _tickAmount;

    private readonly FloodFilter[] _filters;

    public FloodManager(int msecPerTick, params FloodFilter[] filters)
    {
        _tickLength = msecPerTick;
        _filters = filters;

        int max = 1;

        foreach (FloodFilter filter in _filters)
            max = Math.Max(filter.GetTickLimit() + 1, max);

        _tickAmount = max;

        NetFlusher.Add(() => Flush(), 60000);
    }

    private void Flush()
    {
        lock (_lock)
        {
            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, LogEntry> e in _entries)
            {
                if (e.Value.IsActive())
                    continue;

                toRemove.Add(e.Key);
            }
            foreach (string key in toRemove)
                _entries.Remove(key);
        }
    }

    public Result IsFlooding(string key, bool increment)
    {
        if (key == null || key.Length == 0)
            return Result.REJECTED;

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out LogEntry entry))
            {
                entry = new LogEntry(this);

                _entries[key] = entry;
            }

            return entry.IsFlooding(increment);
        }
    }
}
