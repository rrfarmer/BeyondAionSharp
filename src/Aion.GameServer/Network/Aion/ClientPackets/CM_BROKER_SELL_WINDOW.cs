using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BROKER_SELL_WINDOW (ginho1). Opens the broker sell window for an item. BrokerService red-tolerated.</summary>
public class CM_BROKER_SELL_WINDOW : AionClientPacket
{
    private int itemUniqueId;

    public CM_BROKER_SELL_WINDOW(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        this.itemUniqueId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        if (player.IsTrading())
            return;

        BrokerService.GetInstance().ShowSellWindow(player, itemUniqueId);
    }
}
