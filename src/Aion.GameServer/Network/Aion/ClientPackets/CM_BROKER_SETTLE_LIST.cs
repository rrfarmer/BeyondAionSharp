using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BROKER_SETTLE_LIST (kosyachok). Opens the broker sold-item (settled) list. BrokerService/DialogAction/AuditLogger red-tolerated.</summary>
public class CM_BROKER_SETTLE_LIST : AionClientPacket
{
    private int brokerObjId;
    private int startPageIndex;

    public CM_BROKER_SETTLE_LIST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        brokerObjId = ReadD();
        startPageIndex = ReadUH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsTargetingNpcWithFunction(brokerObjId, DialogAction.OPEN_VENDOR))
            BrokerService.GetInstance().ShowSettledItems(player, startPageIndex);
        else
            AuditLogger.Log(player, "tried to open the broker sold item list without targeting a broker");
    }
}
