using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_EXCHANGE_CANCEL (-Avol-). Cancels the active trade. ExchangeService red-tolerated.</summary>
public class CM_EXCHANGE_CANCEL : AionClientPacket
{
    public CM_EXCHANGE_CANCEL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        // 0 bytes
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        ExchangeService.GetInstance().CancelExchange(activePlayer);
    }
}
