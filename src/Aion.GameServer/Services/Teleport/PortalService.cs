using Aion.GameServer.Utils.Stats;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Teleport;

/// <summary>Java parity: services/teleport/PortalService (ATracer, xTz) — instance portal entry. Static port (maxPlayers solo/group/alliance/league registration switch), requirement checks (mentor/race/rank/title/quests/playerSize/level/items), transfer w/ cooltime. GeneralTeam&lt;?,?&gt;→GeneralTeam (wildcard erase to base); DialogPage.X.id()→Id(); streams none; getQuestVarById; getPlayersInside().size()→Count. InstanceService/WorldMapInstance/PortalPath/DAO red-tolerated.</summary>
public class PortalService
{
    private static ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PortalService));

    public static void Port(PortalPath portalPath, Player player, Npc npc)
    {
        Port(portalPath, player, npc, (byte)0);
    }

    public static void Port(PortalPath portalPath, Player player, Npc npc, byte difficult)
    {
        PortalLoc loc = DataManager.PORTAL_LOC_DATA.GetPortalLoc(portalPath.GetLocId());
        if (loc == null)
        {
            log.LogWarning("No portal loc for locId " + portalPath.GetLocId());
            return;
        }

        bool instanceGroupReq = !(player.HasAccess(AdminConfig.INSTANCE_ENTER_ALL) || player.HasPermission(MembershipConfig.INSTANCES_GROUP_REQ));
        int mapId = loc.GetWorldId();
        InstanceCooltime instanceRestrictions = DataManager.INSTANCE_COOLTIME_DATA.GetInstanceCooltimeByWorldId(mapId);
        int maxPlayers = instanceRestrictions == null ? 0 : player.GetRace() == Race.ELYOS ? instanceRestrictions.GetMaxMemberLight() : instanceRestrictions.GetMaxMemberDark();

        if (!player.HasAccess(AdminConfig.INSTANCE_ENTER_ALL))
        {
            if (!CheckMentor(player, mapId))
                return;
            if (!CheckRace(player, npc, portalPath))
                return;
            if (!CheckRank(player, npc, portalPath))
                return;
            if (!CheckTitle(player, npc, portalPath))
                return;
            if (!CheckQuests(player, npc, portalPath))
                return;
            if (instanceGroupReq && !CheckPlayerSize(player, npc, portalPath, maxPlayers))
            {
                return;
            }
        }

        WorldMapInstance instance = null;
        switch (maxPlayers)
        {
            case 0: // 0 means target map has no player limit, so it shouldn't require a registration
                break;
            case 1: // solo
                instance = InstanceService.GetRegisteredInstance(mapId, player.GetObjectId());
                break;
            case 3:
            case 6: // group
                if (player.GetPlayerGroup() != null)
                {
                    instance = InstanceService.GetRegisteredInstance(mapId, player.GetPlayerGroup().GetTeamId());
                }
                break;
            default: // alliance
                if (player.IsInAlliance())
                {
                    if (player.IsInLeague())
                    {
                        instance = InstanceService.GetRegisteredInstance(mapId, player.GetPlayerAlliance().GetLeague().GetObjectId());
                    }
                    else
                    {
                        instance = InstanceService.GetRegisteredInstance(mapId, player.GetPlayerAlliance().GetObjectId());
                    }
                }
                break;
        }

        bool reenter = false;
        if (instance == null || !instance.IsRegistered(player.GetObjectId()))
        {
            if (player.GetPortalCooldownList().IsPortalUseDisabled(mapId))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME());
                return;
            }
        }
        else if (player.GetWorldId() != mapId || player.GetInstanceId() != instance.GetInstanceId())
        {
            reenter = true;
        }

        if (!reenter)
        {
            if (!CheckEnterLevel(player, npc, portalPath, instanceRestrictions))
            {
                return;
            }
            if (!CheckAndRemoveRequiredItems(player, npc, portalPath))
            {
                return;
            }
            if (mapId == player.GetWorldId())
            { // teleport within this instance
                TeleportService.TeleportTo(player, mapId, player.GetInstanceId(), loc.GetX(), loc.GetY(), loc.GetZ(), (byte)loc.GetH());
                return;
            }
        }

        PlayerGroup group = player.GetPlayerGroup();
        switch (maxPlayers)
        {
            case 0:
            case 1:
                // if already registered - just teleport
                if (instance != null && mapId != player.GetWorldId())
                    Transfer(player, loc, instance, reenter);
                else
                    Port(player, loc, reenter, maxPlayers);
                break;
            case 3:
            case 6:
                if (group != null || !instanceGroupReq)
                {
                    instance = InstanceService.GetRegisteredInstance(mapId, group != null ? group.GetTeamId() : player.GetObjectId());

                    // No instance (for group), group on and default requirement off
                    if (instance == null && group != null && !instanceGroupReq)
                    {
                        // For each player from group
                        foreach (Player member in group.GetMembers())
                        {
                            // Get his instance
                            instance = InstanceService.GetRegisteredInstance(mapId, member.GetObjectId());

                            // If some player is soloing and I found no one else yet, I get his instance
                            if (instance != null)
                            {
                                break;
                            }
                        }

                        // No solo instance found
                        if (instance == null)
                        {
                            instance = InstanceService.GetNextAvailableInstance(mapId, difficult, maxPlayers);
                            instance.RegisterTeam(group);
                        }
                    }
                    // No instance and default requirement on = Group on
                    else if (instance == null && instanceGroupReq)
                    {
                        instance = InstanceService.GetNextAvailableInstance(mapId, difficult, maxPlayers);
                        instance.RegisterTeam(group);
                    }
                    // No instance, default requirement off, no group = Register new instance with player ID
                    else if (instance == null && !instanceGroupReq && group == null)
                    {
                        instance = InstanceService.GetNextAvailableInstance(mapId, difficult, maxPlayers);
                    }
                    if (instance.GetPlayersInside().Count < maxPlayers)
                    {
                        Transfer(player, loc, instance, reenter);
                    }
                }
                break;
            default:
                PlayerAlliance allianceGroup = player.GetPlayerAlliance();
                if (allianceGroup != null || !instanceGroupReq)
                {
                    GeneralTeam team = allianceGroup;
                    if (allianceGroup != null && allianceGroup.GetLeague() != null)
                        team = allianceGroup.GetLeague();
                    int teamId = team == null ? player.GetObjectId() : team.GetObjectId();
                    instance = InstanceService.GetRegisteredInstance(mapId, teamId);

                    if (instance == null)
                    {
                        instance = InstanceService.GetNextAvailableInstance(mapId, difficult, maxPlayers);
                        if (team != null)
                            instance.RegisterTeam(team);
                    }
                    if (instance.GetPlayersInside().Count < maxPlayers)
                    {
                        Transfer(player, loc, instance, reenter);
                    }
                }
                break;
        }
    }

    private static bool CheckMentor(Player player, int mapId)
    {
        InstanceCooltime instancecooltime = DataManager.INSTANCE_COOLTIME_DATA.GetInstanceCooltimeByWorldId(mapId);
        if (instancecooltime != null && player.IsMentor())
        {
            if (!instancecooltime.GetCanEnterMentor())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_CANT_ENTER(mapId));
                return false;
            }
        }
        return true;
    }

    private static bool CheckEnterLevel(Player player, Npc npc, PortalPath portalPath, InstanceCooltime instanceRestrictions)
    {
        if (player.HasPermission(MembershipConfig.INSTANCES_LEVEL_REQ))
            return true;
        int enterMinLvl = portalPath.GetMinLevel();
        int enterMaxLvl = 0;
        if (instanceRestrictions != null)
        {
            if (enterMinLvl == 0)
                enterMinLvl = player.GetRace() == Race.ELYOS ? instanceRestrictions.GetEnterMinLevelLight() : instanceRestrictions.GetEnterMinLevelDark();
            enterMaxLvl = player.GetRace() == Race.ELYOS ? instanceRestrictions.GetEnterMaxLevelLight() : instanceRestrictions.GetEnterMaxLevelDark();
        }
        int lvl = player.GetLevel();
        if (lvl < enterMinLvl || enterMaxLvl > 0 && lvl > enterMaxLvl)
        {
            if (portalPath.GetErrLevel() != 0)
            {
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), portalPath.GetErrLevel()));
            }
            else
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_ENTER_LEVEL());
            }
            return false;
        }
        return true;
    }

    private static bool CheckRace(Player player, Npc npc, PortalPath portalPath)
    {
        if (player.HasPermission(MembershipConfig.INSTANCES_RACE_REQ))
            return true;
        int siegeId = portalPath.GetSiegeId();
        Race portalRace = portalPath.GetRace();
        if (portalRace != Race.PC_ALL && player.GetRace() != portalRace || siegeId != 0 && !CheckSiegeId(player, siegeId))
        {
            if (npc.GetObjectTemplate().IsDialogNpc())
            {
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), DialogPage.NO_RIGHT.Id()));
            }
            else
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MOVE_PORTAL_ERROR_INVALID_RACE());
            }
            return false;
        }
        return true;
    }

    private static bool CheckSiegeId(Player player, int sigeId)
    {
        FortressLocation loc = SiegeService.GetInstance().GetFortress(sigeId);
        if (loc != null)
        {
            if (loc.GetRace().GetRaceId() != player.GetRace().GetRaceId())
            {
                return false;
            }
        }
        return true;
    }

    private static bool CheckRank(Player player, Npc npc, PortalPath portalPath)
    {
        if (player.GetAbyssRank().GetRank().GetId() < portalPath.GetMinRank())
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), DialogPage.NO_RIGHT.Id()));
            return false;
        }
        return true;
    }

    private static bool CheckPlayerSize(Player player, Npc npc, PortalPath portalPath, int maxPlayers)
    {
        if (maxPlayers == 6 || maxPlayers == 3)
        { // group
            if (!player.IsInGroup())
            {
                if (portalPath.GetErrGroup() != 0)
                {
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), portalPath.GetErrGroup()));
                }
                else
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ENTER_ONLY_PARTY_DON());
                }
                return false;
            }
        }
        else if (maxPlayers > 6 && maxPlayers <= 24)
        { // alliance
            if (!player.IsInAlliance())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ENTER_ONLY_FORCE_DON());
                return false;
            }
        }
        else if (maxPlayers > 24)
        { // league
            if (!player.IsInLeague())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ENTER_ONLY_UNION_DON());
                return false;
            }
        }
        return true;
    }

    private static bool CheckTitle(Player player, Npc npc, PortalPath portalPath)
    {
        if (player.HasPermission(MembershipConfig.INSTANCES_TITLE_REQ))
            return true;
        int titleId = portalPath.GetTitleId();
        if (titleId != 0 && player.GetCommonData().GetTitleId() != titleId)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), DialogPage.NO_RIGHT.Id()));
            return false;
        }
        return true;
    }

    private static bool CheckQuests(Player player, Npc npc, PortalPath portalPath)
    {
        if (player.HasPermission(MembershipConfig.INSTANCES_QUEST_REQ))
            return true;
        List<QuestReq> questReq = portalPath.GetQuestReq();
        if (questReq != null)
        {
            foreach (QuestReq quest in questReq)
            {
                int questId = quest.GetQuestId();
                int questStep = quest.GetQuestStep();
                QuestState qs = player.GetQuestStateList().GetQuestState(questId);
                if (qs != null && (qs.GetStatus() == QuestStatus.COMPLETE || (questStep > 0 && qs.GetQuestVarById(0) >= questStep)))
                {
                    return true; // one requirement matched
                }
            }
            if (npc.GetObjectTemplate().IsDialogNpc())
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), DialogPage.NO_RIGHT.Id()));
            else
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_USE_GROUPGATE_NO_RIGHT()); // seems there's no default msg
            return false;
        }
        return true;
    }

    private static bool CheckAndRemoveRequiredItems(Player player, Npc npc, PortalPath portalPath)
    {
        Storage inventory = player.GetInventory();
        if (inventory.GetKinah() < portalPath.GetKinah())
        {
            if (npc.GetObjectTemplate().IsDialogNpc())
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), DialogPage.NO_RIGHT.Id()));
            else
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_KINA(portalPath.GetKinah()));
            return false;
        }
        if (portalPath.GetItemReq() != null)
        {
            foreach (ItemReq item in portalPath.GetItemReq())
            {
                if (inventory.GetItemCountByItemId(item.GetItemId()) < item.GetItemCount())
                {
                    if (npc.GetObjectTemplate().IsDialogNpc())
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), DialogPage.NO_RIGHT.Id()));
                    else
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM());
                    return false;
                }
            }
            foreach (ItemReq item in portalPath.GetItemReq())
                inventory.DecreaseByItemId(item.GetItemId(), item.GetItemCount());
        }
        if (portalPath.GetKinah() > 0)
            inventory.DecreaseKinah(portalPath.GetKinah());

        return true;
    }

    private static void Port(Player requester, PortalLoc loc, bool reenter, int maxPlayers)
    {
        WorldMapTemplate worldTemplate = DataManager.WORLD_MAPS_DATA.GetTemplate(loc.GetWorldId());
        if (worldTemplate.IsInstance())
        {
            bool isPersonal = WorldMapTypeExtensions.GetWorld(loc.GetWorldId()).IsPersonal();
            WorldMapInstance instance = InstanceService.GetNextAvailableInstance(loc.GetWorldId(), isPersonal ? requester.GetObjectId() : 0, (byte)0, maxPlayers, true);
            instance.Register(requester.GetObjectId());
            Transfer(requester, loc, instance, reenter);
        }
        else
        {
            TeleportService.TeleportTo(requester, loc.GetWorldId(), loc.GetX(), loc.GetY(), loc.GetZ(), (byte)loc.GetH(), TeleportAnimation.FADE_OUT_BEAM);
        }
    }

    private static void Transfer(Player player, PortalLoc loc, WorldMapInstance instance, bool reenter)
    {
        if (instance.GetStartPos() == null)
            instance.SetStartPos(new WorldPosition(loc.GetWorldId(), loc.GetX(), loc.GetY(), loc.GetZ(), (byte)loc.GetH()));
        instance.Register(player.GetObjectId());
        TeleportService.TeleportTo(player, loc.GetWorldId(), instance.GetInstanceId(), loc.GetX(), loc.GetY(), loc.GetZ(), (byte)loc.GetH(),
            TeleportAnimation.FADE_OUT_BEAM);
        long useDelay = DataManager.INSTANCE_COOLTIME_DATA.CalculateInstanceEntranceCooltime(player, instance.GetMapId());
        if (useDelay > 0 && !reenter)
        {
            player.GetPortalCooldownList().AddPortalCooldown(loc.GetWorldId(), useDelay);
        }
    }
}
