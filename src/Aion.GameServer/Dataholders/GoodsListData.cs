using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Goods;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/GoodsListData (ATracer). @XmlRootElement(goodslists); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("goodslists")]
public class GoodsListData
{
    [XmlElement("list")] protected List<GoodsList> list;
    [XmlElement("in_list")] protected List<GoodsList> inList;
    [XmlElement("purchase_list")] protected List<GoodsList> purchaseList;

    [XmlIgnore] private readonly Dictionary<int, GoodsList> goodsListData = new();
    [XmlIgnore] private readonly Dictionary<int, GoodsList> goodsInListData = new();
    [XmlIgnore] private readonly Dictionary<int, GoodsList> goodsPurchaseListData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (GoodsList it in list)
        {
            goodsListData[it.GetId()] = it;
        }
        foreach (GoodsList it in inList)
        {
            goodsInListData[it.GetId()] = it;
        }
        foreach (GoodsList it in purchaseList)
        {
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
