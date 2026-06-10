using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BUY_BROKER_ITEM (kosyak). Buys a broker-listed item by unique id/count. BrokerService/DialogAction/AuditLogger red-tolerated.</summary>
public class CM_BUY_BROKER_ITEM : AionClientPacket
{
    private int brokerObjId;
    private int itemUniqueId;
    private long itemCount;

    public CM_BUY_BROKER_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        brokerObjId = ReadD();
        itemUniqueId = ReadD();
        itemCount = ReadQ();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (itemCount < 1)
            return;
        if (player.IsTargetingNpcWithFunction(brokerObjId, DialogAction.OPEN_VENDOR))
            BrokerService.GetInstance().BuyBrokerItem(player, itemUniqueId, itemCount);
        else
            AuditLogger.Log(player, "tried to buy an item from broker without targeting a broker");
    }
}
