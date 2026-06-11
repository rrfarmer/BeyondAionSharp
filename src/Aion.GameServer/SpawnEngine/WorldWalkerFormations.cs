using System.Collections.Concurrent;

namespace Aion.GameServer.SpawnEngine;

/// <summary>Java parity: spawnengine/WorldWalkerFormations (Rolandas). ConcurrentHashMap→ConcurrentDictionary; Map.get+null-check+put→TryGetValue+set. Java `protected` method→internal (same-namespace cross-class access). InstanceWalkerFormations red-tolerated.</summary>
public class WorldWalkerFormations
{
    private ConcurrentDictionary<int, InstanceWalkerFormations> formations;

    public WorldWalkerFormations()
    {
        formations = new ConcurrentDictionary<int, InstanceWalkerFormations>();
    }

    internal InstanceWalkerFormations GetInstanceFormations(int instanceId)
    {
        if (!formations.TryGetValue(instanceId, out InstanceWalkerFormations instanceFormation))
        {
            instanceFormation = new InstanceWalkerFormations();
            formations[instanceId] = instanceFormation;
        }
        return instanceFormation;
    }
}
