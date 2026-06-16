using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/BindPointTemplate (avol).
/// Fields are public so System.Xml.Serialization.XmlSerializer can populate them (JAXB read private
/// fields via @XmlAccessorType(FIELD); XmlSerializer only binds public members). Getters preserved 1:1.</summary>
[XmlRoot("bind_points")]
public class BindPointTemplate
{
    [XmlAttribute("name")] public string name;

    [XmlAttribute("npcid")] public int npcId;

    [XmlAttribute("price")] public int price = 0;

    public string GetName()
    {
        return name;
    }

    public int GetNpcId()
    {
        return npcId;
    }

    public int GetPrice()
    {
        return price;
    }
}
