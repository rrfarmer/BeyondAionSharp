using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_EXCHANGE_ADD_ITEM (Avol). Adds an item to the active trade. ExchangeService red-tolerated.</summary>
public class CM_EXCHANGE_ADD_ITEM : AionClientPacket
{
    public int itemObjId;
    public int itemCount;

    public CM_EXCHANGE_ADD_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        itemObjId = ReadD();
        itemCount = ReadD();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        ExchangeService.GetInstance().AddItem(activePlayer, itemObjId, itemCount);
    }
}
