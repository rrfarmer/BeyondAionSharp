using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Ingameshop;

/// <summary>Java parity: model/templates/ingameshop/IGCategory.</summary>
[XmlType("IGCategory")]
public class IGCategory
{
    [XmlElement("sub_category")] protected List<IGSubCategory> subCategories;
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("name")] protected string name;

    public List<IGSubCategory> GetSubCategories()
    {
        if (subCategories == null)
        {
            subCategories = new List<IGSubCategory>();
        }
        return this.subCategories;
    }

    public int GetId()
    {
        return id;
    }

    public string GetName()
    {
        return name;
    }
}
