using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;
using Aion.GameServer.Model.Limiteditems;

namespace Aion.GameServer.Model.Templates.Goods;

/// <summary>Java parity: model/templates/goods/GoodsList (ATracer).</summary>
[XmlType("GoodsList")]
public class GoodsList
{
    [XmlElement("item")] public List<Item> items;
    [XmlAttribute("id")] public int id;
    [XmlAttribute("legion_lvl")] public int legionLevel;
    [XmlAttribute("advertise")] public int advertiseStringId;
    [XmlAttribute("gossip")] public int gossipStringId;
    [XmlElement("salestime")] public string salesTime;

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
        [XmlAttribute("id")] public int id;

        // Java parity: nullable Integer sell_limit / buy_limit. String-proxy: XmlSerializer cannot bind a
        // nullable value type to an [XmlAttribute]; back each with a string and parse it, mirroring JAXB's
        // native nullable-attribute handling — missing attribute -> null, present -> Integer.parseInt.
        [XmlIgnore] private int? sellLimit;
        [XmlIgnore] private int? buyLimit;

        [XmlAttribute("sell_limit")]
        public string SellLimitRaw
        {
            get => sellLimit?.ToString(CultureInfo.InvariantCulture);
            set => sellLimit = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value, CultureInfo.InvariantCulture);
        }

        [XmlAttribute("buy_limit")]
        public string BuyLimitRaw
        {
            get => buyLimit?.ToString(CultureInfo.InvariantCulture);
            set => buyLimit = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value, CultureInfo.InvariantCulture);
        }

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
