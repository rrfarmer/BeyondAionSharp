using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/MultiReturnItemData. @XmlRootElement(multi_return_item); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("multi_return_item")]
public class MultiReturnItemData
{
    [XmlElement("return_item")] private List<MultiReturnItem> multiReturnItemTemplate;

    [XmlIgnore] private readonly Dictionary<int, List<ReturnLocList>> returnLocList = new();

    public void AfterUnmarshal(object parent)
    {
        returnLocList.Clear();
        foreach (MultiReturnItem template in multiReturnItemTemplate)
            returnLocList[template.GetId()] = template.GetReturnLocList();
        multiReturnItemTemplate = null;
    }

    public int Size()
    {
        return returnLocList.Count;
    }

    public List<ReturnLocList> GetReturnLocListById(int id)
    {
        return returnLocList.TryGetValue(id, out var v) ? v : null;
    }
}
