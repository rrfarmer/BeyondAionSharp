using System;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/portals/LegionDominionPortalAI (Created by Yeats on 19.02.2016).
/// </summary>
[AIName("legion_dominion_portal")]
public class LegionDominionPortalAI : PortalDialogAI
{
    public LegionDominionPortalAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId != DialogAction.SETPRO1)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
            return true;
        }
        DateTimeOffset now = ServerTime.Now();
        if (now.DayOfWeek == DayOfWeek.Wednesday && now.Hour >= 8 && now.Hour <= 10)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CLOSED_TIME(301500000));
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
            return true;
        }

        if (player.GetLevel() < 65)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_ENTER_LEVEL());
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
            return true;
        }

        if (player.IsInAlliance() && !player.IsInLeague())
        {
            PortalPath portalPath = DataManager.PORTAL2_DATA.GetPortalDialogPath(GetNpcId(), dialogActionId, player);
            WorldMapInstance instance = InstanceService.GetRegisteredInstance(301500000, player.GetPlayerAlliance().GetObjectId());
            if (portalPath == null)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_NEW_MAP_INFO_CANT_FIND_INSTANCE());
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
                return true;
            }
            if (instance == null)
            {
                if (HasCd(player))
                {
                    return true;
                }
                // only alliance leader can open this instance
                if (player.GetPlayerAlliance().IsSomeCaptain(player) && player.GetLegion() != null && player.GetLegion().GetCurrentLegionDominion() > 0)
                {
                    if (!LegionConfig.REQUIRE_KEY_FOR_STONESPEAR_REACH || player.GetInventory().DecreaseByItemId(185000230, 1))
                    {
                        PortalService.Port(portalPath, player, GetOwner());
                        return true;
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM());
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
                        return true;
                    }
                }
                else
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_NOT_LEADER());
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
                    return true;
                }
            }
            else if (!instance.IsRegistered(player.GetObjectId()))
            {
                if (HasCd(player))
                {
                    return true;
                }
                IInstanceHandler handler = instance.GetInstanceHandler();

                if (handler != null && handler.CanEnter(player))
                {
                    PortalService.Port(portalPath, player, GetOwner());
                }
                else
                {
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
                    return true;
                }
            }
            else if (instance.IsRegistered(player.GetObjectId()))
            {
                IInstanceHandler handler = instance.GetInstanceHandler();

                if (handler != null && handler.CanEnter(player))
                {
                    PortalService.Port(portalPath, player, GetOwner());
                }
                else
                {
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
                    return true;
                }
                return true;
            }
            else
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_ENTER_STATE());
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
                return true;
            }
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ENTER_ONLY_FORCE_DON());
        }

        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
        return true;
    }

    private bool HasCd(Player player)
    {
        if (player.GetPortalCooldownList().IsPortalUseDisabled(301500000))
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME());
            return true;
        }
        return false;
    }
}
