using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.Templates.Flyring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/FlyRingService (xavier).</summary>
public class FlyRingService
{
    private readonly ILogger log = NullLogger.Instance;

    private static class SingletonHolder
    {
        internal static readonly FlyRingService instance = new FlyRingService();
    }

    public static FlyRingService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private FlyRingService()
    {
        foreach (FlyRingTemplate t in DataManager.FLY_RING_DATA.GetFlyRingTemplates())
        {
            FlyRing f = new FlyRing(t, 0);
            f.Spawn();
            log.LogDebug("Added " + f.Name + " at m=" + f.GetWorldId() + ",x=" + f.GetX() + ",y=" + f.GetY() + ",z=" + f.GetZ());
        }
    }
}
