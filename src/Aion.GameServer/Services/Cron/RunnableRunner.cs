namespace Aion.GameServer.Services.Cron;

/// <summary>Java parity: services/cron/RunnableRunner abstract class implements Quartz Job. On execute, pulls the Runnable + long-running flag from the JobDataMap and dispatches to executeRunnable/executeLongRunningRunnable. Quartz Job/JobDataMap/JobExecutionContext and the Runnable interface are red-tolerated (deferred pillars).</summary>
public abstract class RunnableRunner : Job
{
    public const string KEY_RUNNABLE_OBJECT = "cronservice.scheduled.runnable.instance";
    public const string KEY_PROPERTY_IS_LONGRUNNING_TASK = "cronservice.scheduled.runnable.islongrunning";

    public void Execute(JobExecutionContext context)
    {
        JobDataMap jdm = context.GetJobDetail().GetJobDataMap();

        Runnable r = (Runnable)jdm.Get(KEY_RUNNABLE_OBJECT);
        bool longRunning = jdm.GetBoolean(KEY_PROPERTY_IS_LONGRUNNING_TASK);

        if (longRunning)
        {
            ExecuteLongRunningRunnable(r);
        }
        else
        {
            ExecuteRunnable(r);
        }
    }

    public abstract void ExecuteRunnable(Runnable r);

    public abstract void ExecuteLongRunningRunnable(Runnable r);
}
