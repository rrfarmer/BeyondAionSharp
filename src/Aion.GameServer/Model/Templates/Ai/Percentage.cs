using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Ai;

/// <summary>Java parity: model/templates/ai/Percentage (xTz).</summary>
[XmlType("Percentage")]
public class Percentage
{
    [XmlAttribute("percent")] public int percent;
    [XmlAttribute("skillId")] public int skillId = 0;
    [XmlAttribute("isIndividual")] public bool isIndividual = false;
    [XmlElement("summonGroup")] public List<SummonGroup> summons;

    public List<SummonGroup> GetSummons()
    {
        return summons;
    }

    public int GetPercent()
    {
        return percent;
    }

    public int GetSkillId()
    {
        return skillId;
    }

    public bool IsIndividual()
    {
        return isIndividual;
    }
}
