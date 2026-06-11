using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_STOP_TRAINING (xTz). Notifies the instance handler that the player stopped training. WorldMapInstance/IInstanceHandler red-tolerated.</summary>
public class CM_STOP_TRAINING : AionClientPacket
{
    public CM_STOP_TRAINING(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        // nothing to read
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        player.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnStopTraining(player);
    }
}
