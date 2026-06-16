using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Expand;

/// <summary>Java parity: model/templates/expand/Expand (Simple).</summary>
[XmlRoot("expand")]
public class Expand
{
    // Public so XmlSerializer can populate them (JAXB used private/protected fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("level")] public int level;
    [XmlAttribute("price")] public int price;

    public int GetLevel()
    {
        return level;
    }

    public int GetPrice()
    {
        return price;
    }
}
