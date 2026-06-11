using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Walker;

namespace Aion.GameServer.SpawnEngine;

/// <summary>
/// Java parity: spawnengine/WalkerFormator (vlog, Rolandas). Forms walker groups on initial spawn; brings NPCs back if they die. slf4j logger→ILogger (warn→LogWarning). WalkerTemplate/WalkerGroup/DataManager red-tolerated.
/// </summary>
public class WalkerFormator
{
    private static readonly ILogger Log = NullLoggerFactory.Instance.CreateLogger(nameof(WalkerFormator));

    /// <summary>
    /// If it's the instance first spawn, WalkerFormator verifies and creates groups; OrganizeAndSpawn() must be called after to speed up
    /// spawning. If it's a respawn, nothing to verify, then the method places NPC to the first step and resets data to the saved, no organizing is
    /// needed.
    /// </summary>
    /// <returns>true if npc was brought into world by the method call.</returns>
    public static bool ProcessClusteredNpc(Npc npc, int worldId, int instanceId)
    {
        string walkerId = npc.GetSpawn().GetWalkerId();
        if (walkerId != null)
        {
            WalkerTemplate template = DataManager.WALKER_DATA.GetWalkerTemplate(walkerId);
            if (template == null)
            {
                Log.LogWarning("Missing walker ID: " + walkerId);
                return false;
            }
            if (template.GetPool() < 2)
                return false;

            InstanceWalkerFormations formations = WalkerFormationsCache.GetInstanceFormations(worldId, instanceId);
            WalkerGroup wg = formations.GetSpawnWalkerGroup(walkerId);

            if (wg != null)
            {
                npc.SetWalkerGroup(wg);
                wg.Respawn(npc);
                return true;
            }

            return formations.CacheWalkerCandidate(new ClusteredNpc(npc, instanceId, template));
        }
        return false;
    }

    /// <summary>
    /// Organizes spawns in all processed walker groups. Must be called only when spawning all npcs for the instance of world.
    /// </summary>
    public static void OrganizeAndSpawn(int worldId, int instanceId)
    {
        InstanceWalkerFormations formations = WalkerFormationsCache.GetInstanceFormations(worldId, instanceId);
        formations.OrganizeAndSpawn();
    }

    public static void ChangeWalkerGroup(int worldId, int instanceId, WalkerGroup walkerGroup)
    {
        InstanceWalkerFormations formations = WalkerFormationsCache.GetInstanceFormations(worldId, instanceId);
        formations.ChangeCluster(walkerGroup);
    }

    public static void OnInstanceDestroy(int worldId, int instanceId)
    {
        WalkerFormationsCache.OnInstanceDestroy(worldId, instanceId);
    }
}
