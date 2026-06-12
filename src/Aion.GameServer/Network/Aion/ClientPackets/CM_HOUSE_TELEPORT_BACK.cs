using System.Collections.Generic;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Teleport;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_TELEPORT_BACK (Rolandas). Teleports the player back to their stored battle-return coords. TeleportService red-tolerated.</summary>
public class CM_HOUSE_TELEPORT_BACK : AionClientPacket
{
    public CM_HOUSE_TELEPORT_BACK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        // Nothing to read
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        float[] coords = player.GetBattleReturnCoords();
        if (coords != null && player.GetBattleReturnMap() != 0)
        {
            TeleportService
                .TeleportTo(player, player.GetBattleReturnMap(), 1, coords[0], coords[1], coords[2], (byte)0, TeleportAnimation.FADE_OUT_BEAM);

            player.SetBattleReturnCoords(0, null);
        }
    }
}
