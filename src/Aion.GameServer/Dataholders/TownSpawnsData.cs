using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Towns;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/TownSpawnsData (ViAl). @XmlRootElement(town_spawns_data); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("town_spawns_data")]
public class TownSpawnsData
{
    [XmlElement("spawn_map")] private List<TownSpawnMap> spawnMap;

    [XmlIgnore] private readonly Dictionary<int, TownSpawnMap> spawnMapsData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (TownSpawnMap map in spawnMap)
            spawnMapsData[map.GetMapId()] = map;
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
