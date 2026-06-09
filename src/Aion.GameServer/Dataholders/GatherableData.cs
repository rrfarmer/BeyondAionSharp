using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Gather;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/GatherableData. @XmlRootElement(gatherable_templates); afterUnmarshal→AfterUnmarshal(object); List.sort(null)→List.Sort() (natural order).</summary>
[XmlRoot("gatherable_templates")]
public class GatherableData
{
    [XmlElement("gatherable_template")] private List<GatherableTemplate> gatherables;

    [XmlIgnore] private readonly Dictionary<int, GatherableTemplate> gatherableData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (GatherableTemplate gatherable in gatherables)
        {
            if (gatherable.GetMaterials() != null)
                gatherable.GetMaterials().GetMaterial().Sort();
            if (gatherable.GetExtraMaterials() != null)
                gatherable.GetExtraMaterials().GetMaterial().Sort();
            gatherableData[gatherable.GetTemplateId()] = gatherable;
        }
        gatherables = null;
    }

    public int Size()
    {
        return gatherableData.Count;
    }

    public GatherableTemplate GetGatherableTemplate(int id)
    {
        return gatherableData.TryGetValue(id, out var v) ? v : null;
    }
}
