using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BROKER_CANCEL_REGISTERED (kosyachok). Unregisters a registered broker item. BrokerService/DialogAction/AuditLogger red-tolerated.</summary>
public class CM_BROKER_CANCEL_REGISTERED : AionClientPacket
{
    private int brokerObjId;
    private int brokerItemId;

    public CM_BROKER_CANCEL_REGISTERED(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        brokerObjId = ReadD();
        brokerItemId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsTargetingNpcWithFunction(brokerObjId, DialogAction.OPEN_VENDOR))
            BrokerService.GetInstance().CancelRegisteredItem(player, brokerItemId);
        else
            AuditLogger.Log(player, "tried to unregister his registered broker item without targeting a broker");
    }
}
