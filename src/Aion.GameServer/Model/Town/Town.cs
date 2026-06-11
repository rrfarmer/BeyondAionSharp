using System;
using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Housing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Model.Town;

/// <summary>Java parity: model/town/Town (ViAl).</summary>
public class Town : IPersistable, IL10n
{
    private int id;
    private int level;
    private int points;
    private DateTime? levelUpDate;
    private Race race;
    private IPersistable.PersistentState persistentState;
    private List<Npc> spawnedNpcs;

    /// <summary>Used only from DAO.</summary>
    public Town(int id, int level, int points, Race race, DateTime? levelUpDate)
    {
        this.id = id;
        this.level = level;
        this.points = points;
        this.levelUpDate = levelUpDate;
        this.race = race;
        this.persistentState = IPersistable.PersistentState.UPDATED;
        this.spawnedNpcs = new List<Npc>();
        SpawnNewObjects();
        GeoService.GetInstance().UpdateTown(this.race, this.id, this.level);
    }

    /// <summary>Used for initial import from house templates.</summary>
    public Town(int id, Race race)
        : this(id, 1, 0, race, DateTimeOffset.FromUnixTimeMilliseconds(60000).UtcDateTime)
    {
        this.persistentState = IPersistable.PersistentState.NEW;
    }

    public int GetId()
    {
        return id;
    }

    public int GetL10nId()
    {
        int idOffset = id - (race == Race.ELYOS ? 1001 : 2001);
        return (race == Race.ELYOS ? 403330 : 403360) + idOffset;
    }

    public int GetLevel()
    {
        return level;
    }

    public int GetPoints()
    {
        return points;
    }

    public void IncreasePoints(int amount)
    {
        lock (this)
        {
            switch (this.level)
            {
                case 1:
                    if (this.points + amount >= 1000)
                        IncreaseLevel();
                    break;
                case 2:
                    if (this.points + amount >= 2000)
                        IncreaseLevel();
                    break;
                case 3:
                    if (this.points + amount >= 3000)
                        IncreaseLevel();
                    break;
                case 4:
                    if (this.points + amount >= 4000)
                        IncreaseLevel();
                    break;
            }
            this.points += amount;
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        }
    }

    private void IncreaseLevel()
    {
        this.level++;
        this.levelUpDate = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).UtcDateTime;
        BroadcastUpdate();
        DespawnOldObjects();
        SpawnNewObjects();
        GeoService.GetInstance().UpdateTown(this.race, this.id, this.level);
    }

    private void BroadcastUpdate()
    {
        Dictionary<int, Town> data = new Dictionary<int, Town>(1);
        data[this.id] = this;
        SmTownsList packet = new SmTownsList(data);
        Aion.GameServer.World.World.GetInstance().ForEachPlayer(player =>
        {
            if (player.GetRace() == race)
                PacketSendUtility.SendPacket(player, packet);
        });
    }

    private void SpawnNewObjects()
    {
        List<Spawn> newSpawns = DataManager.TOWN_SPAWNS_DATA.GetSpawns(id, level);
        int worldId = DataManager.TOWN_SPAWNS_DATA.GetWorldIdForTown(id);
        foreach (Spawn spawn in newSpawns)
        {
            SpawnGroup spawnGroup = new SpawnGroup(worldId, spawn);
            foreach (SpawnSpotTemplate sst in spawn.GetSpawnSpotTemplates())
            {
                Npc obj = (Npc) SpawnEngine.SpawnObject(new TownSpawnTemplate(spawnGroup, sst, id), 1);
                spawnedNpcs.Add(obj);
            }
        }
    }

    private void DespawnOldObjects()
    {
        foreach (Npc npc in spawnedNpcs)
            npc.GetController().Delete();
        spawnedNpcs.Clear();
    }

    public Race GetRace()
    {
        return this.race;
    }

    public DateTime? GetLevelUpDate()
    {
        return levelUpDate;
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(IPersistable.PersistentState state)
    {
        if (this.persistentState != IPersistable.PersistentState.NEW || state != IPersistable.PersistentState.UPDATE_REQUIRED)
            this.persistentState = state;
    }
}
