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
        // Java parity: JAXB leaves summons null when <summonGroup> is absent. C# XmlSerializer materializes an empty
        // list, which SummonerAI's `getSummons() != null` branch would enter (running handleBeforeSpawn on a
        // non-individual percent with no summons). Normalize empty -> null so the null-check matches Java.
        return summons == null || summons.Count == 0 ? null : summons;
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
