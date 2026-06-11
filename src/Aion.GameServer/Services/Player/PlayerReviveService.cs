using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Services.Vortex;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/PlayerReviveService (Jego, xTz). All revive variants: duel/skill/rebirth/bind/kisk/instance/itemSelf revives + the shared revive() (hp/mp restore honoring no-resurrect-penalty, DP/soul-sickness, aggro clear, group/alliance movement update, resurrect emotion) and scheduleReviveAtBase. DimensionalVortex<?> -> <VortexLocation>; method-ref predicate->lambda; forEachPlayer lambda; schedule(Runnable,ms)->Schedule(ct-lambda); currentTimeMillis->UtcNow. Effect/enums/TeleportService/PanesterraService red-tolerated.</summary>
public class PlayerReviveService
{
    public static void DuelRevive(Player player)
    {
        Revive(player, 30, 30, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        player.UnsetResPosState();
    }

    public static void SkillRevive(Player player)
    {
        if (!player.GetResStatus())
        {
            AuditLogger.Log(player, "possibly tried to use a selfres hack (accepted missing res by another player)");
            return;
        }
        Revive(player, 35, 35, true, player.GetResurrectionSkill());
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        // if player was flying before res, start flying
        if (player.GetIsFlyingBeforeDeath())
        {
            player.GetFlyController().StartFly(true, true);
        }
        else
        {
            player.GetGameStats().UpdateStatsAndSpeedVisually();
        }

        if (player.IsInPrison())
            TeleportService.TeleportToPrison(player);
        else if (player.IsInResPostState())
            TeleportService.TeleportTo(player, player.GetWorldId(), player.GetInstanceId(), player.GetResPosX(), player.GetResPosY(), player.GetResPosZ());
        player.UnsetResPosState();
        player.SetIsFlyingBeforeDeath(false);
    }

    public static void RebirthRevive(Player player)
    {
        if (!player.CanUseRebirthRevive())
        {
            AuditLogger.Log(player, "possibly tried to use a selfres hack (no rebirth effect present)");
            return;
        }

        bool soulSickness = true;
        int rebirthResurrectPercent, rebirthSkillId;
        if (player.HasAccess(AdminConfig.AUTO_RES))
        {
            rebirthSkillId = 0;
            rebirthResurrectPercent = 100;
            soulSickness = false;
        }
        else
        {
            rebirthSkillId = player.GetRebirthEffect().GetSkillId();
            rebirthResurrectPercent = player.GetRebirthEffect().GetResurrectPercent();
            if (rebirthResurrectPercent <= 0)
            {
                NullLoggerFactory.Instance.CreateLogger(nameof(PlayerReviveService)).LogWarning("Rebirth effect missing percent.");
                rebirthResurrectPercent = 5;
            }
        }

        Revive(player, rebirthResurrectPercent, rebirthResurrectPercent, soulSickness, rebirthSkillId);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        // if player was flying before res, start flying
        if (player.GetIsFlyingBeforeDeath())
        {
            player.GetFlyController().StartFly(true, true);
        }
        else
        {
            player.GetGameStats().UpdateStatsAndSpeedVisually();
        }

        if (player.IsInPrison())
            TeleportService.TeleportToPrison(player);
        player.UnsetResPosState();
        player.SetIsFlyingBeforeDeath(false);
    }

    public static void BindRevive(Player player)
    {
        BindRevive(player, 0);
    }

    public static void BindRevive(Player player, int skillId)
    {
        if (player.IsInCustomState(CustomPlayerState.EVENT_MODE))
            Revive(player, 100, 100, false, skillId);
        else
            Revive(player, 25, 25, true, skillId);
        if (skillId > 0)
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        if (player.IsInPrison())
        {
            TeleportService.TeleportToPrison(player);
        }
        else if (player.IsInCustomState(CustomPlayerState.EVENT_MODE))
        {
            TeleportService.TeleportToEvent(player);
        }
        else if (WorldMapType.GetWorld(player.GetWorldId()) == WorldMapType.BELUS)
        {
            PanesterraService.GetInstance().ReviveInEventLocation(player);
        }
        else if (!PanesterraService.GetInstance().TeleportToStartPosition(player))
        {
            WorldPosition resPos = null;
            foreach (DimensionalVortex<VortexLocation> vortex in VortexService.GetInstance().GetActiveInvasions().Values)
            {
                if (player.GetRace() == vortex.GetVortexLocation().GetInvadersRace() && vortex.GetVortexLocation().IsInsideLocation(player))
                {
                    resPos = vortex.GetVortexLocation().GetResurrectionPoint();
                    break;
                }
            }

            if (resPos != null)
                TeleportService.TeleportTo(player, resPos);
            else
                TeleportService.MoveToBindLocation(player);
        }
        player.UnsetResPosState();
    }

    public static void KiskRevive(Player player)
    {
        KiskRevive(player, 0);
    }

    public static void KiskRevive(Player player, int skillId)
    {
        if (player.IsInPrison())
            TeleportService.TeleportToPrison(player);
        else if (player.IsInCustomState(CustomPlayerState.EVENT_MODE))
            TeleportService.TeleportToEvent(player);

        Kisk kisk = player.GetKisk();
        if (kisk != null && kisk.IsActive())
        {
            kisk.ResurrectionUsed();
            if (skillId > 0)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
            Revive(player, 30, 30, false, skillId);
            player.GetGameStats().UpdateStatsAndSpeedVisually();
            player.UnsetResPosState();
            TeleportService.TeleportTo(player, kisk.GetPosition());
        }
    }

    public static void InstanceRevive(Player player)
    {
        InstanceRevive(player, 0);
    }

    public static void InstanceRevive(Player player, int skillId)
    {
        if (player.IsInCustomState(CustomPlayerState.EVENT_MODE))
        {
            Revive(player, 100, 100, false, skillId);
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
            player.GetGameStats().UpdateStatsAndSpeedVisually();
            TeleportService.TeleportToEvent(player);
            return;
        }
        if (player.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnReviveEvent(player))
            return;
        WorldMap map = World.GetInstance().GetWorldMap(player.GetWorldId());
        if (map == null)
        {
            BindRevive(player);
            return;
        }
        Revive(player, 25, 25, true, skillId);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, new SM_PLAYER_INFO(player));
        PacketSendUtility.SendPacket(player, new SM_MOTION(player.GetObjectId(), player.GetMotions().GetActiveMotions()));
        if (map.IsInstanceType() && player.GetPosition().GetWorldMapInstance().GetStartPos() != null)
        {
            WorldPosition pos = player.GetPosition().GetWorldMapInstance().GetStartPos();
            TeleportService.TeleportTo(player, pos.GetMapId(), pos.GetX(), pos.GetY(), pos.GetZ());
        }
        else
            BindRevive(player);
        player.UnsetResPosState();
    }

    public static void Revive(Player player, int hpPercent, int mpPercent, bool setSoulSickness, int resurrectionSkill)
    {
        player.GetKnownList().ForEachPlayer(p =>
        {
            if (player.Equals(p.GetTarget()))
                p.SetTarget(null);
        });
        bool isNoResurrectPenalty = player.GetEffectController().HasAbnormalEffect(e => e.IsNoResurrectPenalty());
        player.SetPlayerResActivate(false);
        player.GetLifeStats().SetCurrentHpPercent(isNoResurrectPenalty ? 100 : hpPercent);
        player.GetLifeStats().SetCurrentMpPercent(isNoResurrectPenalty ? 100 : mpPercent);
        if (player.GetCommonData().GetDp() > 0 && !isNoResurrectPenalty)
            player.GetCommonData().SetDp(0);
        if (!isNoResurrectPenalty && setSoulSickness)
        {
            player.GetController().UpdateSoulSickness(resurrectionSkill);
        }
        player.SetResurrectionSkill(0);
        player.GetAggroList().Clear();
        player.GetController().OnBeforeSpawn();
        if (player.IsInGroup())
        {
            PlayerGroupService.UpdateGroup(player, GroupEvent.MOVEMENT);
        }
        if (player.IsInAlliance())
        {
            PlayerAllianceService.UpdateAlliance(player, PlayerAllianceEvent.MOVEMENT);
        }
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.RESURRECT), true);
    }

    public static void ItemSelfRevive(Player player)
    {
        Item item = player.GetSelfRezStone();
        if (item == null)
        {
            AuditLogger.Log(player, "tried to use selfres without having the required selfres stone");
            return;
        }

        // Add Cooldown and use item
        ItemUseLimits useLimits = item.GetItemTemplate().GetUseLimits();
        int useDelay = useLimits.GetDelayTime();
        player.AddItemCoolDown(useLimits.GetDelayId(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + useDelay, useDelay / 1000);
        player.GetController().CancelUseItem();
        PacketSendUtility.BroadcastPacket(player,
            new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), item.GetItemTemplate().GetTemplateId()), true);
        if (!player.GetInventory().DecreaseByObjectId(item.GetObjectId(), 1))
        {
            AuditLogger.Log(player, "tried to use selfres without having the required selfres stone");
            return;
        }
        // Tombstone Self-Rez retail verified 15%
        Revive(player, 15, 15, true, player.GetResurrectionSkill());
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        // if player was flying before res, start flying
        if (player.GetIsFlyingBeforeDeath())
        {
            player.GetFlyController().StartFly(true, true);
        }
        else
        {
            player.GetGameStats().UpdateStatsAndSpeedVisually();
        }

        if (player.IsInPrison())
            TeleportService.TeleportToPrison(player);
        player.UnsetResPosState();
        player.SetIsFlyingBeforeDeath(false);
    }

    public static void ScheduleReviveAtBase(Player player, int delayMillis, int skillId)
    {
        player.GetController().AddTask(TaskId.TELEPORT, ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetController().GetAndRemoveTask(TaskId.TELEPORT); // remove manually as it won't get removed automatically
            if (player.IsInInstance())
                PlayerReviveService.InstanceRevive(player, skillId);
            else if (player.GetKisk() != null)
                PlayerReviveService.KiskRevive(player, skillId);
            else
                PlayerReviveService.BindRevive(player, skillId);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delayMillis)));
    }
}
