using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Taskmanager;

/// <summary>Java parity: taskmanager/AbstractFIFOPeriodicTaskManager (lord_rex, MrPoke based on l2j-free engines, Neon).</summary>
public abstract class AbstractFIFOPeriodicTaskManager<T> : AbstractPeriodicTaskManager
{
    private const int WARNING_PERIOD_SECONDS = 10;
    private readonly ConcurrentQueue<T> tasks = new();
    // Java parity: LinkedHashSet (insertion-ordered, unique) — List + Contains-guard preserves both.
    private readonly List<T> processedTasks = new();
    private readonly int counterLimit;
    private int counter = 0;

    public AbstractFIFOPeriodicTaskManager(int periodMillis)
        : base(periodMillis)
    {
        counterLimit = Math.Max(5, WARNING_PERIOD_SECONDS * 1000 / periodMillis);
    }

    public void Add(T t)
    {
        tasks.Enqueue(t);
    }

    protected override void Run()
    {
        lock (this)
        {
            int previouslyProcessedTasksSize = processedTasks.Count;
            processedTasks.Clear();
            for (int i = tasks.Count; i > 0; --i)
            {
                if (!tasks.TryDequeue(out T task)) // no tasks left
                    break;
                if (!processedTasks.Contains(task))
                    processedTasks.Add(task);
            }
            foreach (T task in processedTasks)
            {
                try
                {
                    long begin = System.Diagnostics.Stopwatch.GetTimestamp();
                    CallTask(task);
                    if (Aion.Commons.Configs.CommonsConfig.RUNNABLESTATS_ENABLE)
                    {
                        long duration = (System.Diagnostics.Stopwatch.GetTimestamp() - begin) * 1_000_000_000L / System.Diagnostics.Stopwatch.Frequency;
                        Aion.Commons.Utils.Concurrent.RunnableStatsManager.HandleStats(task.GetType(), GetCalledMethodName(), duration);
                    }
                }
                catch (Exception e)
                {
                    log.LogError(e, "Exception in " + GetType().Name + " processing " + task);
                }
            }
            if (processedTasks.Count <= previouslyProcessedTasksSize)
                counter = 0;
            else if (++counter % counterLimit == 0) // log warning if the task queue size continually increased over the last WARNING_PERIOD_SECONDS
                log.LogWarning("Tasks for " + GetType().Name + " are added faster than they can be executed (currently " + processedTasks.Count + " tasks).");
        }
    }

    protected abstract void CallTask(T task);

    protected abstract string GetCalledMethodName();
}
