using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/MultiReturnItem (ginho1).</summary>
[XmlType("MultiReturnItem")]
public class MultiReturnItem
{
    [XmlAttribute("id")] public int id;
    [XmlElement("return_loc")] public List<ReturnLocList> returnLocList;

    public int GetId()
    {
        return id;
    }

    public List<ReturnLocList> GetReturnLocList()
    {
        return returnLocList;
    }
}
