using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;

namespace Aion.GameServer.Services.Cron;

/// <summary>
/// Java parity: services/cron/CronService (SoulKeeper, Neon). Quartz-backed cron scheduler singleton.
/// Infrastructure boundary (gameplay-faithful / infra-idiomatic principle): the public API mirrors the Java
/// service (Schedule/Cancel/FindJobDetails/GetJobTriggers/FindJobs/FindNextFireTimes), but the implementation
/// targets Quartz.NET's idiomatic interface+async surface (IScheduler/IJobDetail/ITrigger; Task-returning
/// scheduler ops bridged synchronously at setup time) instead of org.quartz's concrete classes. The bespoke
/// Java CronScheduleBuilder/MemoryEfficientCronTrigger (a clone-time CronExpression re-intern optimisation) is
/// dropped in favour of Quartz.NET's built-in CronScheduleBuilder.CronSchedule. Action overloads wrap a
/// LambdaRunnable so C# lambda/method-group call sites bind exactly as Java's auto-converted Runnable lambdas.
/// </summary>
public sealed class CronService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CronService));

    private static readonly object initLock = new object();
    private static CronService instance;

    private readonly TimeZoneInfo timeZone;
    private readonly IScheduler scheduler;
    private readonly Type runnableRunner;

    public static CronService GetInstance()
    {
        return instance;
    }

    public static void InitSingleton(Type runnableRunner, TimeZoneInfo timeZone)
    {
        lock (initLock)
        {
            if (instance != null)
            {
                throw new CronServiceException("CronService is already initialized");
            }

            instance = new CronService(runnableRunner, timeZone);
        }
    }

    private CronService(Type runnableRunner, TimeZoneInfo timeZone)
    {
        var properties = new NameValueCollection
        {
            ["quartz.threadPool.threadCount"] = "1",
        };

        try
        {
            scheduler = new StdSchedulerFactory(properties).GetScheduler().GetAwaiter().GetResult();
            scheduler.Start().GetAwaiter().GetResult();
        }
        catch (SchedulerException e)
        {
            throw new CronServiceException("Failed to initialize CronService", e);
        }
        if (runnableRunner == null)
        {
            throw new CronServiceException("RunnableRunner class must be defined");
        }

        this.runnableRunner = runnableRunner;
        this.timeZone = timeZone;
    }

    public void Shutdown()
    {
        try
        {
            scheduler.Shutdown(false).GetAwaiter().GetResult();
        }
        catch (SchedulerException e)
        {
            log.LogError(e, "Failed to shutdown CronService correctly");
        }
    }

    public IJobDetail Schedule(Runnable r, string cronExpression)
    {
        return Schedule(r, cronExpression, false);
    }

    public IJobDetail Schedule(Runnable r, string cronExpression, bool longRunning)
    {
        return Schedule(r, CronExpressions.GetOrCreate(cronExpression), longRunning);
    }

    public IJobDetail Schedule(Runnable r, CronExpression cronExpression)
    {
        return Schedule(r, cronExpression, false);
    }

    public IJobDetail Schedule(Runnable r, CronExpression cronExpression, bool longRunning)
    {
        return Schedule(r, runnableRunner, cronExpression, longRunning);
    }

    // Action overloads: C# call sites pass lambdas/method-groups where Java passed Runnable lambdas.
    public IJobDetail Schedule(Action r, string cronExpression) => Schedule(new LambdaRunnable(r), cronExpression, false);

    public IJobDetail Schedule(Action r, string cronExpression, bool longRunning) => Schedule(new LambdaRunnable(r), cronExpression, longRunning);

    public IJobDetail Schedule(Action r, CronExpression cronExpression) => Schedule(new LambdaRunnable(r), cronExpression, false);

    public IJobDetail Schedule(Action r, CronExpression cronExpression, bool longRunning) => Schedule(new LambdaRunnable(r), cronExpression, longRunning);

    public IJobDetail Schedule(Runnable r, Type runnableRunner, CronExpression cronExpression, bool longRunning)
    {
        try
        {
            JobDataMap jdm = new JobDataMap();
            jdm.Put(RunnableRunner.KEY_RUNNABLE_OBJECT, r);
            jdm.Put(RunnableRunner.KEY_PROPERTY_IS_LONGRUNNING_TASK, longRunning);

            string jobId = "Started at ms" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "; ns" + System.Diagnostics.Stopwatch.GetTimestamp();
            JobKey jobKey = new JobKey("JobKey:" + jobId);
            IJobDetail jobDetail = JobBuilder.Create(runnableRunner).UsingJobData(jdm).WithIdentity(jobKey).Build();
            ITrigger trigger = TriggerBuilder.Create()
                .WithSchedule(CronScheduleBuilder.CronSchedule(cronExpression.CronExpressionString).InTimeZone(timeZone))
                .Build();

            scheduler.ScheduleJob(jobDetail, trigger).GetAwaiter().GetResult();
            return jobDetail;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Failed to start job", e);
        }
    }

    public bool Cancel(IJobDetail jd)
    {
        if (jd == null)
        {
            return false;
        }

        if (jd.Key == null)
        {
            throw new CronServiceException("JobDetail should have JobKey");
        }

        try
        {
            return scheduler.DeleteJob(jd.Key).GetAwaiter().GetResult();
        }
        catch (SchedulerException e)
        {
            throw new CronServiceException("Failed to delete Job", e);
        }
    }

    public bool Cancel(Runnable r)
    {
        List<IJobDetail> jobDetails = FindJobDetails(r);
        if (jobDetails.Count == 0)
            return false;
        bool allCancelled = true;
        foreach (IJobDetail jobDetail in jobDetails)
        {
            allCancelled &= Cancel(jobDetail);
        }
        return allCancelled;
    }

    public List<IJobDetail> FindJobDetails(Runnable runnable)
    {
        try
        {
            List<IJobDetail> jobs = new List<IJobDetail>();
            foreach (JobKey jobKey in scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).GetAwaiter().GetResult())
            {
                IJobDetail jobDetail = scheduler.GetJobDetail(jobKey).GetAwaiter().GetResult();
                if (jobDetail.JobDataMap[RunnableRunner.KEY_RUNNABLE_OBJECT] == (object)runnable)
                    jobs.Add(jobDetail);
            }
            return jobs;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Can't get all active job details", e);
        }
    }

    public List<ITrigger> GetJobTriggers(IJobDetail jd)
    {
        return GetJobTriggers(jd.Key);
    }

    public List<ITrigger> GetJobTriggers(JobKey jk)
    {
        try
        {
            return scheduler.GetTriggersOfJob(jk).GetAwaiter().GetResult().ToList();
        }
        catch (SchedulerException e)
        {
            throw new CronServiceException("Can't get triggers for JobKey " + jk, e);
        }
    }

    public List<IJobDetail> FindJobs<T>(bool withSubTypes) where T : Runnable
    {
        Type runnableType = typeof(T);
        try
        {
            var keys = scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).GetAwaiter().GetResult();
            if (keys.Count == 0)
                return new List<IJobDetail>();

            List<IJobDetail> jobs = new List<IJobDetail>(keys.Count);
            foreach (JobKey jk in keys)
            {
                IJobDetail jobDetail = scheduler.GetJobDetail(jk).GetAwaiter().GetResult();
                object runnable = jobDetail.JobDataMap[RunnableRunner.KEY_RUNNABLE_OBJECT];
                if (runnable != null)
                {
                    if (runnableType == runnable.GetType() || withSubTypes && runnableType.IsAssignableFrom(runnable.GetType()))
                        jobs.Add(jobDetail);
                }
            }
            return jobs;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Couldn't collect job details for jobs of type " + runnableType, e);
        }
    }

    public Dictionary<T, DateTimeOffset> FindNextFireTimes<T>(bool withSubTypes) where T : Runnable
    {
        List<IJobDetail> jobs = FindJobs<T>(withSubTypes);
        if (jobs.Count == 0)
            return new Dictionary<T, DateTimeOffset>();

        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Dictionary<T, DateTimeOffset> nextFireTimes = new Dictionary<T, DateTimeOffset>(jobs.Count);
            foreach (IJobDetail job in jobs)
            {
                object runnable = job.JobDataMap[RunnableRunner.KEY_RUNNABLE_OBJECT];
                DateTimeOffset? nextFireTime = scheduler.GetTriggersOfJob(job.Key).GetAwaiter().GetResult()
                    .Select(t => t.GetNextFireTimeUtc())
                    .Where(d => d != null && d.Value > now)
                    .OrderBy(d => d.Value).FirstOrDefault();
                if (nextFireTime != null)
                {
                    T key = (T)runnable;
                    if (!nextFireTimes.TryGetValue(key, out DateTimeOffset oldDate) || oldDate > nextFireTime.Value)
                        nextFireTimes[key] = nextFireTime.Value;
                }
            }
            return nextFireTimes;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Can't get all active job details", e);
        }
    }
}
