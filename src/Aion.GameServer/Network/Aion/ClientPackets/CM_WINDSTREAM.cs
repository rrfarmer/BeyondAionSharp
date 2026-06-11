using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Flypath;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_WINDSTREam. Windstream flight state transitions (enter/leave/boost). EmotionType/CreatureState PascalCase; FlyState/PlayerMode/FlightPath.Type SCREAMING. SM_WINDSTREAM/SM_EMOTION red-tolerated.</summary>
public class CM_WINDSTREAM : AionClientPacket
{
    int teleportId;
    int distance;
    int state;

    public CM_WINDSTREAM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        teleportId = ReadD();
        distance = ReadD();
        state = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        switch (state)
        {
            case 0: // ?
                player.UnsetPlayerMode(PlayerMode.RIDE);
                break;
            case 1: // entering windstream
                if (player.IsUsingFlightTransporterOrWindstream() || !player.IsFlying())
                    return;
                player.SetFlightPath(new FlightPath(FlightPath.Type.WINDSTREAM, teleportId, distance));
                player.UnsetState(CreatureState.Active);
                player.UnsetState(CreatureState.Gliding);
                player.SetState(CreatureState.Flying);
                player.UnsetFlyState(FlyState.GLIDING);
                player.SetFlyState(FlyState.FLYING);
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.Windstream, teleportId, distance), true);
                player.GetLifeStats().TriggerFpRestore();
                QuestEngine.GetInstance().OnEnterWindStream(new QuestEnv(null, player, 0), teleportId);
                return; // don't send SM_WINDSTREAM
            case 2: // leaving windstream (gliding)
            case 3: // leaving windstream
                if (!player.IsUsingFlightPath(FlightPath.Type.WINDSTREAM))
                    return;
                player.UnsetState(CreatureState.Flying);
                player.SetState(CreatureState.Active);
                player.UnsetFlyState(FlyState.FLYING);
                player.UnsetFlyState(FlyState.GLIDING);
                if (state == 2)
                    player.GetFlyController().SwitchToGliding();
                else
                    player.GetGameStats().UpdateStatsAndSpeedVisually();
                player.SetFlightPath(null);
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, state == 2 ? EmotionType.WindstreamEnd : EmotionType.WindstreamExit),
                    true);
                if (player.IsTransformed()) // send sm_transform if player is transformed
                    PacketSendUtility.BroadcastPacketAndReceive(player, new SM_TRANSFORM(player));
                break;
            case 4: // ?
                break;
            case 7: // start boost
            case 8: // end boost
                PacketSendUtility.BroadcastPacket(player,
                    new SM_EMOTION(player, state == 7 ? EmotionType.WindstreamStartBoost : EmotionType.WindstreamEndBoost), true);
                break;
            default:
                NullLoggerFactory.Instance.CreateLogger(nameof(CM_WINDSTREAM)).LogWarning("Unknown Windstream state #" + state + " was sent from " + player.GetPosition());
                return;
        }
        PacketSendUtility.SendPacket(player, new SM_WINDSTREAM(state, 1));
    }
}
