using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Pet;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/PetData (IlBuono). Container of PetTemplate. @XmlRootElement(pets); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("pets")]
public class PetData
{
    [XmlElement("pet")] public List<PetTemplate> pets;

    [XmlIgnore] private readonly Dictionary<int, PetTemplate> petData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (PetTemplate pet in pets)
        {
            petData[pet.GetTemplateId()] = pet;
        }
        pets = null;
    }

    public int Size()
    {
        return petData.Count;
    }

    /// <summary>Returns a PetTemplate object with given id.</summary>
    public PetTemplate GetPetTemplate(int id)
    {
        return petData.TryGetValue(id, out var v) ? v : null;
    }

    public ISet<int> GetPetIds()
    {
        return new HashSet<int>(petData.Keys);
    }
}
