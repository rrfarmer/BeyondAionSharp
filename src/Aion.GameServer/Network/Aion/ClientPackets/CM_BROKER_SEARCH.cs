using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BROKER_SEARCH (namedrisk). Searches broker items by item-id list. BrokerService/DialogAction/AuditLogger red-tolerated.</summary>
public class CM_BROKER_SEARCH : AionClientPacket
{
    private int brokerObjId;
    private byte sortType;
    private int page;
    private int mask;
    private List<int> itemList;

    public CM_BROKER_SEARCH(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        brokerObjId = ReadD();
        sortType = ReadC(); // 1 - name; 2 - level; 4 - totalPrice; 6 - price for piece
        page = ReadUH();
        mask = ReadUH();
        int itemCount = ReadUH();
        itemList = new List<int>(itemCount);
        for (int index = 0; index < itemCount; index++)
            itemList.Add(ReadD());
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsTargetingNpcWithFunction(brokerObjId, DialogAction.OPEN_VENDOR))
            BrokerService.GetInstance().ShowRequestedItems(player, mask, sortType, page, itemList);
        else
            AuditLogger.Log(player, "tried to search for items in broker without targeting a broker");
    }
}
