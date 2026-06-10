using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services.Cron;

/// <summary>Java parity: services/cron/CronService (SoulKeeper, Neon). Quartz-backed cron scheduler singleton: initSingleton (single-thread StdSchedulerFactory), schedule (overloads -> JobDataMap with runnable + long-running flag, unique JobKey, CronTrigger), cancel (by JobDetail or Runnable), findJobDetails/findJobs/getJobTriggers, findNextFireTimes (earliest future fire per runnable). Nested CronScheduleBuilder + MemoryEfficientCronTrigger (re-interns CronExpression on clone). Quartz pillar (Scheduler/JobDetail/JobDataMap/JobKey/JobBuilder/TriggerBuilder/CronTrigger/Trigger/ScheduleBuilder/CronTriggerImpl/StdSchedulerFactory/CronExpression), java.util.Properties/TimeZone/Date, and the Runnable interface are red-tolerated. static synchronized->lock; ConcurrentHashMap n/a; streams->LINQ; Collections.empty*->new; Map.compute->TryGetValue/indexer; currentTimeMillis->UtcNow.ToUnixTimeMilliseconds; nanoTime->Stopwatch.GetTimestamp.</summary>
public sealed class CronService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CronService));

    private static readonly object initLock = new object();
    private static CronService instance;

    private readonly TimeZoneInfo timeZone;
    private readonly Scheduler scheduler;
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
        Properties properties = new Properties();
        properties.SetProperty("org.quartz.threadPool.threadCount", "1");

        try
        {
            scheduler = new StdSchedulerFactory(properties).GetScheduler();
            scheduler.Start();
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
            scheduler.Shutdown(false);
        }
        catch (SchedulerException e)
        {
            log.LogError(e, "Failed to shutdown CronService correctly");
        }
    }

    public JobDetail Schedule(Runnable r, string cronExpression)
    {
        return Schedule(r, cronExpression, false);
    }

    public JobDetail Schedule(Runnable r, string cronExpression, bool longRunning)
    {
        return Schedule(r, CronExpressions.GetOrCreate(cronExpression), longRunning);
    }

    public JobDetail Schedule(Runnable r, CronExpression cronExpression)
    {
        return Schedule(r, cronExpression, false);
    }

    public JobDetail Schedule(Runnable r, CronExpression cronExpression, bool longRunning)
    {
        return Schedule(r, runnableRunner, cronExpression, longRunning);
    }

    public JobDetail Schedule(Runnable r, Type runnableRunner, CronExpression cronExpression, bool longRunning)
    {
        try
        {
            JobDataMap jdm = new JobDataMap();
            jdm.Put(RunnableRunner.KEY_RUNNABLE_OBJECT, r);
            jdm.Put(RunnableRunner.KEY_PROPERTY_IS_LONGRUNNING_TASK, longRunning);

            string jobId = "Started at ms" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "; ns" + System.Diagnostics.Stopwatch.GetTimestamp();
            JobKey jobKey = new JobKey("JobKey:" + jobId);
            JobDetail jobDetail = JobBuilder.NewJob(runnableRunner).UsingJobData(jdm).WithIdentity(jobKey).Build();
            CronTrigger trigger = TriggerBuilder.NewTrigger().WithSchedule(new CronScheduleBuilder(this, cronExpression)).Build();

            scheduler.ScheduleJob(jobDetail, trigger);
            return jobDetail;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Failed to start job", e);
        }
    }

    public bool Cancel(JobDetail jd)
    {
        if (jd == null)
        {
            return false;
        }

        if (jd.GetKey() == null)
        {
            throw new CronServiceException("JobDetail should have JobKey");
        }

        try
        {
            return scheduler.DeleteJob(jd.GetKey());
        }
        catch (SchedulerException e)
        {
            throw new CronServiceException("Failed to delete Job", e);
        }
    }

    public bool Cancel(Runnable r)
    {
        List<JobDetail> jobDetails = FindJobDetails(r);
        if (jobDetails.Count == 0)
            return false;
        bool allCancelled = true;
        foreach (JobDetail jobDetail in jobDetails)
        {
            allCancelled &= Cancel(jobDetail);
        }
        return allCancelled;
    }

    public List<JobDetail> FindJobDetails(Runnable runnable)
    {
        try
        {
            List<JobDetail> jobs = new List<JobDetail>();
            foreach (JobKey jobKey in scheduler.GetJobKeys(null))
            {
                JobDetail jobDetail = scheduler.GetJobDetail(jobKey);
                if (jobDetail.GetJobDataMap().Get(RunnableRunner.KEY_RUNNABLE_OBJECT) == runnable)
                    jobs.Add(jobDetail);
            }
            return jobs;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Can't get all active job details", e);
        }
    }

    public List<Trigger> GetJobTriggers(JobDetail jd)
    {
        return GetJobTriggers(jd.GetKey());
    }

    public List<Trigger> GetJobTriggers(JobKey jk)
    {
        try
        {
            return scheduler.GetTriggersOfJob(jk);
        }
        catch (SchedulerException e)
        {
            throw new CronServiceException("Can't get triggers for JobKey " + jk, e);
        }
    }

    public List<JobDetail> FindJobs<T>(Type runnableType, bool withSubTypes) where T : Runnable
    {
        try
        {
            HashSet<JobKey> keys = scheduler.GetJobKeys(null);
            if (keys.Count == 0)
                return new List<JobDetail>();

            List<JobDetail> jobs = new List<JobDetail>(keys.Count);
            foreach (JobKey jk in keys)
            {
                JobDetail jobDetail = scheduler.GetJobDetail(jk);
                object runnable = jobDetail.GetJobDataMap().Get(RunnableRunner.KEY_RUNNABLE_OBJECT);
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

    public Dictionary<T, Date> FindNextFireTimes<T>(Type runnableType, bool withSubTypes) where T : Runnable
    {
        List<JobDetail> jobs = FindJobs<T>(runnableType, withSubTypes);
        if (jobs.Count == 0)
            return new Dictionary<T, Date>();

        try
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Dictionary<T, Date> nextFireTimes = new Dictionary<T, Date>(jobs.Count);
            foreach (JobDetail job in jobs)
            {
                object runnable = job.GetJobDataMap().Get(RunnableRunner.KEY_RUNNABLE_OBJECT);
                Date nextFireTime = scheduler.GetTriggersOfJob(job.GetKey()).Select(t => t.GetNextFireTime())
                    .Where(d => d != null && d.GetTime() > now).OrderBy(d => d).FirstOrDefault();
                if (nextFireTime != null)
                {
                    T key = (T)runnable;
                    if (!nextFireTimes.TryGetValue(key, out Date oldDate) || oldDate == null || oldDate.After(nextFireTime))
                        nextFireTimes[key] = nextFireTime;
                }
            }
            return nextFireTimes;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Can't get all active job details", e);
        }
    }

    private class CronScheduleBuilder : ScheduleBuilder<CronTrigger>
    {
        private readonly CronService outer;
        private readonly CronExpression cronExpression;

        public CronScheduleBuilder(CronService outer, CronExpression cronExpression)
        {
            this.outer = outer;
            this.cronExpression = cronExpression;
        }

        public override MutableTrigger Build()
        {
            CronTriggerImpl cronTrigger = new MemoryEfficientCronTrigger();
            cronTrigger.SetCronExpression(cronExpression);
            cronTrigger.SetTimeZone(outer.timeZone);
            return cronTrigger;
        }
    }

    private class MemoryEfficientCronTrigger : CronTriggerImpl
    {
        public override object Clone()
        {
            CronTriggerImpl clone = (CronTriggerImpl)base.Clone();
            clone.SetCronExpression(CronExpressions.GetOrCreate(GetCronExpression()));
            return clone;
        }
    }
}
