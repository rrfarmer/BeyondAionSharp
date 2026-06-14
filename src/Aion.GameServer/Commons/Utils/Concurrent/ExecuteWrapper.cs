using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Concurrent;
using Aion.Commons.Configs;
using Aion.Commons.Lang;

namespace Aion.GameServer.Commons.Utils.Concurrent;

/// <summary>
/// Java parity: commons/utils/concurrent/ExecuteWrapper (NB4L1, Neon) implements Executor.
/// nanoTime -> Stopwatch ticks; TimeUnit.NANOSECONDS.toMillis -> elapsed ms; Throwable -> Exception.
/// </summary>
public class ExecuteWrapper : Executor
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ExecuteWrapper));

    private readonly long expectedMaxExecutionTimeMillis;

    public ExecuteWrapper(long expectedMaxExecutionTimeMillis)
    {
        this.expectedMaxExecutionTimeMillis = expectedMaxExecutionTimeMillis;
    }

    public void Execute(Runnable runnable)
    {
        Execute(runnable, expectedMaxExecutionTimeMillis, true);
    }

    public static void Execute(Runnable runnable, long expectedMaxExecutionTimeMillis, bool catchAndLogThrowables)
    {
        try
        {
            long beginTicks = Stopwatch.GetTimestamp();
            runnable.Run();
            long durationNanos = (long)((Stopwatch.GetTimestamp() - beginTicks) * (1_000_000_000.0 / Stopwatch.Frequency));

            if (CommonsConfig.RUNNABLESTATS_ENABLE)
                RunnableStatsManager.HandleStats(runnable.GetType(), durationNanos);

            long durationMillis = durationNanos / 1_000_000L;
            if (durationMillis > expectedMaxExecutionTimeMillis)
            {
                string name = runnable.GetType().Name;
                log.LogWarning(name + " - execution time: " + durationMillis + "ms");
            }
        }
        catch (Exception t)
        {
            if (catchAndLogThrowables)
                log.LogError(t, "Exception in a Runnable execution:");
            else
                throw;
        }
    }
}
