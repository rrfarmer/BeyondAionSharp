using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.FindGroup;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Findgroup;

/// <summary>Java parity: services/findgroup/FindGroupService (cura, MrPoke). Singleton; ConcurrentDictionary recruitments/applications/instanceGroups; show/add/update/remove for the Recruit/Apply/Instance tabs, server-wide instance application flow, onJoinedTeam/onLogout cleanup. ConcurrentHashMap->ConcurrentDictionary; values().stream().filter().toList()->Values.Where().ToList(); map.get->GetValueOrDefault; remove->TryRemove; put->indexer; List.of->new List; instanceof Npc npc->is Npc npc; broadcastToWorld predicate->lambda. Golden-capture FindGroupMutationPostTraceCaptureHooks calls (no-op instrumentation, not Aion gameplay) omitted. SM_FIND_GROUP overloads/TemporaryPlayerTeam wildcard red-tolerated.</summary>
public class FindGroupService
{
    private readonly ConcurrentDictionary<int, GroupRecruitment> recruitments = new ConcurrentDictionary<int, GroupRecruitment>(); // Recruit Group Members tab
    private readonly ConcurrentDictionary<int, GroupApplication> applications = new ConcurrentDictionary<int, GroupApplication>(); // Apply for Group tab
    private readonly ConcurrentDictionary<int, ServerWideGroup> instanceGroups = new ConcurrentDictionary<int, ServerWideGroup>(); // Instance Groups tab

    private FindGroupService()
    {
    }

    public void ShowRecruitments(Player player)
    {
        List<GroupRecruitment> recruitments = this.recruitments.Values.Where(r => r.GetRace() == player.GetRace()).ToList();
        PacketSendUtility.SendPacket(player, new SM_FIND_GROUP(0, recruitments));
    }

    public void ShowApplications(Player player)
    {
        List<GroupApplication> applications = this.applications.Values.Where(r => r.GetPlayer().GetRace() == player.GetRace()).ToList();
        PacketSendUtility.SendPacket(player, new SM_FIND_GROUP(4, applications));
    }

    public GroupRecruitment RemoveRecruitment(TemporaryPlayerTeam<TeamMember<Player>> team)
    {
        return RemoveRecruitment(team.GetTeamId(), (byte)NetworkConfig.GAMESERVER_ID, (byte)0, (byte)0, (byte)0);
    }

    public GroupRecruitment RemoveRecruitment(Player player, byte serverId, byte unk1, byte unk2, byte unk3)
    {
        int teamId = player.GetCurrentTeamId();
        return RemoveRecruitment(teamId == 0 ? player.GetObjectId() : teamId, serverId, unk1, unk2, unk3);
    }

    private GroupRecruitment RemoveRecruitment(int playerOrTeamId, byte serverId, byte unk1, byte unk2, byte unk3)
    {
        recruitments.TryRemove(playerOrTeamId, out GroupRecruitment recruitment);
        if (recruitment != null)
            PacketSendUtility.BroadcastToWorld(new SM_FIND_GROUP(playerOrTeamId, serverId, unk1, unk2, unk3), p => p.GetRace() == recruitment.GetRace());
        return recruitment;
    }

    public void RemoveApplication(Player player)
    {
        applications.TryRemove(player.GetObjectId(), out GroupApplication application);
        if (application != null)
            PacketSendUtility.BroadcastToWorld(new SM_FIND_GROUP(player.GetObjectId()), p => p.GetRace() == application.GetPlayer().GetRace());
    }

    public void AddRecruitment(Player player, string message, int groupType)
    {
        AionObject playerOrTeam = player.GetCurrentTeam();
        if (playerOrTeam == null)
            playerOrTeam = player;
        GroupRecruitment recruitment = new GroupRecruitment(playerOrTeam, message, groupType);
        recruitments[playerOrTeam.GetObjectId()] = recruitment;
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_OFFER_PARTY_POSTED());
        ShowRecruitments(player); // necessary if player switched tabs before adding this entry (client bug)
    }

    public void AddApplication(Player player, string message, int groupType, int classId, int level)
    {
        GroupApplication application = new GroupApplication(player, message, groupType, classId, level);
        applications[player.GetObjectId()] = application;
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_SEEK_PARTY_POSTED());
        ShowApplications(player); // necessary if player switched tabs before adding this entry (client bug)
    }

    public void UpdateRecruitment(Player player, string message, int groupType)
    {
        int teamId = player.GetCurrentTeamId();
        GroupRecruitment recruitment = recruitments.GetValueOrDefault(teamId == 0 ? player.GetObjectId() : teamId);
        if (recruitment != null)
        {
            recruitment.SetMessage(message);
            recruitment.SetGroupType(groupType);
            recruitment.UpdateLastUpdate();
        }
    }

    public void UpdateApplication(Player player, string message, int groupType, int classId, int level)
    {
        GroupApplication application = applications.GetValueOrDefault(player.GetObjectId());
        if (application != null)
        {
            application.SetMessage(message);
            application.SetGroupType(groupType);
            application.SetClassId(classId);
            application.SetLevel(level);
            application.UpdateLastUpdate();
        }
    }

    public void ShowInstanceGroups(Player player, bool isUpdate)
    {
        List<ServerWideGroup> instanceGroups = this.instanceGroups.Values.Where(group => group.GetRace() == player.GetRace()).ToList();

        if (!isUpdate && GroupConfig.FORM_INSTANCE_GROUP_ANYWHERE)
        {
            List<int> instanceMaskIds = null;
            if (player.GetTarget() is Npc npc)
                instanceMaskIds = DataManager.AUTO_GROUP.GetRecruitableInstanceMaskIds(npc.GetNpcId());
            if (instanceMaskIds == null)
                instanceMaskIds = DataManager.AUTO_GROUP.GetRecruitableInstanceMaskIds();
            PacketSendUtility.SendPacket(player, new SM_FIND_GROUP(instanceMaskIds));
        }

        PacketSendUtility.SendPacket(player, new SM_FIND_GROUP(10, instanceGroups));
    }

    public void ShowInstanceGroups(Player player, Npc portalNpc)
    {
        List<int> instanceMaskIds = DataManager.AUTO_GROUP.GetRecruitableInstanceMaskIds(portalNpc.GetNpcId());
        if (instanceMaskIds != null)
            PacketSendUtility.SendPacket(player, new SM_FIND_GROUP(instanceMaskIds));
    }

    public void RegisterInstanceGroup(Player player, int instanceMaskId, string message, int minMembers)
    {
        ServerWideGroup instanceGroup = new ServerWideGroup(player, instanceMaskId, minMembers, message);
        instanceGroups[player.GetObjectId()] = instanceGroup;
        PacketSendUtility.SendPacket(player, new SM_FIND_GROUP(14, new List<ServerWideGroup> { instanceGroup }));
    }

    public void UpdateInstanceGroup(Player player, string message)
    {
        ServerWideGroup instanceGroup = instanceGroups.GetValueOrDefault(player.GetObjectId());
        if (instanceGroup != null)
        {
            instanceGroup.SetMessage(message);
            instanceGroup.SetLastUpdate();
            ShowInstanceGroups(player, true);
        }
    }

    public void RemoveInstanceGroup(Player player)
    {
        instanceGroups.TryRemove(player.GetObjectId(), out _);
        ShowInstanceGroups(player, true);
    }

    public void ShowInstanceGroupMembersInfo(Player player, int playerObjectId)
    {
        ServerWideGroup instanceGroup = instanceGroups.GetValueOrDefault(playerObjectId);
        if (instanceGroup != null)
            PacketSendUtility.SendPacket(player, new SM_FIND_GROUP(16, new List<ServerWideGroup> { instanceGroup }));
    }

    public void SendInstanceApplication(Player applicant, int playerOrTeamId)
    {
        Player player = World.GetInstance().GetPlayer(playerOrTeamId);
        if (player != null)
            PacketSendUtility.SendPacket(player, new SM_FIND_GROUP(applicant));
    }

    public void SendInstanceApplicationResult(Player responder, int applicantId, byte instanceApplicationReply)
    {
        Player applicant = World.GetInstance().GetPlayer(applicantId);
        if (applicant != null)
        {
            if (instanceApplicationReply == 1)
            {
                ServerWideGroup instanceGroup = instanceGroups.GetValueOrDefault(responder.GetObjectId());
                if (instanceGroup != null)
                {
                    // custom: invite to team to keep it simple, as cross-server recruitment is currently not implemented.
                    // for more info about official server implementation, see CM_/SM_FIND_GROUP action codes 18-25 and
                    // https://forum.aion.gameforge.com/forum/thread/742-server-wide-recruitment-guide-by-kelekelio/
                    if (instanceGroup.GetMinMembers() <= 6)
                        PlayerGroupService.InviteToGroup(responder, applicant);
                    else
                        PlayerAllianceService.InviteToAlliance(responder, applicant);
                }
            }
            else
            {
                PacketSendUtility.SendPacket(applicant, new SM_MESSAGE(responder, ChatUtil.L10n(1400217), ChatType.WHISPER));
            }
        }
    }

    public void OnJoinedTeam(Player player)
    {
        ServerWideGroup instanceGroup = instanceGroups.GetValueOrDefault(player.GetObjectId());
        // custom: team is used as a proxy for a server-wide instance group (forming a team removes instance group registrations on official servers)
        if (instanceGroup != null && instanceGroup.GetMembers().Count >= instanceGroup.GetMinMembers())
            instanceGroups.TryRemove(player.GetObjectId(), out _);
        RemoveApplication(player);
        GroupRecruitment recruitment = RemoveRecruitment(player.GetObjectId(), (byte)NetworkConfig.GAMESERVER_ID, (byte)0, (byte)0, (byte)16);
        TemporaryPlayerTeam<TeamMember<Player>> team = player.GetCurrentTeam();
        if (recruitment != null && team.IsLeader(player))
            AddRecruitment(player, recruitment.GetMessage(), recruitment.GetGroupType());
        else if (team.IsFull())
            RemoveRecruitment(team.GetObjectId(), (byte)NetworkConfig.GAMESERVER_ID, (byte)0, (byte)0, (byte)0);
    }

    public void OnLogout(Player player)
    {
        recruitments.TryRemove(player.GetObjectId(), out _);
        applications.TryRemove(player.GetObjectId(), out _);
        instanceGroups.TryRemove(player.GetObjectId(), out _);
    }

    public static FindGroupService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly FindGroupService instance = new FindGroupService();
    }
}
