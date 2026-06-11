using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Limiteditems;
using Aion.GameServer.Model.Templates.Goods;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using TradeTab = Aion.GameServer.Model.Templates.Tradelist.TradeListTemplate.TradeTab;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TRADELIST (alexa026, ATracer, Sarynth, xTz, Neon). Vendor trade list (npc type/buy modifier/tabs filtered by legion level + limited items). ArrayList->new List; addAll->AddRange; TradeTab aliased. Npc/TradeListTemplate/GoodsList/LimitedItem/LimitedItemTradeService red-tolerated.</summary>
public class SM_TRADELIST : AionServerPacket
{
    private int targetObjId;
    private int playerObjId;
    private TradeNpcType tradeNpcType;
    private int buyPriceModifier;
    private bool showBuyTab;
    private bool showSellTab;
    private List<TradeTab> tradeTablist;
    private List<LimitedItem> limitedItems;

    public SM_TRADELIST(Player player, Npc npc, TradeListTemplate tlist, int buyPriceModifier)
    {
        int legionLevel = player.GetLegion() == null ? 0 : player.GetLegion().GetLegionLevel();

        this.targetObjId = npc.GetObjectId();
        this.playerObjId = player.GetObjectId();
        this.tradeNpcType = tlist.GetTradeNpcType();
        this.buyPriceModifier = buyPriceModifier;
        this.showBuyTab = npc.CanSell();
        this.showSellTab = npc.CanBuy();
        this.tradeTablist = new List<TradeTab>();
        this.limitedItems = new List<LimitedItem>();

        foreach (TradeTab tab in tlist.GetTradeTablist())
        {
            GoodsList goodsList = DataManager.GOODSLIST_DATA.GetGoodsListById(tab.GetId());
            if (goodsList == null || goodsList.GetLegionLevel() > legionLevel)
                continue;
            this.tradeTablist.Add(tab);
        }
        LimitedTradeNpc limitedTradeNpc = LimitedItemTradeService.GetInstance().GetLimitedTradeNpc(tlist.GetNpcId());
        if (limitedTradeNpc != null)
            this.limitedItems.AddRange(limitedTradeNpc.GetLimitedItems());
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjId);
        WriteC(tradeNpcType.Index()); // reward, abyss or normal
        WriteD(buyPriceModifier); // Vendor Buy Price Modifier
        WriteD(100);// new aion 4.5
        WriteC(showBuyTab ? 1 : 0);
        WriteC(showSellTab ? 1 : 0);
        WriteH(tradeTablist.Count);
        foreach (TradeTab tradeTabl in tradeTablist)
            WriteD(tradeTabl.GetId());
        WriteH(limitedItems.Count);
        foreach (LimitedItem limitedItem in limitedItems)
        {
            WriteD(limitedItem.GetItemId());
            WriteH(limitedItem.GetBuyCount(playerObjId));
            WriteH(limitedItem.GetSellLimit());
        }
    }
}
