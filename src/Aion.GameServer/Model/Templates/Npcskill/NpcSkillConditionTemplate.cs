using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npcskill;

/// <summary>Java parity: model/templates/npcskill/NpcSkillConditionTemplate (Yeats).</summary>
[XmlType("cond")]
public class NpcSkillConditionTemplate
{
    [XmlAttribute("cond_type")] protected NpcSkillCondition condType = NpcSkillCondition.NONE;
    [XmlAttribute("hp_below")] protected int hpBelow = 50;
    [XmlAttribute("range")] protected int range = 10;
    [XmlAttribute("npc_id")] protected int npc_id;
    [XmlAttribute("delay")] protected int delay;
    [XmlAttribute("can_die")] protected bool canDie = true;
    [XmlAttribute("despawn_time")] protected int despawn_time = 500;

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
