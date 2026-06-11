using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Mercenaries;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/MercenaryLocation (Whoop). A purchasable mercenary spawn zone tied to a fortress siege: spawn (despawn old, spawn from MercenaryZone, announce), despawnCurrentMercs, isRequestValid (cooldown + &lt;50% alive), getSpawnZone (resolve merc zone by siege/race/zone id). currentTimeMillis->UtcNow; Race.getRaceByString(siegeRace.name())->GetRaceByString(ToString()); instanceof Npc->is Npc; forEachPlayer lambda. MercenaryZone/SiegeMercenaryZone/SpawnEngine red-tolerated.</summary>
public class MercenaryLocation
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(MercenaryLocation));
    private List<VisibleObject> spawnedMercs = new List<VisibleObject>();
    private MercenaryZone spawns; // TODO: Change this to SpawnGroup
    private SiegeMercenaryZone smz;
    private Race race;
    private long lastSpawn;
    private int siegeId;

    public MercenaryLocation(SiegeMercenaryZone template, SiegeRace race, int siegeId)
    {
        this.smz = template;
        this.siegeId = siegeId;
        this.race = Race.GetRaceByString(race.ToString());
        if (this.race != null)
            spawns = GetSpawnZone();
    }

    public void Spawn()
    {
        DespawnCurrentMercs();
        if (spawns == null)
            return;
        List<VisibleObject> mercs = new List<VisibleObject>();
        foreach (Spawn spawn in spawns.GetSpawns())
        {
            foreach (SpawnSpotTemplate sst in spawn.GetSpawnSpotTemplates())
            {
                SpawnTemplate spawnTemplate = SpawnEngine.NewSiegeSpawn(spawns.GetWorldId(), spawn.GetNpcId(), siegeId, SiegeRace.GetByRace(race),
                    SiegeModType.SIEGE, sst.GetX(), sst.GetY(), sst.GetZ(), sst.GetHeading());
                spawnTemplate.SetStaticId(sst.GetStaticId());
                VisibleObject newMerc = SpawnEngine.SpawnObject(spawnTemplate, 1);
                mercs.Add(newMerc);
            }
        }
        spawnedMercs.AddRange(mercs);
        lastSpawn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SiegeService.GetInstance().GetFortress(siegeId).ForEachPlayer(p => PacketSendUtility.SendPacket(p, new SM_SYSTEM_MESSAGE(smz.GetAnnounceId())));
    }

    public void DespawnCurrentMercs()
    {
        if (spawnedMercs.Count == 0)
            return;
        foreach (VisibleObject merc in spawnedMercs)
        {
            merc.GetController().DeleteIfAliveOrCancelRespawn();
        }
        spawnedMercs.Clear();
    }

    /// <summary>
    /// Returns true if cooldown is expired and enough mercs are dead
    /// </summary>
    public bool IsRequestValid()
    {
        return spawns != null && (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastSpawn) > smz.GetCooldown() && !HasEnoughMercsAlive();
    }

    /// <summary>
    /// Check if enough mercs are still alive. Returns false if 50% are alive.
    /// </summary>
    private bool HasEnoughMercsAlive()
    {
        int totalMercs = spawnedMercs.Count;
        int livingMercs = 0;
        foreach (VisibleObject vo in spawnedMercs)
        {
            if (vo is Npc)
            {
                if (vo.IsSpawned() && !((Npc)vo).IsDead())
                    livingMercs++;
            }
        }
        return livingMercs < (totalMercs / 2);
    }

    private MercenaryZone GetSpawnZone()
    {
        MercenaryZone tempZone = null;
        MercenarySpawn spawn = DataManager.SPAWNS_DATA.GetMercenarySpawnBySiegeId(siegeId);
        if (spawn == null)
        {
            log.LogError("[MERC] There is no mercenaries spawns for siege " + siegeId + " and zone" + smz.GetId());
            return tempZone;
        }
        MercenaryRace targetRace = null;
        foreach (MercenaryRace mrace in spawn.GetMercenaryRaces())
        {
            if (mrace.GetRace() == race)
            {
                targetRace = mrace;
                break;
            }
        }
        if (targetRace == null)
        {
            log.LogError("[MERC] There is no mercenary race for siege " + siegeId + ", zone" + smz.GetId() + ", race:" + race.ToString());
            return tempZone;
        }
        foreach (MercenaryZone mzone in targetRace.GetMercenaryZones())
        {
            if (mzone.GetZoneId() == smz.GetId())
            {
                tempZone = mzone;
                break;
            }
        }
        if (tempZone == null)
        {
            log.LogError("[MERC] There is no mercenary zone for siege " + siegeId + ", zone" + smz.GetId() + ", race:" + race.ToString());
            return tempZone;
        }
        return tempZone;
    }

    public int GetCosts()
    {
        return smz.GetCosts();
    }

    public int GetMsgId()
    {
        return smz.GetMsgId();
    }
}
