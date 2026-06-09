using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npcskill;

/// <summary>Java parity: model/templates/npcskill/NpcSkillTemplates (AionChs Master).</summary>
[XmlType("npc_skills")]
public class NpcSkillTemplates
{
    // Java parity: @XmlList @XmlAttribute("npc_ids") List<Integer> — space-separated.
    [XmlIgnore] protected List<int> npcIds;
    [XmlElement("npc_skill")] protected List<NpcSkillTemplate> npcSkills;

    [XmlAttribute("npc_ids")]
    public string NpcIdsXml
    {
        get => npcIds == null ? null : string.Join(" ", npcIds);
        set
        {
            if (value == null) { npcIds = null; return; }
            string[] parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            npcIds = new List<int>(parts.Length);
            foreach (string p in parts)
                npcIds.Add(int.Parse(p));
        }
    }

    public List<int> GetNpcIds()
    {
        return npcIds;
    }

    public List<NpcSkillTemplate> GetNpcSkills()
    {
        return npcSkills;
    }
}
