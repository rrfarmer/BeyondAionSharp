using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetFeedResult (Rolandas).</summary>
[XmlType("PetFeedResult")]
public class PetFeedResult
{
    [XmlAttribute("item")] protected int item;

    public int GetItem()
    {
        return item;
    }
}
