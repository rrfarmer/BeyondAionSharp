using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/Sale (Rolandas).</summary>
[XmlRoot("sale")]
public class Sale
{
    [XmlAttribute("point_price")] protected int pointPrice;

    [XmlAttribute("gold_price")] protected long goldPrice;

    [XmlAttribute("level")] protected int level;

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
