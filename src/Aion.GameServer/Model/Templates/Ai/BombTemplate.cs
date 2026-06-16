using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Ai;

/// <summary>Java parity: model/templates/ai/BombTemplate (xTz).</summary>
[XmlType("BombTemplate")]
public class BombTemplate
{
    [XmlAttribute("skillId")] public int skillId = 0;
    [XmlAttribute("cd")] public int cd = 0;

    public int GetCd()
    {
        return cd;
    }

    public int GetSkillId()
    {
        return skillId;
    }
}
