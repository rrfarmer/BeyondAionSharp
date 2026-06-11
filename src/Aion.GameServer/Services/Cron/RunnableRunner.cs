using System.Threading.Tasks;
using Quartz;
using Aion.Commons.Lang;

namespace Aion.GameServer.Services.Cron;

/// <summary>Java parity: services/cron/RunnableRunner abstract class implements Quartz Job (Quartz.NET IJob).
/// On execute, pulls the Runnable + long-running flag from the JobDataMap and dispatches to
/// executeRunnable/executeLongRunningRunnable. Quartz.NET IJob.Execute is Task-returning; the synchronous Java
/// body is preserved and the completed task returned.</summary>
public abstract class RunnableRunner : IJob
{
    public const string KEY_RUNNABLE_OBJECT = "cronservice.scheduled.runnable.instance";
    public const string KEY_PROPERTY_IS_LONGRUNNING_TASK = "cronservice.scheduled.runnable.islongrunning";

    public Task Execute(IJobExecutionContext context)
    {
        JobDataMap jdm = context.JobDetail.JobDataMap;

        Runnable r = (Runnable)jdm[KEY_RUNNABLE_OBJECT];
        bool longRunning = jdm.GetBoolean(KEY_PROPERTY_IS_LONGRUNNING_TASK);

        if (longRunning)
        {
            ExecuteLongRunningRunnable(r);
        }
        else
        {
            ExecuteRunnable(r);
        }

        return Task.CompletedTask;
    }

    public abstract void ExecuteRunnable(Runnable r);

    public abstract void ExecuteLongRunningRunnable(Runnable r);
}
