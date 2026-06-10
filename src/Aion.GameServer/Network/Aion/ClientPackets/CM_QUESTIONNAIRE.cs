using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_QUESTIONNAIRE (xTz). Submits a questionnaire/HTML reward selection. HTMLService red-tolerated.</summary>
public class CM_QUESTIONNAIRE : AionClientPacket
{
    private int objectId;
    private int itemId;
    private string stringItemsId;
    private int itemSize;
    private List<int> items;

    public CM_QUESTIONNAIRE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objectId = ReadD();
        itemSize = ReadUH();
        items = new List<int>();
        for (int i = 0; i < itemSize; i++)
        {
            itemId = ReadD();
            items.Add(itemId);
        }
        stringItemsId = ReadS();
    }

    protected override void RunImpl()
    {
        if (objectId > 0)
        {
            Player player = GetConnection().GetActivePlayer();
            HTMLService.GetReward(player, objectId, items);
        }
    }
}
