using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/CubeExpandData (Cube Expanders). @XmlRootElement(cube_expander); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("cube_expander")]
public class CubeExpandData
{
    // Public so XmlSerializer can populate it (JAXB used a private field via @XmlAccessorType(FIELD)).
    [XmlElement("expansion_npc")] public List<StorageExpansionTemplate> expansionTemplates;

    [XmlIgnore] private readonly Dictionary<int, StorageExpansionTemplate> expansionTemplatesByNpcId = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (StorageExpansionTemplate expansionTemplate in expansionTemplates)
        {
            foreach (int npcId in expansionTemplate.GetNpcIds())
                expansionTemplatesByNpcId[npcId] = expansionTemplate;
        }
        expansionTemplates = null;
    }

    public int Size()
    {
        return expansionTemplatesByNpcId.Count;
    }

    public StorageExpansionTemplate GetCubeExpansionTemplate(int id)
    {
        return expansionTemplatesByNpcId.TryGetValue(id, out var v) ? v : null;
    }
}
