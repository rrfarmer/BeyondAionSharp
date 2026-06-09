using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Limiteditems;

namespace Aion.GameServer.Model.Templates.Goods;

/// <summary>Java parity: model/templates/goods/GoodsList (ATracer).</summary>
[XmlType("GoodsList")]
public class GoodsList
{
    [XmlElement("item")] private List<Item> items;
    [XmlAttribute("id")] private int id;
    [XmlAttribute("legion_lvl")] private int legionLevel;
    [XmlAttribute("advertise")] private int advertiseStringId;
    [XmlAttribute("gossip")] private int gossipStringId;
    [XmlElement("salestime")] private string salesTime;

    [XmlIgnore] private List<int> itemIdList;

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        itemIdList = new List<int>();
        if (items == null)
            return;

        foreach (Item item in items)
        {
            itemIdList.Add(item.GetId());
        }
    }

    /// <summary>return the limitedItems.</summary>
    public List<LimitedItem> GetLimitedItems()
    {
        List<LimitedItem> limitedItems = new List<LimitedItem>();
        if (items != null)
        {
            foreach (Item item in items)
            {
                if (item.GetBuyLimit() != null && item.GetSellLimit() != null)
                {
                    limitedItems.Add(new LimitedItem(item.GetId(), item.GetSellLimit().Value, item.GetBuyLimit().Value, salesTime));
                }
            }
        }
        return limitedItems;
    }

    public int GetId()
    {
        return id;
    }

    public List<int> GetItemIdList()
    {
        return itemIdList;
    }

    public int GetLegionLevel()
    {
        return legionLevel;
    }

    public int GetAdvertiseStringId()
    {
        return advertiseStringId;
    }

    public int GetGossipStringId()
    {
        return gossipStringId;
    }

    [XmlType("")]
    public class Item
    {
        [XmlAttribute("id")] private int id;
        [XmlAttribute("sell_limit")] private int? sellLimit;
        [XmlAttribute("buy_limit")] private int? buyLimit;

        public int GetId()
        {
            return id;
        }

        public int? GetSellLimit()
        {
            return sellLimit;
        }

        public int? GetBuyLimit()
        {
            return buyLimit;
        }
    }
}
