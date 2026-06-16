using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/ExtractedItemsCollection (antness).</summary>
[XmlType("ExtractedItemsCollection")]
public class ExtractedItemsCollection : ResultedItemsCollection, IChance
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlAttribute("chance")] public float chance = 100f;
    [XmlAttribute("minlevel")] public int minLevel;
    [XmlAttribute("maxlevel")] public int maxLevel = 99;

    public float GetChance()
    {
        return chance;
    }

    public int GetMinLevel()
    {
        return minLevel;
    }

    public int GetMaxLevel()
    {
        return maxLevel;
    }
}
