using System;

namespace Aion.GameServer.Utils;

/// <summary>Java parity: utils/TimeUtil.</summary>
public class TimeUtil
{
    public static bool IsExpired(long time)
    {
        return time < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
