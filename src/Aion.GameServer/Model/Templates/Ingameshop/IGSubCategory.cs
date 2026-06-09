using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Ingameshop;

/// <summary>Java parity: model/templates/ingameshop/IGSubCategory.</summary>
[XmlType("IGSubCategory")]
public class IGSubCategory
{
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("name")] protected string name;

    public int GetId()
    {
        return id;
    }

    public string GetName()
    {
        return name;
    }
}
