using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BROKER_SETTLE_ACCOUNT (kosyachok). Collects kinah and unsold items from the broker. BrokerService/DialogAction/AuditLogger red-tolerated.</summary>
public class CM_BROKER_SETTLE_ACCOUNT : AionClientPacket
{
    private int brokerObjId;

    public CM_BROKER_SETTLE_ACCOUNT(int opcode, ISet<State> validStates)
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
            BrokerService.GetInstance().SettleAccount(player);
        else
            AuditLogger.Log(player, "tried to get Kinah and unsold items from the broker without targeting a broker");
    }
}
