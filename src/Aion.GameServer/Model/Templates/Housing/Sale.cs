using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/Sale (Rolandas).</summary>
[XmlRoot("sale")]
public class Sale
{
    [XmlAttribute("point_price")] public int pointPrice;

    [XmlAttribute("gold_price")] public long goldPrice;

    [XmlAttribute("level")] public int level;

    public int GetPointPrice()
    {
        return pointPrice;
    }

    public long GetGoldPrice()
    {
        return goldPrice;
    }

    public int GetMinLevel()
    {
        return level;
    }
}
