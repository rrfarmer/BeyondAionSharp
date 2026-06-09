using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Model.Templates.Cp;

/// <summary>Java parity: model/templates/cp/CPRank (Neon).</summary>
public class CPRank
{
    [XmlAttribute("type")] private CPType type;
    [XmlAttribute("rank_num")] private int rankNum;
    [XmlAttribute("visible_intruder_min_rank")] private int visibleIntruderMinRank;
    [XmlElement("modifiers")] private ModifiersTemplate statModifiers;

    public CPType GetType_()
    {
        return type;
    }

    public int GetRankNum()
    {
        return rankNum;
    }

    public List<StatFunction> GetStatModifiers()
    {
        return statModifiers == null ? new List<StatFunction>() : statModifiers.GetModifiers();
    }

    public int GetVisibleIntruderMinRank()
    {
        return visibleIntruderMinRank;
    }
}
