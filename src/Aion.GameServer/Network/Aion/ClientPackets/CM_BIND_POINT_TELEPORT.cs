using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Teleport;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BIND_POINT_TELEPORT (ginho1). Bind-point teleport request (cast/cancel). BindPointTeleportService converged; AionClientPacket base red-tolerated.</summary>
public class CM_BIND_POINT_TELEPORT : AionClientPacket
{
    private byte action;
    private int locId;
    private long kinah;

    public CM_BIND_POINT_TELEPORT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadC();// 1 casting, 2 cancel, 3 done
        if (action == 1)
        {
            locId = ReadD();
            kinah = ReadQ();// kinah
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsDead())
            return;

        switch (action)
        {
            case 1:
                BindPointTeleportService.Teleport(player, locId, kinah);
                break;
            case 2:
                BindPointTeleportService.CancelTeleport(player, locId);
                break;
        }
    }
}
