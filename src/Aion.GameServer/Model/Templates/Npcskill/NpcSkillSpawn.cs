using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npcskill;

/// <summary>Java parity: model/templates/npcskill/NpcSkillSpawn.</summary>
[XmlType("spawn_npc")]
public class NpcSkillSpawn
{
    [XmlAttribute("npc_id")] public int npcId;
    [XmlAttribute("delay")] public int delay;
    [XmlAttribute("min_distance")] public int minDistance;
    [XmlAttribute("max_distance")] public int maxDistance;
    [XmlAttribute("min_count")] public int minCount = 1;
    [XmlAttribute("max_count")] public int maxCount = 0;

    public int GetNpcId()
    {
        return npcId;
    }

    public int GetDelay()
    {
        return delay;
    }

    public int GetMinDistance()
    {
        return minDistance;
    }

    public int GetMaxDistance()
    {
        return maxDistance;
    }

    public int GetMinCount()
    {
        return minCount;
    }

    public int GetMaxCount()
    {
        return maxCount;
    }
}
