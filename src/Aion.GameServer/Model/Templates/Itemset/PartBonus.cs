using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Model.Templates.Itemset;

/// <summary>Java parity: model/templates/itemset/PartBonus (ATracer).</summary>
[XmlRoot("PartBonus")]
public class PartBonus
{
    [XmlAttribute("count")] public int count;
    [XmlElement("modifiers")] public ModifiersTemplate modifiers;

    public List<StatFunction> GetModifiers()
    {
        return modifiers != null ? modifiers.GetModifiers() : null;
    }

    public int GetCount()
    {
        return count;
    }
}
