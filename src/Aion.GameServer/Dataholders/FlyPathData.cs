using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Flypath;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/FlyPathData. @XmlRootElement(flypath_template); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("flypath_template")]
public class FlyPathData
{
    [XmlElement("flypath_location")] public List<FlyPathEntry> list;

    [XmlIgnore] private readonly Dictionary<int, FlyPathEntry> loctlistData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (FlyPathEntry loc in list)
        {
            loctlistData[loc.GetId()] = loc;
        }
        list = null;
    }

    public int Size()
    {
        return loctlistData.Count;
    }

    public FlyPathEntry GetPathTemplate(int id)
    {
        return loctlistData.TryGetValue(id, out var v) ? v : null;
    }
}
