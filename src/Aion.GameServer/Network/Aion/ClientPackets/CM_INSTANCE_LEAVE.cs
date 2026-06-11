using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_INSTANCE_LEAVE (xTz). Asks the instance handler to remove the player from the current instance. WorldMapInstance/InstanceHandler red-tolerated.</summary>
public class CM_INSTANCE_LEAVE : AionClientPacket
{
    public CM_INSTANCE_LEAVE(int opcode, ISet<State> validStates)
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
        if (player.IsInInstance())
        {
            player.GetPosition().GetWorldMapInstance().GetInstanceHandler().LeaveInstance(player);
        }
    }
}
