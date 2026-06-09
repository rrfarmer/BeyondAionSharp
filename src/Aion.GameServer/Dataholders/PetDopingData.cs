using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Pet;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/PetDopingData. @XmlRootElement(dopings); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("dopings")]
public class PetDopingData
{
    [XmlElement("doping")] private List<PetDopingEntry> list;

    [XmlIgnore] private readonly Dictionary<int, PetDopingEntry> dopingsById = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (PetDopingEntry dope in list)
            dopingsById[dope.GetId()] = dope;
        list = null;
    }

    public int Size()
    {
        return dopingsById.Count;
    }

    public PetDopingEntry GetDopingTemplate(int id)
    {
        return dopingsById.TryGetValue(id, out var v) ? v : null;
    }
}
