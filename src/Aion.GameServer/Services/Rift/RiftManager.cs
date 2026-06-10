using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Effect;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Rift;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Services.Rift;

/// <summary>Java parity: services/rift/RiftManager (Source). Singleton; static riftsPerWorld (ConcurrentDictionary&lt;int,List&lt;Npc&gt;&gt;) and riftGroups (ConcurrentDictionary&lt;string,SpawnTemplate&gt;); addRiftSpawnTemplate, spawnRift/spawnVortex (RiftEnum lookup), spawnInstance (build Npc, store/position/spawn), addSpawnedRift/getSpawnedRifts/removeSpawnedRift. ConcurrentHashMap->ConcurrentDictionary; CopyOnWriteArrayList->List (snapshot-on-write concurrency dropped); computeIfAbsent->GetOrAdd; Collections.emptyList->new List; rift.name()->RiftEnum.Name(). RVController/SpawnTemplate/VortexLocation/RiftLocation red-tolerated.</summary>
public class RiftManager
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(RiftManager));
    private static readonly ConcurrentDictionary<int, List<Npc>> riftsPerWorld = new ConcurrentDictionary<int, List<Npc>>();
    private static readonly ConcurrentDictionary<string, SpawnTemplate> riftGroups = new ConcurrentDictionary<string, SpawnTemplate>();

    public static void AddRiftSpawnTemplate(SpawnGroup spawn)
    {
        if (spawn.HasPool())
        {
            SpawnTemplate template = spawn.GetSpawnTemplates()[0];
            riftGroups[template.GetAnchor()] = template;
        }
        else
        {
            foreach (SpawnTemplate template in spawn.GetSpawnTemplates())
            {
                riftGroups[template.GetAnchor()] = template;
            }
        }
    }

    public void SpawnRift(RiftLocation loc, bool isWithGuards)
    {
        RiftEnum rift = RiftEnum.GetRift(loc.GetId());
        SpawnRift(rift, null, loc, isWithGuards);
    }

    public void SpawnVortex(VortexLocation loc)
    {
        RiftEnum rift = RiftEnum.GetVortex(loc.GetDefendersRace());
        SpawnRift(rift, loc, null, false);
    }

    private void SpawnRift(RiftEnum rift, VortexLocation vl, RiftLocation rl, bool isWithGuards)
    {
        SpawnTemplate masterTemplate = riftGroups.GetValueOrDefault(rift.GetMaster());
        SpawnTemplate slaveTemplate = riftGroups.GetValueOrDefault(rift.GetSlave());

        if (masterTemplate == null || slaveTemplate == null)
        {
            return;
        }

        int spawned = 0;
        int instanceCount = World.GetInstance().GetWorldMap(masterTemplate.GetWorldId()).GetInstanceCount();

        for (int i = 1; i <= instanceCount; i++)
        {
            if (slaveTemplate.HasPool())
            {
                slaveTemplate = slaveTemplate.ChangeTemplate(i);
            }
            Npc slave = SpawnInstance(i, slaveTemplate, new RVController(null, rift));
            Npc master = SpawnInstance(i, masterTemplate, new RVController(slave, rift, isWithGuards));

            if (rift.IsVortex())
            {
                vl.SetVortexController((RVController)master.GetController());
                spawned = vl.GetSpawned().Count;
                vl.GetSpawned().Add(master);
                vl.GetSpawned().Add(slave);
            }
            else
            {
                spawned = rl.GetSpawned().Count;
                rl.AddSpawned(master);
                rl.AddSpawned(slave);
            }
        }

        log.LogInformation("Rift opened: " + rift.Name() + ", spawned " + spawned + " Npcs (guards=" + isWithGuards + ").");
    }

    private Npc SpawnInstance(int instance, SpawnTemplate template, RVController controller)
    {
        NpcTemplate masterObjectTemplate = DataManager.NPC_DATA.GetNpcTemplate(template.GetNpcId());
        Npc npc = new Npc(controller, template, masterObjectTemplate);

        npc.SetKnownlist(new NpcKnownList(npc));
        npc.SetEffectController(new EffectController(npc));

        World world = World.GetInstance();
        world.StoreObject(npc);
        world.SetPosition(npc, template.GetWorldId(), instance, template.GetX(), template.GetY(), template.GetZ(), template.GetHeading());
        world.Spawn(npc);
        AddSpawnedRift(npc);

        return npc;
    }

    private static void AddSpawnedRift(Npc rift)
    {
        riftsPerWorld.GetOrAdd(rift.GetWorldId(), k => new List<Npc>()).Add(rift);
    }

    public static List<Npc> GetSpawnedRifts(int worldId)
    {
        List<Npc> rifts = riftsPerWorld.GetValueOrDefault(worldId);
        return rifts != null ? rifts : new List<Npc>();
    }

    public static bool RemoveSpawnedRift(Npc rift)
    {
        List<Npc> rifts = riftsPerWorld.GetValueOrDefault(rift.GetWorldId());
        return rifts != null && rifts.Remove(rift);
    }

    public static RiftManager GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    private static class SingletonHolder
    {
        internal static readonly RiftManager INSTANCE = new RiftManager();
    }
}
