using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetRewards (Rolandas).</summary>
[XmlType("PetRewards")]
public class PetRewards
{
    // Public so XmlSerializer can populate them (JAXB used private/protected fields via @XmlAccessorType(FIELD)).
    [XmlElement("result")] public List<PetFeedResult> results;
    [XmlAttribute("group")] public FoodType type;
    [XmlAttribute("loved")] public bool loved = false;

    public List<PetFeedResult> GetResults()
    {
        if (results == null)
        {
            results = new List<PetFeedResult>();
        }
        return this.results;
    }

    public FoodType GetType_()
    {
        return type;
    }

    public bool IsLoved()
    {
        return loved;
    }
}
