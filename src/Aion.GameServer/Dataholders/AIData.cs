using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Ai;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/AIData. @XmlRootElement(ai_templates); uses model/templates/ai/AITemplate POJO (not the AI behavior base); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("ai_templates")]
public class AIData
{
    [XmlElement("ai", typeof(AITemplate))] private List<AITemplate> templates;

    [XmlIgnore] private readonly Dictionary<int, AITemplate> aiTemplate = new();

    public void AfterUnmarshal(object parent)
    {
        aiTemplate.Clear();
        foreach (AITemplate template in templates)
            aiTemplate[template.GetNpcId()] = template;
        templates = null;
    }

    public int Size()
    {
        return aiTemplate.Count;
    }

    public AITemplate GetAiTemplate(int npcId)
    {
        return aiTemplate.TryGetValue(npcId, out var v) ? v : null;
    }
}
