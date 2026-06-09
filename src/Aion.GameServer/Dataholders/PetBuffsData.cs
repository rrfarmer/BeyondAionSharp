using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Pet;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/PetBuffsData. @XmlRootElement(pet_buffs); LinkedHashMap→Dictionary; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("pet_buffs")]
public class PetBuffsData
{
    [XmlElement("buff")] protected List<PetBuff> buffs;

    [XmlIgnore] private Dictionary<int, PetBuff> petBuffsById = new();

    public void AfterUnmarshal(object parent)
    {
        if (buffs == null)
            return;

        foreach (PetBuff buff in buffs)
            petBuffsById[buff.GetId()] = buff;

        buffs.Clear();
        buffs = null;
    }

    public PetBuff GetPetBuff(int buffId)
    {
        return petBuffsById.TryGetValue(buffId, out var v) ? v : null;
    }

    public int Size()
    {
        return petBuffsById.Count;
    }
}
