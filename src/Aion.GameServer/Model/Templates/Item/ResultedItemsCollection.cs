using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/ResultedItemsCollection (antness).</summary>
[XmlType("ResultedItemsCollection")]
public class ResultedItemsCollection
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlElement("item")] public List<ResultedItem> items;
    [XmlElement("random_item")] public List<RandomItem> randomItems;

    public List<ResultedItem> GetItems()
    {
        return items != null ? items : new List<ResultedItem>();
    }

    public List<RandomItem> GetRandomItems()
    {
        return randomItems != null ? randomItems : new List<RandomItem>();
    }
}
