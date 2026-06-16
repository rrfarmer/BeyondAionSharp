using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npcskill;

/// <summary>Java parity: model/templates/npcskill/NpcSkillConditionTemplate (Yeats).</summary>
[XmlType("cond")]
public class NpcSkillConditionTemplate
{
    [XmlAttribute("cond_type")] public NpcSkillCondition condType = NpcSkillCondition.NONE;
    [XmlAttribute("hp_below")] public int hpBelow = 50;
    [XmlAttribute("range")] public int range = 10;
    [XmlAttribute("npc_id")] public int npc_id;
    [XmlAttribute("delay")] public int delay;
    [XmlAttribute("can_die")] public bool canDie = true;
    [XmlAttribute("despawn_time")] public int despawn_time = 500;

    public NpcSkillCondition GetCondType()
    {
        return condType;
    }

    public int GetHpBelow()
    {
        return hpBelow;
    }

    public int GetRange()
    {
        return range;
    }

    public int GetNpcId()
    {
        return npc_id;
    }

    public int GetDelay()
    {
        return delay;
    }

    public bool CanDie()
    {
        return canDie;
    }

    public int GetDespawnTime()
    {
        return despawn_time;
    }
}
