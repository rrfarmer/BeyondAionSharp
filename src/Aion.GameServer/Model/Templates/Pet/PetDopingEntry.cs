using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetDopingEntry (Rolandas). @XmlType("dope") @XmlAccessorType(NONE).</summary>
[XmlType("dope")]
public class PetDopingEntry
{
    [XmlAttribute("id")] public int id;
    [XmlAttribute("usedrink")] public bool usedrink;
    [XmlAttribute("usefood")] public bool usefood;
    [XmlAttribute("usescroll")] public int usescroll;

    public int GetId()
    {
        return id;
    }

    public bool IsUseDrink()
    {
        return usedrink;
    }

    public bool IsUseFood()
    {
        return usefood;
    }

    public int GetScrollsUsed()
    {
        return usescroll;
    }
}
