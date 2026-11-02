using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Flypath;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MOVE_IN_AIR (-Nemesiss-, Sweetkr, KID). Player flying-teleport movement update. CreatureState PascalCase; World red-tolerated.</summary>
public class CM_MOVE_IN_AIR : AionClientPacket
{
    private const float MAX_WINDSTREAM_DEVIATION = 170;
    private int worldId;
    private float x, y, z;
    private byte heading;
    private int progress;

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
        progress = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (!player.IsSpawned())
            return;
        if (!player.IsInState(CreatureState.FLYING) || worldId != player.GetPosition().GetClientWorldId())
            return;

        if (player.GetFlightPath() != null)
        {
            if (!IsOnWindstreamPath(player.GetFlightPath()))
                return;
            player.GetFlightPath().SetDistance(progress);
        }

        if (player.IsProtectionActive())
            player.GetController().StopProtectionActiveTask();

        global::Aion.GameServer.World.World.GetInstance().UpdatePosition(player, x, y, z, heading);
        player.GetMoveController().OnMoveFromClient();
        player.GetController().OnMove();
    }

    private bool IsOnWindstreamPath(FlightPath flightPath)
    {
        if (flightPath.GetType_() != FlightPath.Type.WINDSTREAM)
            return true;
        FlyPathEntry path = DataManager.FLY_PATH.GetPathTemplate(flightPath.GetId() / 1000);
        if (path == null || !path.IsWindstream())
            return true;
        if (!flightPath.IsProgressReachable(progress))
            return false;
        var position = path.GetWindstreamPosition(unchecked(progress + flightPath.GetBoostGain(progress)));
        return PositionUtil.IsInRange(x, y, z, position.GetX(), position.GetY(), position.GetZ(), MAX_WINDSTREAM_DEVIATION);
    }
}
