using System;
using System.Threading.Tasks;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Taskmanager;

/// <summary>
/// Java parity: taskmanager/AbstractPeriodicTaskManager (lord_rex, MrPoke based on l2j-free engines).
/// This can be used for periodic calls.
/// </summary>
public abstract class AbstractPeriodicTaskManager
{
    protected static readonly ILogger log = NullLogger.Instance;

    public AbstractPeriodicTaskManager(int period)
    {
        log.LogInformation(GetType().Name + " initialized.");
        ThreadPoolManager.GetInstance().ScheduleAtFixedRate(ct =>
        {
            Run();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(Aion.Commons.Utils.Rnd.Get(500, 550)), TimeSpan.FromMilliseconds(period));
    }

    protected abstract void Run();
}
