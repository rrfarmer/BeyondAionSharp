using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_DUEL_REQUEST (xavier). Requests a duel with a target player. DuelService red-tolerated.</summary>
public class CM_DUEL_REQUEST : AionClientPacket
{
    /// <summary>Target object id that client wants to start duel with</summary>
    private int objectId;

    public CM_DUEL_REQUEST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        DuelService.GetInstance().OnDuelRequest(activePlayer, activePlayer.GetKnownList().GetPlayer(objectId));
    }
}
