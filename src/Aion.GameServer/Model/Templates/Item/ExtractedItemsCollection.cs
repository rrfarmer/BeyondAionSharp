using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/ExtractedItemsCollection (antness).</summary>
[XmlType("ExtractedItemsCollection")]
public class ExtractedItemsCollection : ResultedItemsCollection, IChance
{
    [XmlAttribute("chance")] private float chance = 100f;
    [XmlAttribute("minlevel")] private int minLevel;
    [XmlAttribute("maxlevel")] private int maxLevel = 99;

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
