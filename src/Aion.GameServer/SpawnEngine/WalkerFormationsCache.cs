using System.Collections.Concurrent;

namespace Aion.GameServer.Spawnengine;

/// <summary>Java parity: spawnengine/WalkerFormationsCache (Rolandas). Package-private class→internal; ConcurrentHashMap→ConcurrentDictionary; Map.get+null-check+put→TryGetValue+set. InstanceWalkerFormations red-tolerated.</summary>
internal class WalkerFormationsCache
{
    private static ConcurrentDictionary<int, WorldWalkerFormations> formations = new ConcurrentDictionary<int, WorldWalkerFormations>();

    private WalkerFormationsCache()
    {
    }

    internal static InstanceWalkerFormations GetInstanceFormations(int worldId, int instanceId)
    {
        if (!formations.TryGetValue(worldId, out WorldWalkerFormations wwf))
        {
            wwf = new WorldWalkerFormations();
            formations[worldId] = wwf;
        }
        return wwf.GetInstanceFormations(instanceId);
    }

    internal static void OnInstanceDestroy(int worldId, int instanceId)
    {
        GetInstanceFormations(worldId, instanceId).OnInstanceDestroy();
    }
}
