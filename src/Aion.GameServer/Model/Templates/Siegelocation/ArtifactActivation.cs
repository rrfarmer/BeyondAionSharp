using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/ArtifactActivation (Wakizashi).</summary>
[XmlType("ArtifactActivation")]
public class ArtifactActivation
{
    [XmlAttribute("item_id")] protected int itemId;
    [XmlAttribute("count")] protected int count;
    [XmlAttribute("skill")] protected int skill;
    [XmlAttribute("cd")] protected int cd;

    [XmlAttribute("repeat_count")] protected int repeatCount = 1;
    [XmlAttribute("repeat_interval")] protected int repeatInterval = 1;

    public int GetItemId()
    {
        return itemId;
    }

    public int GetCount()
    {
        return count;
    }

    public int GetSkillId()
    {
        return skill;
    }

    public long GetCd()
    {
        return cd * 1000;
    }

    public int GetRepeatCount()
    {
        return repeatCount;
    }

    public int GetRepeatInterval()
    {
        return repeatInterval;
    }
}
