using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Assemblednpc;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/AssembledNpcsData. @XmlRootElement(assembled_npcs); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("assembled_npcs")]
public class AssembledNpcsData
{
    [XmlElement("assembled_npc", typeof(AssembledNpcTemplate))]
    public List<AssembledNpcTemplate> templates;

    private readonly Dictionary<int, AssembledNpcTemplate> assembledNpcsTemplates = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (AssembledNpcTemplate template in templates)
        {
            assembledNpcsTemplates[template.GetNr()] = template;
        }
        templates = null;
    }

    public int Size()
    {
        return assembledNpcsTemplates.Count;
    }

    public AssembledNpcTemplate GetAssembledNpcTemplate(int i)
    {
        return assembledNpcsTemplates.TryGetValue(i, out var v) ? v : null;
    }
}
