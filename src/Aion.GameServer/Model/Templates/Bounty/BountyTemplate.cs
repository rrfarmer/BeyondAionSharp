using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Bounty;

/// <summary>Java parity: model/templates/bounty/BountyTemplate (Estrayl).</summary>
[XmlType("Bounty")]
public class BountyTemplate
{
    // Public so XmlSerializer can populate them (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("item_id")] public int itemId;
    [XmlAttribute("count")] public int count;

    public int GetItemId()
    {
        return itemId;
    }

    public int GetCount()
    {
        return count;
    }
}
