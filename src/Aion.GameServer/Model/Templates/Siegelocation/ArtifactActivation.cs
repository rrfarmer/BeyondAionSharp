using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/ArtifactActivation (Wakizashi).</summary>
[XmlType("ArtifactActivation")]
public class ArtifactActivation
{
    [XmlAttribute("item_id")] public int itemId;
    [XmlAttribute("count")] public int count;
    [XmlAttribute("skill")] public int skill;
    [XmlAttribute("cd")] public int cd;

    [XmlAttribute("repeat_count")] public int repeatCount = 1;
    [XmlAttribute("repeat_interval")] public int repeatInterval = 1;

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
