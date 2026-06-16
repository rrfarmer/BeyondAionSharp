using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Goods;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/GoodsListData (ATracer). @XmlRootElement(goodslists); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("goodslists")]
public class GoodsListData
{
    [XmlElement("list")] public List<GoodsList> list;
    [XmlElement("in_list")] public List<GoodsList> inList;
    [XmlElement("purchase_list")] public List<GoodsList> purchaseList;

    [XmlIgnore] private readonly Dictionary<int, GoodsList> goodsListData = new();
    [XmlIgnore] private readonly Dictionary<int, GoodsList> goodsInListData = new();
    [XmlIgnore] private readonly Dictionary<int, GoodsList> goodsPurchaseListData = new();

    public void AfterUnmarshal(object parent)
    {
        // Java parity: JAXB invokes afterUnmarshal on every unmarshalled object; XmlSerializer does not call
        // nested callbacks, so propagate to each GoodsList (it builds its own itemIdList) to stay 1:1.
        foreach (GoodsList it in list)
        {
            it.AfterUnmarshal(this);
            goodsListData[it.GetId()] = it;
        }
        foreach (GoodsList it in inList)
        {
            it.AfterUnmarshal(this);
            goodsInListData[it.GetId()] = it;
        }
        foreach (GoodsList it in purchaseList)
        {
            it.AfterUnmarshal(this);
            goodsPurchaseListData[it.GetId()] = it;
        }
        list = inList = purchaseList = null;
    }

    public GoodsList GetGoodsListById(int id)
    {
        return goodsListData.TryGetValue(id, out var v) ? v : null;
    }

    public GoodsList GetGoodsInListById(int id)
    {
        return goodsInListData.TryGetValue(id, out var v) ? v : null;
    }

    public GoodsList GetGoodsPurchaseListById(int id)
    {
        return goodsPurchaseListData.TryGetValue(id, out var v) ? v : null;
    }

    public int Size()
    {
        return goodsListData.Count + goodsInListData.Count + goodsPurchaseListData.Count;
    }
}
