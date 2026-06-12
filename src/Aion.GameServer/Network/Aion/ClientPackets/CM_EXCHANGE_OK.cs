using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_EXCHANGE_OK (-Avol-). Confirms the active trade. ExchangeService red-tolerated.</summary>
public class CM_EXCHANGE_OK : AionClientPacket
{
    public CM_EXCHANGE_OK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {

    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        ExchangeService.GetInstance().ConfirmExchange(activePlayer);
    }
}
