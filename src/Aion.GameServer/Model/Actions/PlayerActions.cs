using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Ride;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Actions;

/// <summary>Java parity: model/actions/PlayerActions (xTz). Static util; Object→object; synchronized(rideObservers)→lock. Player public fields ride/inRoll, CreatureState/EmotionType/SM_EMOTION/ActionObserver/RideInfo/InRoll red-tolerated.</summary>
public class PlayerActions
{
    public static bool IsInPlayerMode(Player player, PlayerMode mode)
    {
        switch (mode)
        {
            case PlayerMode.RIDE:
                return player.ride != null;
            case PlayerMode.IN_ROLL:
                return player.inRoll != null;
        }
        return false;
    }

    public static void SetPlayerMode(Player player, PlayerMode mode, object obj)
    {
        switch (mode)
        {
            case PlayerMode.RIDE:
                player.ride = (RideInfo)obj;
                break;
            case PlayerMode.IN_ROLL:
                player.inRoll = (InRoll)obj;
                break;
        }
    }

    public static bool UnsetPlayerMode(Player player, PlayerMode mode)
    {
        switch (mode)
        {
            case PlayerMode.RIDE:
                if (player.ride == null)
                    return false;
                player.ride = null;
                // check for sprinting when forcefully dismounting player
                if (player.IsInSprintMode())
                {
                    if (!player.IsInFlyingState())// if player is flying while dismounting, do not start restore task
                        player.GetLifeStats().TriggerFpRestore();
                    player.SetSprintMode(false);
                }
                player.UnsetState(CreatureState.RESTING);
                player.UnsetState(CreatureState.FLOATING_CORPSE);
                player.SetState(CreatureState.ACTIVE);
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.CHANGE_SPEED, 0, 0), true);
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.RIDE_END), true);

                player.GetGameStats().UpdateStatsAndSpeedVisually();

                // remove rideObservers
                List<ActionObserver> rideObservers = player.GetRideObservers();
                lock (rideObservers)
                {
                    foreach (ActionObserver observer in rideObservers)
                    {
                        player.GetObserveController().RemoveObserver(observer);
                    }
                    rideObservers.Clear();
                }
                return true;
            case PlayerMode.IN_ROLL:
                if (player.inRoll == null)
                    return false;
                player.inRoll = null;
                return true;
            default:
                return false;
        }
    }
}
