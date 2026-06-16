using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Spawns;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/HouseNpcsData (Rolandas). @XmlRootElement(house_npcs); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("house_npcs")]
public class HouseNpcsData
{
    [XmlElement("house")] public List<HouseSpawns> houseSpawnsData;

    [XmlIgnore] private Dictionary<int, List<HouseSpawn>> houseSpawnsByAddressId = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (HouseSpawns houseSpawns in houseSpawnsData)
        {
            HashSet<SpawnType> spawnTypes = new();
            houseSpawnsByAddressId[houseSpawns.GetAddress()] = houseSpawns.GetSpawns();
            foreach (HouseSpawn spawn in houseSpawns.GetSpawns())
            {
                if (!spawnTypes.Add(spawn.GetType_()))
                    throw new ArgumentException("Duplicate " + spawn.GetType_() + " spawn for house " + houseSpawns.GetAddress());
            }
        }
        houseSpawnsData = null;
    }

    public List<HouseSpawn> GetSpawnsByAddress(int address)
    {
        return houseSpawnsByAddressId.TryGetValue(address, out var v) ? v : null;
    }

    public int Size()
    {
        return houseSpawnsByAddressId.Values.Sum(l => l.Count);
    }
}
