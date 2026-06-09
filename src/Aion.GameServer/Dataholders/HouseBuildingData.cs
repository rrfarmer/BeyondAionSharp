using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/HouseBuildingData (Rolandas). @XmlType("Ai")-style anonymous (no @XmlRootElement); afterUnmarshal→AfterUnmarshal(object); put!=null→!TryAdd+throw.</summary>
public class HouseBuildingData
{
    [XmlElement("building")] private List<Building> buildings;

    [XmlIgnore] private Dictionary<int, Building> buildingById = new();

    public void AfterUnmarshal(object parent)
    {
        if (buildings == null)
            return;

        foreach (Building building in buildings)
        {
            if (!buildingById.TryAdd(building.GetId(), building))
                throw new ArgumentException("Duplicate building ID " + building.GetId());
        }
        buildings = null;
    }

    public Building GetBuilding(int buildingId)
    {
        return buildingById.TryGetValue(buildingId, out var v) ? v : null;
    }

    public int Size()
    {
        return buildingById.Count;
    }
}
