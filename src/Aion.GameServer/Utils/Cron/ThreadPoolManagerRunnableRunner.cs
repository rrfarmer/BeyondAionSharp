using Aion.GameServer.Services.Cron;

namespace Aion.GameServer.Utils.Cron;

/// <summary>
/// Java parity: utils/cron/ThreadPoolManagerRunnableRunner (extends RunnableRunner). Dispatches cron Runnables to ThreadPoolManager.
/// Runnable type + ThreadPoolManager.Execute/ExecuteLongRunning are red-tolerated (same as the RunnableRunner base, which already
/// red-tolerates the Runnable interface and Quartz Job).
/// </summary>
public class ThreadPoolManagerRunnableRunner : RunnableRunner
{
    public override void ExecuteRunnable(Runnable r)
    {
        ThreadPoolManager.GetInstance().Execute(r);
    }

    public override void ExecuteLongRunningRunnable(Runnable r)
    {
        ThreadPoolManager.GetInstance().ExecuteLongRunning(r);
    }
}
