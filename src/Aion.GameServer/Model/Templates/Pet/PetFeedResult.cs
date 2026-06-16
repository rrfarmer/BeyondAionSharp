using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetFeedResult (Rolandas).</summary>
[XmlType("PetFeedResult")]
public class PetFeedResult
{
    // Public so XmlSerializer can populate it (JAXB used a protected field via @XmlAccessorType(FIELD)).
    [XmlAttribute("item")] public int item;

    public int GetItem()
    {
        return item;
    }
}
