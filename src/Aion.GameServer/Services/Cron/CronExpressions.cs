using System;
using System.Collections.Concurrent;

namespace Aion.GameServer.Services.Cron;

/// <summary>Java parity: services/cron/CronExpressions. Process-wide interning cache of parsed CronExpression instances keyed by their string form. ConcurrentHashMap->ConcurrentDictionary; computeIfAbsent->GetOrAdd; java.text.ParseException->FormatException; RuntimeException wrap preserved. Quartz CronExpression red-tolerated.</summary>
public class CronExpressions
{
    private static readonly ConcurrentDictionary<string, CronExpression> cronExpressions = new ConcurrentDictionary<string, CronExpression>();

    private CronExpressions()
    {
    }

    public static CronExpression GetOrCreate(string cronExpression)
    {
        return cronExpressions.GetOrAdd(cronExpression, _ =>
        {
            try
            {
                return new CronExpression(cronExpression);
            }
            catch (FormatException e)
            {
                throw new Exception(e.Message, e);
            }
        });
    }
}
