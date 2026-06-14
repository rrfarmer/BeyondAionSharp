using System;
using System.Collections.Concurrent;

namespace Aion.GameServer.Commons.Utils.Concurrent;

/// <summary>
/// Java parity: commons/utils/concurrent/RunnableStatsManager — accumulates per-class, per-method execution-time
/// statistics (count/total/min/max). Gated at the call sites by CommonsConfig.RUNNABLESTATS_ENABLE (off by default).
/// The aggregation behaviour is preserved 1:1; the synchronized HashMap + manual array growth become ConcurrentDictionary
/// (idiomatic-C# concurrency for unavoidable infra). Class names are stripped of the "com.aionemu.gameserver." prefix
/// for parity with the Java report output.
/// </summary>
public static class RunnableStatsManager
{
    private sealed class MethodStat
    {
        private readonly string className;
        private readonly string methodName;
        private long count;
        private long total;
        private long min = long.MaxValue;
        private long max = long.MinValue;

        internal MethodStat(string className, string methodName)
        {
            this.className = className;
            this.methodName = methodName;
        }

        internal void HandleStats(long runTime)
        {
            lock (this)
            {
                count++;
                total += runTime;
                min = Math.Min(min, runTime);
                max = Math.Max(max, runTime);
            }
        }
    }

    private sealed class ClassStat
    {
        private readonly string className;
        private readonly MethodStat runnableStat;
        private readonly ConcurrentDictionary<string, MethodStat> methodStats = new();

        internal ClassStat(Type clazz)
        {
            className = (clazz.FullName ?? clazz.Name).Replace("com.aionemu.gameserver.", "");
            runnableStat = new MethodStat(className, "run()");
            methodStats["run()"] = runnableStat;
        }

        internal MethodStat GetRunnableStat() => runnableStat;

        internal MethodStat GetMethodStat(string methodName) =>
            methodStats.GetOrAdd(methodName, name => new MethodStat(className, name));
    }

    private static readonly ConcurrentDictionary<Type, ClassStat> classStats = new();

    private static ClassStat GetClassStat(Type clazz) => classStats.GetOrAdd(clazz, c => new ClassStat(c));

    // Java parity: handleStats(Class<? extends Runnable>, long)
    public static void HandleStats(Type clazz, long runTime) =>
        GetClassStat(clazz).GetRunnableStat().HandleStats(runTime);

    // Java parity: handleStats(Class<?>, String methodName, long)
    public static void HandleStats(Type clazz, string methodName, long runTime) =>
        GetClassStat(clazz).GetMethodStat(methodName).HandleStats(runTime);
}
