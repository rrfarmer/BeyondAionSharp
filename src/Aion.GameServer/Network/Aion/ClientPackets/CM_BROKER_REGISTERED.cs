using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BROKER_REGISTERED (kosyak). Shows the player's registered broker items. BrokerService/DialogAction/AuditLogger red-tolerated.</summary>
public class CM_BROKER_REGISTERED : AionClientPacket
{
    private int brokerObjId;

    public CM_BROKER_REGISTERED(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        brokerObjId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsTargetingNpcWithFunction(brokerObjId, DialogAction.OPEN_VENDOR))
            BrokerService.GetInstance().ShowRegisteredItems(player);
        else
            AuditLogger.Log(player, "tried to view his registered broker items without targeting a broker");
    }
}
