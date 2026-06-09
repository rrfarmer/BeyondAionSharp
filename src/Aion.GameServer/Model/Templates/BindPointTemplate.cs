using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/BindPointTemplate (avol).</summary>
[XmlRoot("bind_points")]
public class BindPointTemplate
{
    [XmlAttribute("name")] private string name;

    [XmlAttribute("npcid")] private int npcId;

    [XmlAttribute("price")] private int price = 0;

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
