using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Network.Aion;
using TradeTab = Aion.GameServer.Model.Templates.Tradelist.TradeListTemplate.TradeTab;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TRADE_IN_LIST (MrPoke). Trade-in (sellback) list (npc type/buy modifier + trade tabs). TradeListTemplate.TradeTab aliased; tradeNpcType.index()->Index(). Npc/TradeListTemplate red-tolerated.</summary>
public class SM_TRADE_IN_LIST : AionServerPacket
{
    private Npc npc;
    private TradeListTemplate tlist;
    private int buyPriceModifier;

    public SM_TRADE_IN_LIST(Npc npc, TradeListTemplate tlist, int buyPriceModifier)
    {
        this.npc = npc;
        this.tlist = tlist;
        this.buyPriceModifier = buyPriceModifier;
    }

    protected override void WriteImpl(AionConnection con)
    {
        if ((tlist != null) && (tlist.GetNpcId() != 0) && (tlist.GetCount() != 0))
        {
            WriteD(npc.GetObjectId());
            WriteC(tlist.GetTradeNpcType().Index());
            WriteD(buyPriceModifier); // Vendor Buy Price Modifier
            WriteD(100);// new aion 4.5
            WriteH(tlist.GetCount());
            foreach (TradeTab tradeTabl in tlist.GetTradeTablist())
            {
                WriteD(tradeTabl.GetId());
            }
        }
    }
}
