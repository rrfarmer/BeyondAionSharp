using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Ai;

/// <summary>Java parity: model/templates/ai/AITemplate (xTz) — XML data POJO (@XmlType "Ai"). Distinct from Aion.GameServer.Ai.AITemplate (AI behavior base).</summary>
[XmlType("Ai")]
public class AITemplate
{
    [XmlElement("summons")] public Summons summons;
    [XmlElement("bombs")] public Bombs bombs;
    [XmlAttribute("npcId")] public int npcId;

    public Summons GetSummons()
    {
        return summons;
    }

    public Bombs GetBombs()
    {
        return bombs;
    }

    public int GetNpcId()
    {
        return npcId;
    }
}
