using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BROKER_LIST (kosyachok). Browses broker items by mask/sort/page. BrokerService/DialogAction/AuditLogger red-tolerated.</summary>
public class CM_BROKER_LIST : AionClientPacket
{
    private int brokerObjId;
    private byte sortType;
    private int page;
    private int listMask;

    public CM_BROKER_LIST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        brokerObjId = ReadD();
        sortType = ReadC(); // 1 - name; 2 - level; 4 - totalPrice; 6 - price for piece
        page = ReadUH();
        listMask = ReadUH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsTargetingNpcWithFunction(brokerObjId, DialogAction.OPEN_VENDOR))
            BrokerService.GetInstance().ShowRequestedItems(player, listMask, sortType, page, null);
        else
            AuditLogger.Log(player, "tried to browse for broker items without targeting a broker");
    }
}
