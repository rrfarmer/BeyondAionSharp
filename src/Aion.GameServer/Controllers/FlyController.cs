using System;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.State;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/FlyController.</summary>
public class FlyController
{
    private const long FLY_REUSE_TIME = 10000;
    private Aion.GameServer.Model.GameObjects.Players.Player player;

    public FlyController(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        this.player = player;
    }

    public void OnStopGliding()
    {
        if (player.IsInGlidingState())
        {
            player.UnsetFlyState(FlyState.GLIDING);
            player.UnsetState(CreatureState.Gliding);
            if (!player.IsInFlyState(FlyState.FLYING))
            {
                player.GetLifeStats().TriggerFpRestore();
                Aion.GameServer.Utils.PacketSendUtility.BroadcastToSightedPlayers(player, new Aion.GameServer.Network.Aion.ServerPackets.SmEmotion(player, EmotionType.StopGlide), true);
            }
            else
            {
                player.GetLifeStats().TriggerFpReduce();
            }
            player.GetGameStats().UpdateStatsAndSpeedVisually();
        }
    }

    /// <summary>
    /// Ends flying: 1) by CM_EMOTION, 2) from server side during teleport, 3) when FP is decreased to 0.
    /// </summary>
    public void EndFly(bool broadcastPacket)
    {
        player.UnsetFlyState(FlyState.FLYING);
        player.UnsetFlyState(FlyState.GLIDING);
        player.UnsetState(CreatureState.Flying);
        player.UnsetState(CreatureState.Gliding);
        player.UnsetState(CreatureState.FloatingCorpse);
        player.GetGameStats().UpdateStatsAndSpeedVisually();

        if (broadcastPacket && player.IsSpawned())
            Aion.GameServer.Utils.PacketSendUtility.BroadcastToSightedPlayers(player, new Aion.GameServer.Network.Aion.ServerPackets.SmEmotion(player, EmotionType.Land), true);
        player.GetLifeStats().TriggerFpRestore();
    }

    /// <summary>
    /// Starts flying (CM_EMOTION pageUp / fly button, on revive, or after teleport).
    /// </summary>
    public bool StartFly(bool broadcastPacket, bool ignoreFlightCooldown)
    {
        if (!CanFly(player))
            return false;
        if (!ignoreFlightCooldown)
        {
            if (player.GetFlyReuseTime() > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
                Aion.GameServer.Utils.Audit.AuditLogger.Log(player, "possibly using fly cooldown hack. Left cooldown time: " + ((player.GetFlyReuseTime() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000) + "s");
                return false;
            }
            player.SetFlyReuseTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + FLY_REUSE_TIME - 100);
        }
        player.SetFlyState(FlyState.FLYING);
        player.SetState(CreatureState.Flying);
        if (player.IsInPlayerMode(Aion.GameServer.Model.Actions.PlayerMode.RIDE))
        {
            player.SetState(CreatureState.FloatingCorpse);
        }
        player.GetLifeStats().TriggerFpReduce();
        player.GetGameStats().UpdateStatsAndSpeedVisually();

        if (broadcastPacket)
            Aion.GameServer.Utils.PacketSendUtility.BroadcastToSightedPlayers(player, new Aion.GameServer.Network.Aion.ServerPackets.SmEmotion(player, EmotionType.Fly), true);
        return true;
    }

    private static bool CanFly(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        if (!player.GetCommonData().IsDaeva())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GLIDE_ONLY_DEVA_CAN());
            return false;
        }
        if (!player.HasAccess(Aion.GameServer.Configs.Administration.AdminConfig.FREE_FLIGHT) && (player.IsInsideZoneType(Aion.GameServer.Model.Templates.Zone.ZoneType.NoFly) || !player.IsInsideZoneType(Aion.GameServer.Model.Templates.Zone.ZoneType.Fly)))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_FLYING_FORBIDDEN_HERE());
            return false;
        }
        if (player.GetEffectController().IsAbnormalSet(Aion.GameServer.SkillEngine.Effects.AbnormalState.NOFLY))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANT_FLY_NOW_DUE_TO_NOFLY());
            return false;
        }
        if (player.GetTransformModel().GetRes6() == 1)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_FLY_CANNOT_FLY_POLYMORPH_STATUS());
            return false;
        }
        return player.GetStore() == null;
    }

    /// <summary>Switching to glide mode (CM_MOVE VALIDATE_GLIDE) from standing or flying state.</summary>
    public bool SwitchToGliding()
    {
        if (player.IsInGlidingState() || !player.CanPerformMove())
            return false;

        if (!CanGlide(player))
            return false;
        if (player.GetFlyState() == 0)
        {
            // fly reuse time only if gliding from walking
            if (player.GetFlyReuseTime() > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
                return false;
            }
            player.SetFlyReuseTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + FLY_REUSE_TIME);
        }
        player.SetFlyState(FlyState.GLIDING);
        player.SetState(CreatureState.Gliding);
        player.GetLifeStats().TriggerFpReduce();
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        return true;
    }

    private static bool CanGlide(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        if (!player.GetCommonData().IsDaeva())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GLIDE_ONLY_DEVA_CAN());
            return false;
        }
        if (player.GetTransformModel().GetRes6() == 1)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GLIDE_CANNOT_GLIDE_POLYMORPH_STATUS());
            return false;
        }
        return true;
    }
}
