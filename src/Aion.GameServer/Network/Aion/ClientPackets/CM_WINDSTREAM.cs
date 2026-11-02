using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Dataholders;
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
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: windstream entry, exit and boost state transitions (332d54885).</summary>
public class CM_WINDSTREAM : AionClientPacket
{
    private const float MAX_ENTRY_DISTANCE = 45, MAX_GLIDE_EXIT_DISTANCE = 50, MAX_EXIT_DISTANCE = 30;
    private int teleportId;
    private int progress;
    private WindstreamState? state;

    public CM_WINDSTREAM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        teleportId = ReadD();
        progress = ReadD();
        int stateId = ReadD();
        state = WindstreamStateExtensions.GetById(stateId);
        if (state == null)
            AionLog.For(nameof(CM_WINDSTREAM)).LogWarning("Unknown Windstream state #{State} was sent from {Position}", stateId, GetConnection().GetActivePlayer().GetPosition());
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (state == null)
            return;
        switch (state)
        {
            case WindstreamState.ENTER:
                if (player.IsUsingFlightTransporterOrWindstream())
                    return;
                // The client can ask before its flying/gliding move packet arrives, and retries until accepted.
                if (!player.IsFlying())
                {
                    SendPacket(new SM_WINDSTREAM(state.Value, false));
                    return;
                }
                if (!CanEnter(player))
                {
                    AuditLogger.Log(player, "tried to enter windstream " + teleportId + " at " + progress + " from " + player.GetPosition());
                    SendPacket(new SM_WINDSTREAM(state.Value, false));
                    return;
                }
                player.UnsetPlayerMode(PlayerMode.RIDE);
                RecallService.GetInstance().Cancel(player, RecallService.CancelReason.CANCELLED);
                player.SetFlightPath(new FlightPath(FlightPath.Type.WINDSTREAM, teleportId, progress));
                player.UnsetState(CreatureState.ACTIVE);
                player.UnsetState(CreatureState.GLIDING);
                player.SetState(CreatureState.FLYING);
                player.UnsetFlyState(FlyState.GLIDING);
                player.SetFlyState(FlyState.FLYING);
                player.GetLifeStats().TriggerFpRestore();
                break;
            case WindstreamState.ENTERED:
                if (player.IsUsingFlightPath(FlightPath.Type.WINDSTREAM))
                {
                    PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.WINDSTREAM, teleportId, progress), true);
                    global::Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnEnterWindStream(new QuestEnv(null, player, 0), teleportId);
                }
                return; // don't send SM_WINDSTREAM
            case WindstreamState.LEAVE_GLIDING:
            case WindstreamState.LEAVE:
                if (!player.IsUsingFlightPath(FlightPath.Type.WINDSTREAM))
                    return;
                if (!CanLeave(player))
                {
                    AuditLogger.Log(player, "tried to leave windstream " + teleportId + " at " + progress + " from " + player.GetPosition());
                    SendPacket(new SM_WINDSTREAM(state.Value, false));
                    return;
                }
                player.UnsetState(CreatureState.FLYING);
                player.SetState(CreatureState.ACTIVE);
                player.UnsetFlyState(FlyState.FLYING);
                player.UnsetFlyState(FlyState.GLIDING);
                player.GetMoveController().RestartFalling(player.GetZ());
                if (state == WindstreamState.LEAVE_GLIDING)
                    player.GetFlyController().SwitchToGliding();
                else
                    player.GetGameStats().UpdateStatsAndSpeedVisually();
                player.SetFlightPath(null);
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, state == WindstreamState.LEAVE_GLIDING ? EmotionType.WINDSTREAM_END : EmotionType.WINDSTREAM_EXIT),
                    true);
                if (player.IsTransformed()) // send sm_transform if player is transformed
                    PacketSendUtility.BroadcastPacketAndReceive(player, new SM_TRANSFORM(player));
                break;
            case WindstreamState.LEFT:
            case WindstreamState.RESET:
                break;
            case WindstreamState.START_BOOST:
            case WindstreamState.END_BOOST:
                if (!player.IsUsingFlightPath(FlightPath.Type.WINDSTREAM))
                    return;
                bool start = state == WindstreamState.START_BOOST;
                if (!(start ? player.GetFlightPath().StartBoost(progress) : player.GetFlightPath().EndBoost(progress)))
                {
                    SendPacket(new SM_WINDSTREAM(state.Value, false));
                    return;
                }
                PacketSendUtility.BroadcastPacket(player,
                    new SM_EMOTION(player, start ? EmotionType.WINDSTREAM_START_BOOST : EmotionType.WINDSTREAM_END_BOOST), true);
                break;
        }
        SendPacket(new SM_WINDSTREAM(state.Value, true));
    }

    private bool CanEnter(Player player)
    {
        FlyPathEntry path = DataManager.FLY_PATH.GetPathTemplate(teleportId / 1000);
        if (path == null || !path.IsWindstream() || path.GetStartWorldId() != player.GetWorldId() || !path.CanEnterWindstream(progress))
            return false;
        var entry = path.GetWindstreamPosition(progress);
        return PositionUtil.IsInRange(player, entry.GetX(), entry.GetY(), entry.GetZ(), MAX_ENTRY_DISTANCE);
    }

    private bool CanLeave(Player player)
    {
        FlyPathEntry path = DataManager.FLY_PATH.GetPathTemplate(player.GetFlightPath().GetId() / 1000);
        if (path == null || !path.IsWindstream())
            return true;
        if (!player.GetFlightPath().IsProgressReachable(progress))
            return false;
        int boostedProgress = unchecked(progress + player.GetFlightPath().GetBoostGain(progress));
        if (!path.CanLeaveWindstream(boostedProgress))
            return false;
        var exit = path.GetWindstreamPosition(boostedProgress);
        float maxDistance = state == WindstreamState.LEAVE_GLIDING ? MAX_GLIDE_EXIT_DISTANCE : MAX_EXIT_DISTANCE;
        return PositionUtil.IsInRange(player, exit.GetX(), exit.GetY(), exit.GetZ(), maxDistance);
    }
}
