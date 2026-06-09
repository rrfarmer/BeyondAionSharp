using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Ingameshop;

namespace Aion.GameServer.Configs.Ingameshop;

/// <summary>Java parity: configs/ingameshop/InGameShopProperty.</summary>
[XmlRoot("in_game_shop")]
public class InGameShopProperty
{
    [XmlElement("category")] private List<IGCategory> categories;

    public List<IGCategory> GetCategories()
    {
        if (categories == null)
        {
            categories = new List<IGCategory>();
        }
        return this.categories;
    }

    public int Size()
    {
        return GetCategories().Count;
    }

    public void Clear()
    {
        if (categories != null)
        {
            categories.Clear();
        }
    }

    // Java parity: JAXBUtil.deserialize (deferred JAXB→XmlSerializer bridge, red).
    public static InGameShopProperty Load()
    {
        return (InGameShopProperty) JAXBUtil.Deserialize(new FileInfo("./config/ingameshop/in_game_shop.xml"), typeof(InGameShopProperty));
    }
}
