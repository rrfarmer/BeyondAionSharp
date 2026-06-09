using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Road;
using Aion.GameServer.Model.Templates.Road;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/RoadService (SheppeR).</summary>
public class RoadService
{
    private readonly ILogger log = NullLogger.Instance;

    private static class SingletonHolder
    {
        internal static readonly RoadService instance = new RoadService();
    }

    public static RoadService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private RoadService()
    {
        foreach (RoadTemplate rt in DataManager.ROAD_DATA.GetRoadTemplates())
        {
            foreach (int instanceId in Aion.GameServer.World.World.GetInstance().GetWorldMap(rt.GetMap()).GetAvailableInstanceIds())
            {
                Road r = new Road(rt, instanceId);
                r.Spawn();
                log.LogDebug("Added " + r.Name + " at m=" + r.GetWorldId() + ",x=" + r.GetX() + ",y=" + r.GetY() + ",z=" + r.GetZ() + " [" + instanceId
                    + "]");
            }
        }
    }
}
