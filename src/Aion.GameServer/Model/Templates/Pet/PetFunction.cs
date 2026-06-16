using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetFunction (IlBuono).</summary>
[XmlRoot("petfunction")]
public class PetFunction
{
    [XmlAttribute("type")] public PetFunctionType type;
    [XmlAttribute("id")] public int id;
    [XmlAttribute("slots")] public int slots;
    [XmlAttribute("rate_price")] public int ratePrice;

    public PetFunctionType GetPetFunctionType()
    {
        return type;
    }

    public int GetId()
    {
        return id;
    }

    public int GetSlots()
    {
        return slots;
    }

    public int GetRatePrice()
    {
        return ratePrice;
    }

    public static PetFunction CreateEmpty()
    {
        PetFunction result = new PetFunction();
        result.type = PetFunctionType.NONE;
        return result;
    }
}
