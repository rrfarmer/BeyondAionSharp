using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetDopingEntry (Rolandas). @XmlType("dope") @XmlAccessorType(NONE).</summary>
[XmlType("dope")]
public class PetDopingEntry
{
    [XmlAttribute("id")] private int id;
    [XmlAttribute("usedrink")] private bool usedrink;
    [XmlAttribute("usefood")] private bool usefood;
    [XmlAttribute("usescroll")] private int usescroll;

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
