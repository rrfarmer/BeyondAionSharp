using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Ai;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/AIData. @XmlRootElement(ai_templates); uses model/templates/ai/AITemplate POJO (not the AI behavior base); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("ai_templates")]
public class AIData
{
    [XmlElement("ai", typeof(AITemplate))] public List<AITemplate> templates;

    [XmlIgnore] private readonly Dictionary<int, AITemplate> aiTemplate = new();

    public void AfterUnmarshal(object parent)
    {
        aiTemplate.Clear();
        foreach (AITemplate template in templates)
        {
            // Java parity: JAXB fires SummonGroup.afterUnmarshal (minCount/maxCount validation) children-first
            // before the holder's; XmlSerializer skips JAXB callbacks, so fire it here.
            Summons summons = template.GetSummons();
            if (summons?.GetPercentage() != null)
            {
                foreach (Percentage percentage in summons.GetPercentage())
                {
                    if (percentage.GetSummons() == null)
                        continue;
                    foreach (SummonGroup group in percentage.GetSummons())
                        group.AfterUnmarshal(percentage);
                }
            }

            aiTemplate[template.GetNpcId()] = template;
        }
        templates = null;
    }

    /// <summary>
    /// Java parity: the static_data import graph merges every &lt;ai_templates&gt; source file (ai/bombs.xml,
    /// ai/spawn_helpers.xml) into one AIData before afterUnmarshal. XmlSerializer loads a single file, so merge
    /// the &lt;ai&gt; rows from <paramref name="other"/> into this holder's pending list before AfterUnmarshal runs.
    /// </summary>
    public void MergePending(AIData other)
    {
        if (other?.templates == null)
            return;
        templates ??= new List<AITemplate>();
        templates.AddRange(other.templates);
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
