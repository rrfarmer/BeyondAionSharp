using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_REGISTER_BROKER_ITEM (kosyak). Registers an item for sale on the broker. BrokerService/DialogAction/AuditLogger red-tolerated.</summary>
public class CM_REGISTER_BROKER_ITEM : AionClientPacket
{
    private int brokerObjId;
    private int itemUniqueId;
    private long price;
    private long itemCount;
    private bool splittingAvailable;

    public CM_REGISTER_BROKER_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        brokerObjId = ReadD();
        itemUniqueId = ReadD();
        price = ReadQ();
        itemCount = ReadQ();
        splittingAvailable = ReadC() == 1;
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        if (player.IsTrading() || itemCount <= 0)
            return;

        if (player.IsTargetingNpcWithFunction(brokerObjId, DialogAction.OPEN_VENDOR))
            BrokerService.GetInstance().RegisterItem(player, itemUniqueId, itemCount, price, splittingAvailable);
        else
            AuditLogger.Log(player, "tried to register a broker item without targeting a broker");
    }
}
