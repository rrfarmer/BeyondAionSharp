using System;
using System.Collections.Generic;
using System.Threading;

namespace Aion.GameServer.Network.Sequrity;

/// <summary>
/// Java parity: network/sequrity/NetFlusher (NB4L1). Java single daemon java.util.Timer + TimerTask scheduleAtFixedRate →
/// C# System.Threading.Timer per task (retained in a static list to prevent GC, mirroring the JVM Timer keeping tasks alive).
/// Runnable→Action; RuntimeException→Exception.
/// </summary>
public static class NetFlusher
{
    private static readonly List<Timer> _timers = new();

    public static void Add(Action runnable, long interval)
    {
        Timer timer = new Timer(_ =>
        {
            try
            {
                runnable();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }, null, interval, interval);
        lock (_timers)
        {
            _timers.Add(timer);
        }
    }
}
