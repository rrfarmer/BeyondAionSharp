using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>
/// Automatic Announcement System.
/// Java parity: services/AnnouncementService (Divinity).
/// </summary>
public class AnnouncementService
{
    private static readonly ILogger log = NullLogger.Instance;
    private readonly ConcurrentDictionary<int, ScheduledTask> delays = new ConcurrentDictionary<int, ScheduledTask>();
    private readonly ConcurrentDictionary<int, Announcement> announcements = new ConcurrentDictionary<int, Announcement>();

    private AnnouncementService()
    {
        Reload();
    }

    public static AnnouncementService GetInstance()
    {
        return SingletonHolder.instance;
    }

    /// <summary>Reload the announcements system.</summary>
    public void Reload()
    {
        // Cancel all tasks
        foreach (ScheduledTask delay in delays.Values)
            delay.Cancel();
        delays.Clear();
        announcements.Clear();

        // And load again all announcements
        foreach (Announcement announcement in AnnouncementsDAO.LoadAnnouncements())
            Schedule(announcement);

        log.LogInformation("Loaded " + announcements.Count + " announcements");
    }

    private void Schedule(Announcement announce)
    {
        announcements[announce.GetId()] = announce;
        delays[announce.GetId()] = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            string sender = announce.GetFaction() == null ? "" : announce.GetFaction() == Race.ELYOS ? "Elyos " : "Asmodian ";
            sender += "Announcement";
            string msg = announce.GetChatType() == ChatType.SHOUT || announce.GetChatType() == ChatType.GROUP_LEADER ? "" : sender + ": ";
            msg += announce.GetAnnounce();
            SM_MESSAGE message = new SM_MESSAGE(1, sender, msg, announce.GetChatType());
            PacketSendUtility.BroadcastToWorld(message, player => player.GetOppositeRace() != announce.GetFaction());
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(announce.GetDelay() * 1000), TimeSpan.FromMilliseconds(announce.GetDelay() * 1000));
    }

    public bool AddAnnouncement(string message, string faction, string chatType, int delay)
    {
        int id = AnnouncementsDAO.AddAnnouncement(message, faction, chatType, delay);
        if (id == -1)
            return false;
        Schedule(new Announcement(id, message, faction, chatType, delay));
        return true;
    }

    public bool DelAnnouncement(int id)
    {
        if (!announcements.TryRemove(id, out _) || !AnnouncementsDAO.DelAnnouncement(id))
            return false;
        if (delays.TryRemove(id, out ScheduledTask delay) && delay != null)
            delay.Cancel();
        return true;
    }

    public ICollection<Announcement> GetAnnouncements()
    {
        return announcements.Values;
    }

    private static class SingletonHolder
    {
        internal static readonly AnnouncementService instance = new AnnouncementService();
    }
}
