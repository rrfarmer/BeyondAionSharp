using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Model.Templates.Npcskill;

/// <summary>Java parity: model/templates/npcskill/NpcSkillTemplate (AionChs Master, nrg, Yeats).</summary>
[XmlType("npc_skill")]
public class NpcSkillTemplate
{
    [XmlAttribute("id")] public int id;
    [XmlAttribute("lv")] public int lv;
    [XmlAttribute("prob")] public int prob;
    [XmlAttribute("min_hp")] public int minHp = 0;
    [XmlAttribute("max_hp")] public int maxHp = 100;
    [XmlAttribute("max_time")] public int maxTime = 0;
    [XmlAttribute("min_time")] public int minTime = 0;
    [XmlAttribute("conjunction")] public ConjunctionType conjunction = ConjunctionType.AND;
    [XmlAttribute("cd")] public int cd = 0;
    [XmlAttribute("is_post_spawn")] public bool is_post_spawn = false;
    [XmlAttribute("prio")] public int prio = 0;
    [XmlAttribute("next_skill_time")] public int nextSkillTime = -1; // -1 = random time between 3s and 9s
    [XmlElement("cond")] public NpcSkillConditionTemplate conditionTemplate;
    [XmlElement("spawn_npc")] public NpcSkillSpawn spawn;
    [XmlAttribute("next_chain_id")] public int nextChainId = 0;
    [XmlAttribute("chain_id")] public int chainId = 0;
    [XmlAttribute("max_chain_time")] public int maxChainTime = 15000;
    [XmlAttribute("target")] public NpcSkillTargetAttribute target = NpcSkillTargetAttribute.MOST_HATED;

    public int GetSkillId()
    {
        return id;
    }

    public int GetSkillLevel()
    {
        return lv;
    }

    public int GetProbability()
    {
        return prob;
    }

    public int GetMinhp()
    {
        return minHp;
    }

    public int GetMaxhp()
    {
        return maxHp;
    }

    public int GetMinTime()
    {
        return minTime;
    }

    public int GetMaxTime()
    {
        return maxTime;
    }

    /// <summary>Gets the value of the conjunction property.</summary>
    public ConjunctionType GetConjunctionType()
    {
        return conjunction;
    }

    public int GetCooldown()
    {
        return cd;
    }

    public bool IsPostSpawn()
    {
        return is_post_spawn;
    }

    public int GetPriority()
    {
        return prio;
    }

    public NpcSkillConditionTemplate GetConditionTemplate()
    {
        return conditionTemplate;
    }

    public NpcSkillSpawn GetSpawn()
    {
        return spawn;
    }

    public SkillTemplate GetSkillTemplate()
    {
        if (id <= 0)
        {
            return null;
        }
        return DataManager.SKILL_DATA.GetSkillTemplate(id);
    }

    public int GetNextSkillTime()
    {
        return nextSkillTime;
    }

    public int GetNextChainId()
    {
        return nextChainId;
    }

    public int GetChainId()
    {
        return chainId;
    }

    public int GetMaxChainTime()
    {
        return maxChainTime;
    }

    public NpcSkillTargetAttribute GetTarget()
    {
        return target;
    }
}
