using System;
using System.Threading;
using Aion.GameServer.Dao;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace Aion.GameServer.Taskmanager;

/// <summary>
/// Java parity: taskmanager/AbstractCronTask (Rolandas, Neon). implements Runnable→Run(); Quartz CronExpression;
/// Semaphore→SemaphoreSlim (acquireUninterruptibly→Wait); java.util.Date→DateTimeOffset (getTimeAfter→Quartz
/// GetTimeAfter(.Value); getTime→ToUnixTimeMilliseconds; new Date()→UtcNow; new Date(ms)→FromUnixTimeMilliseconds);
/// getClass().getSimpleName()→GetType().Name; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds. ServerVariablesDAO/
/// CronService/ThreadPoolManager red-tolerated.
/// </summary>
public abstract class AbstractCronTask
{
    protected static readonly long? SERVER_STOP_MILLIS = ServerVariablesDAO.LoadLong("serverLastRun");
    private static readonly SemaphoreSlim semaphore = new(1, 1);
    protected readonly ILogger log = NullLogger.Instance;
    private readonly CronExpression cronExpression;
    private DateTimeOffset lastPlannedRunBeforeServerStart;
    private DateTimeOffset? lastRun;
    private DateTimeOffset nextRun;

    public AbstractCronTask(CronExpression cronExpression)
    {
        this.cronExpression = cronExpression;
        if (this.cronExpression == null)
        {
            log.LogInformation(GetType().Name + " is deactivated");
            return;
        }
        this.nextRun = GetNextRunAfter(DateTimeOffset.UtcNow);
        this.lastPlannedRunBeforeServerStart = FindLastPlannedRun();
        RunAndScheduleAsyncWithLock();
    }

    /// <summary>
    /// Runs this task in another thread, so that constructor can finish and external references to this task don't throw null pointer exceptions.
    /// This additionally makes sure that other tasks don't run before a previous one is finished, so multiple tasks are initialized semi-synchronous.
    /// </summary>
    private void RunAndScheduleAsyncWithLock()
    {
        semaphore.Wait();
        ThreadPoolManager.GetInstance().ExecuteLongRunning(() =>
        {
            if (ShouldRunOnStart())
                Run();
            CronService.GetInstance().Schedule(this, cronExpression, true);
            log.LogInformation("Scheduled " + GetType().Name + " with cron expression: " + cronExpression);
            semaphore.Release();
        });
    }

    /// <returns>Default implementation returns true if the server was down when task should have run</returns>
    protected virtual bool ShouldRunOnStart()
    {
        return SERVER_STOP_MILLIS != null && SERVER_STOP_MILLIS < lastPlannedRunBeforeServerStart.ToUnixTimeMilliseconds();
    }

    /// <returns>The last time this task started, null if it didn't during this uptime yet</returns>
    public DateTimeOffset? GetLastRun()
    {
        return lastRun;
    }

    /// <returns>The last time this task started or should have started (in case task hasn't yet run since the server got restarted)</returns>
    public DateTimeOffset GetLastPlannedRun()
    {
        return lastRun == null ? lastPlannedRunBeforeServerStart : lastRun.Value;
    }

    public long GetMillisSinceLastRun()
    {
        return lastRun == null ? -1 : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastRun.Value.ToUnixTimeMilliseconds();
    }

    /// <returns>Time of the next task start</returns>
    public DateTimeOffset GetNextRun()
    {
        return nextRun;
    }

    /// <returns>Time of the next task start after given date</returns>
    public DateTimeOffset GetNextRunAfter(DateTimeOffset date)
    {
        return cronExpression.GetTimeAfter(date).Value;
    }

    public long GetMillisUntilNextRun()
    {
        return nextRun.ToUnixTimeMilliseconds() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    protected abstract void ExecuteTask();

    public void Run()
    {
        lastRun = DateTimeOffset.UtcNow;
        nextRun = GetNextRunAfter(lastRun.Value);
        ExecuteTask();
    }

    /// <returns>
    /// Date when this task last should have run, whether the server was online or not. NOTE: The current implementation may not find the
    /// correct date if the underlying cron expression is irregular
    /// </returns>
    private DateTimeOffset FindLastPlannedRun()
    {
        long interval = GetNextRunAfter(nextRun).ToUnixTimeMilliseconds() - nextRun.ToUnixTimeMilliseconds();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long millis = now;
        DateTimeOffset lastRun;
        do
        {
            millis -= interval / 2;
            lastRun = cronExpression.GetTimeAfter(DateTimeOffset.FromUnixTimeMilliseconds(millis)).Value;
        } while (lastRun.ToUnixTimeMilliseconds() >= now);
        return lastRun;
    }
}
