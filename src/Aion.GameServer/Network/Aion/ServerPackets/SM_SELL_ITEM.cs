using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Trade;
using TradeTab = global::Aion.GameServer.Model.Templates.Tradelist.TradeListTemplate.TradeTab;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SELL_ITEM (orz, Sarynth, Artur, Neon). Opens a vendor trade window (npc type/buy rate/buy-sell tabs/trade tabs). TradeListTemplate.TradeTab nested via alias; ArrayList->new List. Npc/TradeListTemplate/TradeNpcType/PricesService red-tolerated.</summary>
public class SM_SELL_ITEM : AionServerPacket
{
    private int targetObjectId;
    private TradeNpcType tradeNpcType;
    private int buyPriceRate;
    private bool showBuyTab;
    private bool showSellTab;
    private List<TradeTab> tradeTabs;

    public SM_SELL_ITEM(Npc npc)
    {
        TradeListTemplate tradeList = DataManager.TRADE_LIST_DATA.GetPurchaseTemplate(npc.GetNpcId());
        this.targetObjectId = npc.GetObjectId();
        this.tradeNpcType = tradeList != null ? tradeList.GetTradeNpcType() : TradeNpcType.NORMAL;
        this.buyPriceRate = tradeList != null ? tradeList.GetBuyPriceRate() : PricesService.GetVendorSellModifier();
        this.showBuyTab = npc.CanSell();
        this.showSellTab = npc.CanBuy() || npc.CanPurchase();
        this.tradeTabs = tradeList != null ? tradeList.GetTradeTablist() : new List<TradeTab>();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjectId);
        WriteC(tradeNpcType.Index());
        WriteD(buyPriceRate); // price * (buyPriceRate / 100) = display price
        WriteC(showBuyTab ? 1 : 0); // npc sells
        WriteC(showSellTab ? 1 : 0); // npc buys
        WriteH(tradeTabs.Count);
        foreach (TradeTab tradeTab in tradeTabs)
            WriteD(tradeTab.GetId());
    }
}
