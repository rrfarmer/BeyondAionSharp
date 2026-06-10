namespace Aion.GameServer.Services.Cron;

/// <summary>Java parity: services/cron/CurrentThreadRunnableRunner extends RunnableRunner. Runs the Runnable synchronously on the current (Quartz scheduler) thread; long-running variant delegates to executeRunnable. Runnable interface red-tolerated.</summary>
public class CurrentThreadRunnableRunner : RunnableRunner
{
    public override void ExecuteRunnable(Runnable r)
    {
        r.Run();
    }

    public override void ExecuteLongRunningRunnable(Runnable r)
    {
        ExecuteRunnable(r);
    }
}
