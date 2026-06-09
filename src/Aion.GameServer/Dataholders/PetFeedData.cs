using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Pet;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/PetFeedData (Rolandas). @XmlRootElement(pet_feed); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("pet_feed")]
public class PetFeedData
{
    [XmlElement("flavour")] protected List<PetFlavour> flavours;

    [XmlIgnore] private Dictionary<int, PetFlavour> petFlavoursById = new();

    public void AfterUnmarshal(object parent)
    {
        if (flavours == null)
            return;

        foreach (PetFlavour flavour in flavours)
            petFlavoursById[flavour.GetId()] = flavour;

        flavours.Clear();
        flavours = null;
    }

    public PetFlavour GetFlavourById(int flavourId)
    {
        return petFlavoursById.TryGetValue(flavourId, out var v) ? v : null;
    }

    public int Size()
    {
        return petFlavoursById.Count;
    }

    public PetFlavour[] GetPetFlavours()
    {
        return petFlavoursById.Values.ToArray();
    }
}
