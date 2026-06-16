using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Tradelist;

/// <summary>Java parity: model/templates/tradelist/TradeListTemplate (orz).</summary>
[XmlRoot("tradelist_template")]
public class TradeListTemplate
{
    /// <summary>Npc Id.</summary>
    [XmlAttribute("npc_id")] public int npcId;
    [XmlAttribute("npc_type")] public TradeNpcType tradeNpcType = TradeNpcType.NORMAL;
    [XmlAttribute("sell_price_rate")] public int sellPriceRate = 100;
    [XmlAttribute("sell_price_rate2")] public int sellPriceRate2 = 100;
    [XmlAttribute("ap_sell_price_rate2")] public int apSellPriceRate2 = 100;
    [XmlAttribute("buy_price_rate")] public int buyPriceRate;

    // Java parity: nullable Boolean save_count. XmlSerializer cannot bind bool?; round-trip through a string
    // proxy (absent attribute -> null, 1:1 with JAXB).
    [XmlIgnore] private bool? saveCount;

    [XmlAttribute("save_count")]
    public string SaveCountRaw
    {
        get => saveCount?.ToString().ToLowerInvariant();
        set => saveCount = value == null ? null : bool.Parse(value);
    }

    [XmlElement("tradelist")] public List<TradeTab> tradeTablist;

    public List<TradeTab> GetTradeTablist()
    {
        if (tradeTablist == null)
            tradeTablist = new List<TradeTab>();
        return this.tradeTablist;
    }

    public int GetNpcId()
    {
        return npcId;
    }

    public int GetCount()
    {
        return tradeTablist.Count;
    }

    public TradeNpcType GetTradeNpcType()
    {
        return tradeNpcType;
    }

    public int GetSellPriceRate()
    {
        return sellPriceRate;
    }

    public int GetSellPriceRate2()
    {
        return sellPriceRate2;
    }

    public int GetApSellPriceRate2()
    {
        return apSellPriceRate2;
    }

    public int GetBuyPriceRate()
    {
        return buyPriceRate;
    }

    public bool? IsSaveCount()
    {
        return saveCount;
    }

    [XmlType("Tradelist")]
    public class TradeTab
    {
        [XmlAttribute("id")] public int id;

        public int GetId()
        {
            return id;
        }
    }
}
