using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/Cooldowns extends ConcurrentHashMap&lt;Integer, Long&gt;.
/// Foundational C#-vs-Java difference: <see cref="ConcurrentDictionary{TKey,TValue}"/> is sealed, so this
/// wraps one instead of extending it (closest-to-1:1). The Java get/put overrides that auto-expire stale
/// cooldowns are reproduced in <see cref="Get"/> / <see cref="Put"/>. Enumeration/Count delegate to the map.
/// </summary>
public class Cooldowns : IEnumerable<KeyValuePair<int, long>>
{
    private readonly ConcurrentDictionary<int, long> map = new ConcurrentDictionary<int, long>();

    /// <summary>
    /// The time (in millis) when the cooldown for given ID expires. Null if there is no active cooldown.
    /// </summary>
    public long? Get(int cooldownId)
    {
        if (!map.TryGetValue(cooldownId, out long cd))
            return null;
        if (cd <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            if (((ICollection<KeyValuePair<int, long>>)map).Remove(new KeyValuePair<int, long>(cooldownId, cd)))
                return null;
            return Get(cooldownId); // get again, because the retrieved cd isn't up to date
        }
        return cd;
    }

    public long? Put(int cooldownId, long reuseTimeMillis)
    {
        if (reuseTimeMillis <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            return Remove(cooldownId);
        map[cooldownId] = reuseTimeMillis;
        return reuseTimeMillis;
    }

    public long? Remove(int cooldownId)
    {
        return map.TryRemove(cooldownId, out long old) ? old : (long?)null;
    }

    /// <summary>True if there is a not yet expired cooldown for given ID</summary>
    public bool HasCooldown(int cooldownId)
    {
        return map.ContainsKey(cooldownId);
    }

    /// <summary>Remaining time in seconds when this cooldown expires. 0 if there is no cooldown.</summary>
    public int RemainingSeconds(int cooldownId)
    {
        long? cd = Get(cooldownId);
        return cd == null ? 0 : (int)((cd.Value - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
    }

    public int Count => map.Count;

    public IEnumerator<KeyValuePair<int, long>> GetEnumerator() => map.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => map.GetEnumerator();
}
