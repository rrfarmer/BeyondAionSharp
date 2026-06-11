using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PRIVATE_STORE (Simple). Sets the private-store item list (empty closes the store). PrivateStoreService/TradePSItem red-tolerated.</summary>
public class CM_PRIVATE_STORE : AionClientPacket
{
    private TradePSItem[] tradePSItems;

    public CM_PRIVATE_STORE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        int itemCount = ReadUH();
        tradePSItems = new TradePSItem[itemCount];
        for (int i = 0; i < itemCount; i++)
        {
            int itemObjId = ReadD();
            int itemId = ReadD();
            int count = ReadUH();
            long price = ReadQ();
            tradePSItems[i] = new TradePSItem(itemObjId, itemId, count, price);
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (tradePSItems.Length <= 0)
            PrivateStoreService.ClosePrivateStore(player);
        else
            PrivateStoreService.CreateStoreWithItems(player, tradePSItems);
    }
}
