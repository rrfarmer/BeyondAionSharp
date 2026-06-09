using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetRewards (Rolandas).</summary>
[XmlType("PetRewards")]
public class PetRewards
{
    [XmlElement("result")] protected List<PetFeedResult> results;
    [XmlAttribute("group")] protected FoodType type;
    [XmlAttribute("loved")] protected bool loved = false;

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
