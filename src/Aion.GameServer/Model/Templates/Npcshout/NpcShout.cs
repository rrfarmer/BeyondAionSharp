using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Npcshout;

/// <summary>Java parity: model/templates/npcshout/NpcShout (Rolandas).</summary>
[XmlType("NpcShout")]
public class NpcShout
{
    [XmlAttribute("string_id")] protected int stringId;

    [XmlAttribute("when")] protected ShoutEventType when;

    [XmlAttribute("pattern")] protected string pattern;

    [XmlAttribute("param")] protected string param;

    // Java parity: nullable Integer; getters collapse null→0, so a plain int (default 0 when attribute absent) is behaviorally faithful.
    [XmlAttribute("skill_no")] protected int skillNo;

    [XmlAttribute("poll_delay")] protected int pollDelay;

    public int GetStringId()
    {
        return stringId;
    }

    public ShoutEventType GetWhen()
    {
        return when;
    }

    public string GetPattern()
    {
        return pattern;
    }

    public string GetParam()
    {
        return param;
    }

    public int GetSkillNo()
    {
        return skillNo;
    }

    public int GetPollDelay()
    {
        return pollDelay;
    }

    public int GetShoutRange(Aion.GameServer.Model.GameObjects.Npc npc)
    {
        return npc.GetObjectTemplate().GetMinimumShoutRange();
    }
}
