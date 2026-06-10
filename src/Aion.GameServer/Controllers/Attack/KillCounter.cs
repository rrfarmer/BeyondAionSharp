using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Main;

namespace Aion.GameServer.Controllers.Attack;

/// <summary>Java parity: controllers/attack/KillCounter (Neon). ConcurrentHashMap→ConcurrentDictionary; computeIfAbsent→GetOrAdd / TryGetValue+init; synchronized→lock; removeIf→RemoveAll; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds. CustomConfig.PVP_DAY_DURATION red-tolerated.</summary>
public class KillCounter
{
    private static readonly ConcurrentDictionary<int, Dictionary<int, List<long>>> PVP_KILL_LISTS = new();

    /// <summary>
    /// Increments the killers kill counter for the victim by one and returns the updated count. Old kills get removed over time, so the returned value
    /// represents only kills in the past 24 hours (configurable, see CustomConfig.PVP_DAY_DURATION).
    /// </summary>
    /// <returns>The count how many times the killer killed given victim.</returns>
    public static int AddKillFor(int killerId, int victimId)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long minAge = now - CustomConfig.PVP_DAY_DURATION;
        Dictionary<int, List<long>> killTimesByVictimId = PVP_KILL_LISTS.GetOrAdd(killerId, k => new Dictionary<int, List<long>>());
        lock (killTimesByVictimId)
        {
            if (!killTimesByVictimId.TryGetValue(victimId, out List<long> killTimes))
            {
                killTimes = new List<long>();
                killTimesByVictimId[victimId] = killTimes;
            }
            killTimes.RemoveAll(time => time < minAge);
            killTimes.Add(now);
            return killTimes.Count;
        }
    }
}
