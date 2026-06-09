using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Taskmanager;

namespace Aion.GameServer.Taskmanager.Tasks;

/// <summary>Java parity: taskmanager/tasks/ExpireTimerTask (Mr. Poke).</summary>
public class ExpireTimerTask : AbstractPeriodicTaskManager
{
    private readonly ConcurrentDictionary<IExpirable, Aion.GameServer.Model.GameObjects.Player.Player> expirables = new();

    public ExpireTimerTask()
        : base(1000)
    {
    }

    public static ExpireTimerTask GetInstance()
    {
        return SingletonHolder.instance;
    }

    public void RegisterExpirable(IExpirable expirable, Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        RegisterExpirables(new List<IExpirable> { expirable }, player);
    }

    public void RegisterExpirables(IEnumerable<IExpirable> expirables, Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        foreach (IExpirable expirable in expirables)
        {
            if (expirable.GetExpireTime() > 0)
                this.expirables[expirable] = player;
        }
    }

    public void UnregisterExpirables(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        foreach (KeyValuePair<IExpirable, Aion.GameServer.Model.GameObjects.Player.Player> entry in expirables.Where(entry => player.Equals(entry.Value)).ToList())
            expirables.TryRemove(entry.Key, out _);
    }

    protected override void Run()
    {
        int timeNow = (int)(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
        foreach (KeyValuePair<IExpirable, Aion.GameServer.Model.GameObjects.Player.Player> entry in expirables)
        {
            IExpirable expirable = entry.Key;
            Aion.GameServer.Model.GameObjects.Player.Player player = entry.Value;
            int remainingSeconds = expirable.GetExpireTime() - timeNow;
            if (remainingSeconds < 0 && expirable.CanExpireNow())
            {
                expirable.OnExpire(player);
                expirables.TryRemove(expirable, out _);
            }
            else
            {
                switch (remainingSeconds)
                {
                    case 1800:
                    case 900:
                    case 600:
                    case 300:
                    case 60:
                        expirable.OnBeforeExpire(player, remainingSeconds / 60);
                        break;
                }
            }
        }
    }

    private static class SingletonHolder
    {
        internal static readonly ExpireTimerTask instance = new ExpireTimerTask();
    }
}
