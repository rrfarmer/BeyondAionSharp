using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Autogroup;

/// <summary>Java parity: services/autogroup/AutoGroupUtility (Estrayl). Registration gating for the auto-group / instance matchmaking system: canRegisterNewEntry/QuickEntry/GroupEntry, checkGroupRequirements (leader, member-count caps for periodic/harmony arenas, per-member item/cooldown/level/searching checks), sendSuccessfulRegistration, sendWindow*, hasCoolDown. TemporaryPlayerTeam&lt;?&gt;-><TeamMember<Player>>; keySet->Keys; AutoGroupType/LookingForParty/SM_ packets red-tolerated.</summary>
public class AutoGroupUtility
{
    public static bool CanRegisterNewEntry(Player player, AutoGroupType agt)
    {
        if (!agt.GetTemplate().CanRegisterNewEntry())
            return false;
        if (player.IsInTeam())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_NOT_LEADER());
            return false;
        }
        return true;
    }

    public static bool CanRegisterQuickEntry(Player player, AutoGroupType agt)
    {
        if (!agt.GetTemplate().CanRegisterQuickEntry())
            return false;
        if (player.IsInTeam())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_NOT_LEADER());
            return false;
        }
        return true;
    }

    public static bool CanRegisterGroupEntry(Player player, AutoGroupType agt, int mapId, int maskId)
    {
        return agt.GetTemplate().HasRegisterGroup() && CheckGroupRequirements(player, agt, mapId, maskId);
    }

    public static bool CheckGroupRequirements(Player player, AutoGroupType agt, int mapId, int maskId)
    {
        TemporaryPlayerTeam<TeamMember<Player>> team = player.GetCurrentTeam();
        if (team == null || !team.IsLeader(player))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_NOT_LEADER());
            return false;
        }
        if (agt.IsPeriodicInstance())
        {
            int maxMemberPerTeam = DataManager.INSTANCE_COOLTIME_DATA.GetMaxMemberCount(agt.GetTemplate().GetInstanceMapId(), player.GetRace());
            if (team.Size() > maxMemberPerTeam)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_TOO_MANY_MEMBERS(maxMemberPerTeam, mapId));
                return false;
            }
        }
        else if (agt.IsHarmonyArena() || agt.IsTrainingHarmonyArena())
        {
            if (team.Size() > 3)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_TOO_MANY_MEMBERS(3, mapId));
                return false;
            }
        }

        foreach (Player member in team.GetMembers())
        {
            if (team.GetLeaderObject().Equals(member))
            {
                continue;
            }
            if (agt.IsHarmonyArena() && !PvPArenaService.CheckItem(member, agt))
            {
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM());
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_ENTER_MEMBER(member.GetName()));
                return false;
            }
            if (HasCoolDown(member, mapId) || !agt.IsInLvlRange(member.GetLevel()) || AutoGroupService.GetInstance().IsSearching(member, maskId))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_ENTER_MEMBER(member.GetName()));
                return false;
            }
        }
        return true;
    }

    public static void SendSuccessfulRegistration(LookingForParty lfp, string leaderName, AutoGroupType agt, int maskId)
    {
        foreach (int objectId in lfp.GetMembers().Keys)
        {
            Player player = World.GetInstance().GetPlayer(objectId);
            if (player != null)
            {
                if (agt.IsPeriodicInstance())
                    PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(maskId, 6, true));
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_REGISTER_SUCCESS());
                PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(maskId, 1, lfp.GetEntryRequestType().GetId(), leaderName));
            }
        }
    }

    public static void SendWindowToPlayerIfOnline(int objectId, int maskId, int windowId)
    {
        Player player = World.GetInstance().GetPlayer(objectId);
        if (player != null)
            SendWindowToPlayer(player, maskId, windowId);
    }

    public static void SendWindowToPlayer(Player player, int maskId, int windowId)
    {
        PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(maskId, windowId));
    }

    public static bool HasCoolDown(Player player, int worldId)
    {
        return player.GetPortalCooldownList().IsPortalUseDisabled(worldId);
    }
}
