using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Calc.Functions;

namespace Aion.GameServer.Model.Templates.Stats;

/// <summary>
/// Java parity: model/templates/stats/ModifiersTemplate. Polymorphic list of stat functions.
/// </summary>
public class ModifiersTemplate
{
    [XmlElement("sub", typeof(StatSubFunction))]
    [XmlElement("add", typeof(StatAddFunction))]
    [XmlElement("rate", typeof(StatRateFunction))]
    [XmlElement("set", typeof(StatSetFunction))]
    [XmlElement("abs", typeof(StatAbsFunction))]
    public List<StatFunction> modifiers;

    [XmlAttribute("chance")]
    public float chance = 100f;

    public List<StatFunction> GetModifiers()
    {
        return modifiers;
    }

    public float GetChance()
    {
        return chance;
    }
}
