using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Teleport;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/TeleLocationData. @XmlRootElement(teleport_location); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("teleport_location")]
public class TeleLocationData
{
    [XmlElement("teleloc_template")] public List<TelelocationTemplate> tlist;

    [XmlIgnore] private readonly Dictionary<int, TelelocationTemplate> loctlistData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (TelelocationTemplate loc in tlist)
        {
            loctlistData[loc.GetLocId()] = loc;
        }
        tlist = null;
    }

    public int Size()
    {
        return loctlistData.Count;
    }

    public TelelocationTemplate GetTelelocationTemplate(int id)
    {
        return loctlistData.TryGetValue(id, out var v) ? v : null;
    }
}
