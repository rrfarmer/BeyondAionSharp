using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Expand;

/// <summary>Java parity: model/templates/expand/Expand (Simple).</summary>
[XmlRoot("expand")]
public class Expand
{
    [XmlAttribute("level")] protected int level;
    [XmlAttribute("price")] protected int price;

    public int GetLevel()
    {
        return level;
    }

    public int GetPrice()
    {
        return price;
    }
}
