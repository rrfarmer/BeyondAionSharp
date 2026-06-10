using System;
using Aion.GameServer.Instance;
using Aion.GameServer.Instance.Handlers;

namespace Aion.GameServer.World;

/// <summary>Java parity: world/WorldMapInstanceFactory (ATracer). Method-ref getNewInstanceHandler→method group; Function→Func; WorldMapType.RESHANTA.getId()→GetId() extension. InstanceEngine/WorldMap2D/3DInstance red-tolerated.</summary>
public class WorldMapInstanceFactory
{
    public static WorldMapInstance CreateWorldMapInstance(WorldMap parent, int maxPlayers)
    {
        return CreateWorldMapInstance(parent, 0, InstanceEngine.GetInstance().GetNewInstanceHandler, maxPlayers);
    }

    public static WorldMapInstance CreateWorldMapInstance(WorldMap parent, int ownerId, Func<WorldMapInstance, InstanceHandler> instanceHandlerSupplier, int maxPlayers)
    {
        WorldMapInstance instance;
        if (parent.GetMapId() == WorldMapType.RESHANTA.GetId())
        {
            instance = new WorldMap3DInstance(parent, parent.GetNextInstanceId(), maxPlayers, instanceHandlerSupplier);
        }
        else
        {
            instance = new WorldMap2DInstance(parent, parent.GetNextInstanceId(), ownerId, maxPlayers, instanceHandlerSupplier);
        }
        parent.AddInstance(instance.GetInstanceId(), instance);
        return instance;
    }
}
