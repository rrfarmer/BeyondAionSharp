using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/BindPointData. @XmlRootElement(bind_points); afterUnmarshal→AfterUnmarshal(object parent).</summary>
[XmlRoot("bind_points")]
public class BindPointData
{
    [XmlElement("bind_point")] private List<BindPointTemplate> bplist;

    [XmlIgnore] private readonly Dictionary<int, BindPointTemplate> bindplistData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (BindPointTemplate bind in bplist)
        {
            bindplistData[bind.GetNpcId()] = bind;
        }
        bplist = null;
    }

    public int Size()
    {
        return bindplistData.Count;
    }

    public BindPointTemplate GetBindPointTemplate(int npcId)
    {
        return bindplistData.TryGetValue(npcId, out var v) ? v : null;
    }
}
