using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Npcskill;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/NpcSkillData (ATracer). @XmlRootElement(npc_skill_templates); putIfAbsent→!TryAdd+warn; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("npc_skill_templates")]
public class NpcSkillData
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlElement("npc_skills")] private List<NpcSkillTemplates> npcSkills;

    [XmlIgnore] private readonly Dictionary<int, NpcSkillTemplates> npcSkillData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (NpcSkillTemplates npcSkillList in npcSkills)
        {
            foreach (int npcId in npcSkillList.GetNpcIds())
            {
                if (!npcSkillData.TryAdd(npcId, npcSkillList))
                    log.LogWarning("Npc " + npcId + " has multiple skill lists in npc_skills.xml");
            }
        }
        npcSkills = null;
    }

    public int Size()
    {
        return npcSkillData.Count;
    }

    public NpcSkillTemplates GetNpcSkillList(int id)
    {
        return npcSkillData.TryGetValue(id, out var v) ? v : null;
    }

    public void SetNpcSkillTemplates(List<NpcSkillTemplates> template)
    {
        this.npcSkills = template;
        npcSkillData.Clear();
        AfterUnmarshal(null);
    }

    public ICollection<NpcSkillTemplates> GetAllNpcSkillTemplates()
    {
        return npcSkillData.Values;
    }
}
