using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Tradelist;

/// <summary>Java parity: model/templates/tradelist/TradeListTemplate (orz).</summary>
[XmlRoot("tradelist_template")]
public class TradeListTemplate
{
    /// <summary>Npc Id.</summary>
    [XmlAttribute("npc_id")] private int npcId;
    [XmlAttribute("npc_type")] private TradeNpcType tradeNpcType = TradeNpcType.NORMAL;
    [XmlAttribute("sell_price_rate")] private int sellPriceRate = 100;
    [XmlAttribute("sell_price_rate2")] private int sellPriceRate2 = 100;
    [XmlAttribute("ap_sell_price_rate2")] private int apSellPriceRate2 = 100;
    [XmlAttribute("buy_price_rate")] private int buyPriceRate;

    // Java parity: nullable Boolean save_count.
    [XmlAttribute("save_count")] private bool? saveCount;

    [XmlElement("tradelist")] protected List<TradeTab> tradeTablist;

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
        [XmlAttribute("id")] protected int id;

        public int GetId()
        {
            return id;
        }
    }
}
