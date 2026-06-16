using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Towns;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/TownSpawnsData (ViAl). @XmlRootElement(town_spawns_data); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("town_spawns_data")]
public class TownSpawnsData
{
    [XmlElement("spawn_map")] public List<TownSpawnMap> spawnMap;

    [XmlIgnore] private readonly Dictionary<int, TownSpawnMap> spawnMapsData = new();

    // Java imports the town_spawns/ dir file-by-file (each its own <town_spawns_data> root); the loader merges
    // the per-file spawn_map lists, then runs AfterUnmarshal once on the merged holder.
    public void MergePending(TownSpawnsData other)
    {
        if (other.spawnMap == null)
            return;
        spawnMap ??= new List<TownSpawnMap>();
        spawnMap.AddRange(other.spawnMap);
    }

    public void AfterUnmarshal(object parent)
    {
        if (spawnMap == null)
            return;
        foreach (TownSpawnMap map in spawnMap)
        {
            // XmlSerializer does not invoke child afterUnmarshal callbacks; cascade child-first.
            map.AfterUnmarshal(this);
            spawnMapsData[map.GetMapId()] = map;
        }
        spawnMap = null;
    }

    public int GetSpawnsCount()
    {
        int counter = 0;
        foreach (TownSpawnMap spawnMap in spawnMapsData.Values)
            foreach (TownSpawn townSpawn in spawnMap.GetTownSpawns())
                foreach (TownLevel townLevel in townSpawn.GetTownLevels())
                    counter += townLevel.GetSpawns().Count;
        return counter;
    }

    public List<Spawn> GetSpawns(int townId, int townLevel)
    {
        foreach (TownSpawnMap spawnMap in spawnMapsData.Values)
        {
            if (spawnMap.GetTownSpawn(townId) != null)
            {
                TownSpawn townSpawn = spawnMap.GetTownSpawn(townId);
                return townSpawn.GetSpawnsForLevel(townLevel).GetSpawns();
            }
        }
        return null;
    }

    public int GetWorldIdForTown(int townId)
    {
        foreach (TownSpawnMap spawnMap in spawnMapsData.Values)
            if (spawnMap.GetTownSpawn(townId) != null)
                return spawnMap.GetMapId();
        return 0;
    }

    public void AddAllNpcIdsToSet(ISet<int> npcIds)
    {
        foreach (TownSpawnMap map in spawnMapsData.Values)
            foreach (TownSpawn ts in map.GetTownSpawns())
                foreach (TownLevel tl in ts.GetTownLevels())
                    foreach (Spawn spawn in tl.GetSpawns())
                        npcIds.Add(spawn.GetNpcId());
    }
}
