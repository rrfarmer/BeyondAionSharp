using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/HouseData (Rolandas). @XmlRootElement(house_lands); put!=null→!TryAdd+throw; afterUnmarshal→AfterUnmarshal(object). NOTE: lands not nulled (matches Java).</summary>
[XmlRoot("house_lands")]
public class HouseData
{
    [XmlElement("land")] private List<HousingLand> lands;

    [XmlIgnore] private Dictionary<int, HouseAddress> addressesById = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (HousingLand land in lands)
        {
            foreach (HouseAddress address in land.GetAddresses())
            {
                if (!addressesById.TryAdd(address.GetId(), address))
                    throw new ArgumentException("Duplicate house address " + address.GetId() + " in house_lands templates");
            }
        }
    }

    public List<HouseAddress> GetAddresses(int worldId)
    {
        return addressesById.Values.Where(address => address.GetMapId() == worldId).ToList();
    }

    public HouseAddress GetAddress(int houseAddress)
    {
        return addressesById.TryGetValue(houseAddress, out var v) ? v : null;
    }

    public HouseAddress GetStudioAddress(Race race)
    {
        return GetAddress(race == Race.ELYOS ? 2001 : 3001);
    }

    public ICollection<HousingLand> GetLands()
    {
        return lands;
    }

    public int Size()
    {
        return lands.Count;
    }
}
