using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.SkillEngine.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Utils.Audit;

/// <summary>Java parity: utils/audit/GMService (MrPoke, Neon).</summary>
public class GMService
{
    private static readonly ILogger log = NullLogger.Instance;

    public static GMService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private readonly ConcurrentDictionary<int, Aion.GameServer.Model.GameObjects.Player.Player> staffMembers = new();
    private readonly List<SkillTemplate> gmSkills;

    private GMService()
    {
        gmSkills = DataManager.SKILL_DATA.GetSkillTemplates()
            .Where(t => t.GetGroup() != null && t.GetGroup().StartsWith("GM_") || t.GetStack().StartsWith("GM_")).ToList();
        if (gmSkills.Count == 0)
            log.LogWarning("No GM skills found, possibly because of changed or missing skill templates.");
    }

    public ICollection<Aion.GameServer.Model.GameObjects.Player.Player> GetOnlineStaffMembers()
    {
        return staffMembers.Values;
    }

    public void OnPlayerLogin(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        if (player.IsStaff())
        {
            AdminConfig.LOGIN_EXECUTE_COMMANDS.ForEach(cmd => Aion.GameServer.Utils.Chathandlers.ChatProcessor.GetInstance().HandleChatCommand(player, cmd));
            staffMembers[player.GetObjectId()] = player;
            ScheduleBroadcastLogin(player);
        }
    }

    public void OnPlayerLogout(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        if (staffMembers.TryRemove(player.GetObjectId(), out _) && IsAnnounceable(player))
            BroadcastConnectionStatus(player, false);
    }

    public bool IsAnnounceable(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        return player.IsOnline() && player.IsStaff() && !player.IsInCustomState(CustomPlayerState.NO_WHISPERS_MODE)
            && player.GetFriendList().GetStatus() != FriendList.Status.OFFLINE
            && (AdminConfig.ANNOUNCE_LEVELS.Contains(player.GetAccount().GetAccessLevel().ToString()) || AdminConfig.ANNOUNCE_LEVELS.Contains("*"));
    }

    private void BroadcastConnectionStatus(Aion.GameServer.Model.GameObjects.Player.Player gm, bool connected)
    {
        string name = Aion.GameServer.Utils.ChatUtil.Name(gm);
        Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage sysMsg = connected
            ? Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_NOTIFY_LOGIN_BUDDY(name)
            : Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_NOTIFY_LOGOFF_BUDDY(name);

        if ((connected && AdminConfig.ANNOUNCE_LOGIN_TO_ALL_PLAYERS) || (!connected && AdminConfig.ANNOUNCE_LOGOUT_TO_ALL_PLAYERS))
        {
            Aion.GameServer.Utils.PacketSendUtility.BroadcastToWorld(sysMsg, p => !p.Equals(gm));
        }
        else
        {
            Aion.GameServer.Utils.PacketSendUtility.BroadcastToWorld(sysMsg, p => p.IsStaff() && !p.Equals(gm));
        }
    }

    private void ScheduleBroadcastLogin(Aion.GameServer.Model.GameObjects.Player.Player gm)
    {
        if (!IsAnnounceable(gm))
            return;

        byte delay = 15;
        Aion.GameServer.Utils.PacketSendUtility.SendMessage(gm,
            "Your login will be announced in " + delay + "s.\nYou can disable this by setting whisper off or changing your online status to invisible.");
        Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (IsAnnounceable(gm))
            {
                BroadcastConnectionStatus(gm, true);
                Aion.GameServer.Utils.PacketSendUtility.SendMessage(gm, "Your login has been announced.");
            }
            else
            {
                Aion.GameServer.Utils.PacketSendUtility.SendMessage(gm, "Your login has not been announced.");
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(delay * 1000));
    }

    public void AddGmSkills(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        foreach (SkillTemplate t in gmSkills)
        {
            switch (t.GetSkillId())
            {
                case 322: // [Event] Manastone Preservation
                case 323: // Homerun Energy
                case 339: // Panesterra Dominant
                    continue;
            }
            if (player.GetRace() == Race.ASMODIANS && t.GetStack().Contains("_LIGHT"))
                continue;
            if (player.GetRace() == Race.ELYOS && t.GetStack().Contains("_DARK"))
                continue;
            Aion.GameServer.Services.SkillLearnService.LearnTemporarySkill(player, t.GetSkillId(), t.GetLvl());
        }
    }

    private static class SingletonHolder
    {
        internal static readonly GMService instance = new GMService();
    }
}
