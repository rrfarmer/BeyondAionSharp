using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/HousePartsData (Rolandas). @XmlRootElement(house_parts); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("house_parts")]
public class HousePartsData
{
    [XmlElement("house_part")] private List<HousePart> houseParts;

    [XmlIgnore] private Dictionary<int, HousePart> partsById = new();

    public void AfterUnmarshal(object parent)
    {
        if (houseParts == null)
            return;

        foreach (HousePart part in houseParts)
            partsById[part.GetId()] = part;
        houseParts = null;
    }

    public HousePart GetPartById(int partId)
    {
        return partsById.TryGetValue(partId, out var v) ? v : null;
    }

    public int Size()
    {
        return partsById.Count;
    }
}
