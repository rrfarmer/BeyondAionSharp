using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MOVE_IN_AIR (-Nemesiss-, Sweetkr, KID). Player flying-teleport movement update. CreatureState PascalCase; World red-tolerated.</summary>
public class CM_MOVE_IN_AIR : AionClientPacket
{
    private int worldId;
    private float x, y, z;
    private byte heading;
    private int distance;

    public CM_MOVE_IN_AIR(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        worldId = ReadD();
        x = ReadF();
        y = ReadF();
        z = ReadF();
        heading = ReadC();
        distance = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (!player.IsSpawned())
            return;
        if (!player.IsInState(CreatureState.Flying))
            return;

        if (player.GetFlightPath() != null)
            player.GetFlightPath().SetDistance(distance);

        if (player.IsProtectionActive())
            player.GetController().StopProtectionActiveTask();

        World.GetInstance().UpdatePosition(player, x, y, z, heading);
        player.GetMoveController().OnMoveFromClient();
        player.GetController().OnMove();
    }
}
