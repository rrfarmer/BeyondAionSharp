using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Model.Templates.Itemset;

/// <summary>Java parity: model/templates/itemset/FullBonus (ATracer).</summary>
[XmlRoot("FullBonus")]
public class FullBonus
{
    [XmlElement("modifiers")] public ModifiersTemplate modifiers;

    private int totalnumberofitems;

    public List<StatFunction> GetModifiers()
    {
        return modifiers != null ? modifiers.GetModifiers() : null;
    }

    /// <returns>Value of the number of items in the set.</returns>
    public int GetCount()
    {
        return totalnumberofitems;
    }

    /// <summary>Sets number of items in the set (when this bonus applies).</summary>
    public void SetNumberOfItems(int number)
    {
        this.totalnumberofitems = number;
    }
}
