using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BUY_TRADE_IN_TRADE (MrPoke, Ritsu). Buys an item from a trade-in NPC, surrendering trade-in items. TradeService red-tolerated.</summary>
public class CM_BUY_TRADE_IN_TRADE : AionClientPacket
{
    private int sellerObjId;
    private byte mask;
    private int itemId;
    private int count;
    private int tradeInListCount;
    private List<int> tradeInItemObjIds;

    public CM_BUY_TRADE_IN_TRADE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        tradeInItemObjIds = new List<int>();
        sellerObjId = ReadD();
        mask = ReadC(); // NEW - TODO find out what this is!
        itemId = ReadD();
        count = ReadD();
        tradeInListCount = ReadUH();
        for (int i = 0; i < tradeInListCount; i++)
            tradeInItemObjIds.Add(ReadD());
    }

    protected override void RunImpl()
    {
        Player player = this.GetConnection().GetActivePlayer();
        if (count < 1)
            return;

        TradeService.PerformBuyFromTradeInTrade(player, sellerObjId, itemId, count, tradeInItemObjIds);
    }
}
