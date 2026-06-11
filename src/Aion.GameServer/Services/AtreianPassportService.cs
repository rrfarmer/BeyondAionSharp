using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.Time;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/AtreianPassportService (ViAl, Luzien, SVDNESS). Daily attendance rewards. java.time→C#: LocalDateTime→DateTime, LocalDate→DateOnly, LocalTime.MAX→TimeOnly.MaxValue, Instant/Timestamp→DateTimeOffset (toInstant→DateTimeOffset itself; plusSeconds→AddSeconds; isAfter→&gt;; isBefore→&lt;; truncatedTo(SECONDS)→FromUnixTimeSeconds); Optional.map.orElse(null)→?.; stream max/min(comparing)→MaxBy/MinBy; switch-arrows→switch; PersistentState→IPersistable.PersistentState; Quartz JobDetail→IJobDetail. CronService/AccountPassportsDAO/PassportsList red-tolerated.</summary>
public class AtreianPassportService
{
    private const string DAILY_CRON_AT_09_00 = "0 0 9 ? * *";
    private const int ATTEND_RESET_HOUR = 9;
    private readonly DateTime? expireDate;
    private IJobDetail cronInfo;

    private AtreianPassportService()
    {
        expireDate = CalculatePassportExpireDate();
        if (!IsAtreianPassportDisabled())
        {
            cronInfo = CronService.GetInstance().Schedule(() =>
            {
                if (IsAtreianPassportDisabled())
                {
                    CronService.GetInstance().Cancel(cronInfo);
                    cronInfo = null;
                    return;
                }
                bool isFirstDayOfMonth = ServerTime.Now().Day == 1;
                AccountPassportsDAO.ResetAllLastStamps();
                if (isFirstDayOfMonth)
                {
                    AccountPassportsDAO.ResetAllStamps();
                }
                World.GetInstance().ForEachPlayer(player =>
                {
                    var acc = player.GetAccount();
                    acc.SetLastStamp(null);
                    if (isFirstDayOfMonth)
                    {
                        acc.SetPassportStamps(0);
                    }
                    OnLogin(player);
                });
            }, DAILY_CRON_AT_09_00);
        }
    }

    public bool IsAtreianPassportDisabled()
    {
        return IsAtreianPassportDisabled(ServerTime.Now().DateTime);
    }

    private bool IsAtreianPassportDisabled(DateTime checkDateTime)
    {
        return expireDate != null && checkDateTime > expireDate.Value;
    }

    private DateTime? FindLastRewardTime()
    {
        var lastPossibleReward = DataManager.ATREIAN_PASSPORT_DATA.GetAll().Values
            .Where(v => v.GetAttendType() == AttendType.DAILY || v.GetAttendType() == AttendType.CUMULATIVE)
            .MaxBy(v => v.GetPeriodEnd());
        return lastPossibleReward?.GetPeriodEnd();
    }

    private DateTime? CalculatePassportExpireDate()
    {
        var disableDateTime = FindLastRewardTime();
        if (disableDateTime == null)
        {
            return null;
        }
        return disableDateTime.Value.Date.Add(TimeOnly.MaxValue.ToTimeSpan()).AddDays(14);
    }

    public void TakeReward(Player player, Dictionary<int, ISet<int>> passports)
    {
        if (IsAtreianPassportDisabled())
        {
            return;
        }
        List<Passport> toRemove = new List<Passport>();
        PassportsList ppl = player.GetAccount().GetPassportsList();
        foreach (var entry in passports)
        {
            int passId = entry.Key;
            foreach (var time in entry.Value)
            {
                var passport = ppl.GetPassport(passId, time);
                if (passport == null)
                {
                    AuditLogger.Log(player, "tried to get non-existing passport (ID: " + passId + ", time: " + time + ").");
                    continue;
                }
                if (passport.IsRewarded() || passport.GetPersistentState() == IPersistable.PersistentState.DELETED)
                {
                    AuditLogger.Log(player, "tried to get passport which is already rewarded (ID: " + passId + ").");
                    continue;
                }
                if (player.GetInventory().IsFull())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WAREHOUSE_FULL_INVENTORY());
                    break;
                }
                var atp = DataManager.ATREIAN_PASSPORT_DATA.GetAtreianPassportId(passId);
                int minLevel = atp.GetRewardPermitLevel();
                if (minLevel > 0 && player.GetLevel() < minLevel)
                {
                    string itemName = "";
                    var itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(atp.GetRewardItemId());
                    if (itemTemplate != null && itemTemplate.GetL10n() != null)
                    {
                        itemName = itemTemplate.GetL10n();
                    }
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ATTEND_REWARD_INVALID_LEVEL(minLevel, itemName));
                    continue;
                }
                int expireMin = atp.GetRewardExpireMinutes();
                if (expireMin > 0)
                {
                    DateTimeOffset deadline = passport.GetArriveDate().AddSeconds(expireMin * 60L);
                    if (DateTimeOffset.UtcNow > deadline)
                    {
                        passport.SetPersistentState(IPersistable.PersistentState.DELETED);
                        ppl.RemovePassport(passport);
                        toRemove.Add(passport);
                        continue;
                    }
                }
                ItemService.AddItem(player, atp.GetRewardItemId(), atp.GetRewardItemCount(), true, new ItemService.ItemUpdatePredicate(ItemPacketService.ItemAddType.ITEM_COLLECT, ItemPacketService.ItemUpdateType.INC_PASSPORT_ADD));
                passport.SetRewarded(true);
                passport.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                toRemove.Add(passport);
            }
        }
        if (toRemove.Count != 0)
        {
            AccountPassportsDAO.StorePassportList(player.GetAccount().GetId(), toRemove);
        }
        OnLogin(player);
    }

    public void OnLogin(Player player)
    {
        var now = ServerTime.Now().DateTime;
        if (IsAtreianPassportDisabled(now))
        {
            return;
        }
        PurgeExpiredPassports(player);
        Account pa = player.GetAccount();
        bool doReward = CheckOnlineDate(pa, now) && pa.GetPassportStamps() < 28;
        foreach (var atp in DataManager.ATREIAN_PASSPORT_DATA.GetAll().Values)
        {
            if (atp.IsActive() && atp.GetPeriodStart() < now && atp.GetPeriodEnd() > now)
            {
                switch (atp.GetAttendType())
                {
                    case AttendType.DAILY:
                        if (doReward)
                        {
                            DateOnly attendDay = GetAttendDay(now);
                            if (!pa.GetPassportsList().HasPassportForDay(atp.GetId(), attendDay))
                            {
                                var ts = NowTs();
                                var passport = new Passport(atp.GetId(), false, ts);
                                passport.SetPersistentState(IPersistable.PersistentState.NEW);
                                pa.GetPassportsList().AddPassport(passport);
                            }
                        }
                        break;
                    case AttendType.CUMULATIVE:
                        if (doReward && atp.GetAttendNum() == pa.GetPassportStamps() + 1)
                        {
                            var ts = NowTs();
                            var passport = new Passport(atp.GetId(), false, ts);
                            passport.SetPersistentState(IPersistable.PersistentState.NEW);
                            pa.GetPassportsList().AddPassport(passport);
                        }
                        else if (!pa.GetPassportsList().IsPassportPresent(atp.GetId()))
                        {
                            var ts = NowTs();
                            var passport = new Passport(atp.GetId(), false, ts);
                            passport.SetFakeStamp(true);
                            if (atp.GetAttendNum() <= pa.GetPassportStamps())
                            {
                                passport.SetRewarded(true);
                            }
                            pa.GetPassportsList().AddPassport(passport);
                        }
                        break;
                    case AttendType.ANNIVERSARY:
                    {
                        int monthsAlive = GetAccountAgeInMonths(player, DateOnly.FromDateTime(now));
                        int target = atp.GetAttendNum();
                        if (pa.GetPassportsList().IsPassportPresent(atp.GetId()))
                        {
                            break;
                        }
                        if (monthsAlive == target)
                        {
                            var ts = NowTs();
                            var passport = new Passport(atp.GetId(), false, ts);
                            passport.SetPersistentState(IPersistable.PersistentState.NEW);
                            pa.GetPassportsList().AddPassport(passport);
                        }
                        else if (monthsAlive > target)
                        {
                            var ts = NowTs();
                            var passport = new Passport(atp.GetId(), false, ts);
                            passport.SetFakeStamp(true);
                            passport.SetRewarded(true);
                            pa.GetPassportsList().AddPassport(passport);
                        }
                        break;
                    }
                }
            }
        }
        if (doReward)
        {
            pa.IncreasePassportStamps();
            pa.SetLastStamp(NowTs());
            CheckPassportLimit(player);
            AccountPassportsDAO.StorePassport(pa);
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ATTEND_MSG_ATTEND_REWARD_GET());
        }
        SendPassport(player);
    }

    private void SendPassport(Player player)
    {
        Account pa = player.GetAccount();
        DateOnly playerCreationDate = DateOnly.FromDateTime(ServerTime.AtDate(player.GetCreationDate()).DateTime);
        PacketSendUtility.SendPacket(player, new SM_ATREIAN_PASSPORT(pa.GetPassportsList(), pa.GetPassportStamps(), playerCreationDate));
    }

    private bool CheckOnlineDate(Account pa, DateTime now)
    {
        DateTimeOffset? last = pa.GetLastStamp();
        if (last == null)
        {
            return true;
        }
        var lastAttendDay = GetAttendDay(ServerTime.AtDate(last.Value).DateTime);
        DateOnly currentAttendDay = GetAttendDay(now);
        return !currentAttendDay.Equals(lastAttendDay);
    }

    private DateOnly GetAttendDay(DateTime serverTime)
    {
        return DateOnly.FromDateTime(serverTime.AddHours(-ATTEND_RESET_HOUR));
    }

    private void CheckPassportLimit(Player player)
    {
        Account pa = player.GetAccount();
        var pl = pa.GetPassportsList().GetAllPassports();
        // More than 50 passports cannot be saved.
        if (pl.Count < 50)
        {
            return;
        }
        var oldest = pl
            .Where(pp => !pp.IsFakeStamp())
            .MinBy(pp => pp.GetArriveDate());
        if (oldest == null)
        {
            oldest = pl
                .MinBy(pp => pp.GetArriveDate());
        }
        if (oldest != null)
        {
            oldest.SetPersistentState(IPersistable.PersistentState.DELETED);
            pa.GetPassportsList().RemovePassport(oldest);
            AccountPassportsDAO.StorePassportList(pa.GetId(), new List<Passport> { oldest });
            var itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(oldest.GetTemplate().GetRewardItemId());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ATTEND_REWARD_REMOVE_EXCESS(itemTemplate.GetL10n()));
        }
    }

    private void PurgeExpiredPassports(Player player)
    {
        Account pa = player.GetAccount();
        PassportsList ppl = pa.GetPassportsList();
        DateTimeOffset now = ServerTime.Now();
        List<Passport> toRemove = new List<Passport>();
        foreach (var pp in new List<Passport>(ppl.GetAllPassports()))
        {
            if (pp.IsRewarded() || pp.IsFakeStamp())
            {
                continue;
            }
            var atp = DataManager.ATREIAN_PASSPORT_DATA.GetAtreianPassportId(pp.GetId());
            if (atp == null)
            {
                continue;
            }
            int expireMin = atp.GetRewardExpireMinutes();
            if (expireMin <= 0)
            {
                continue;
            }
            DateTimeOffset deadline = pp.GetArriveDate().AddSeconds(expireMin * 60L);
            if (now > deadline)
            {
                pp.SetPersistentState(IPersistable.PersistentState.DELETED);
                ppl.RemovePassport(pp);
                toRemove.Add(pp);
            }
        }
        if (toRemove.Count != 0)
        {
            AccountPassportsDAO.StorePassportList(pa.GetId(), toRemove);
        }
    }

    /// <summary>
    /// Calculates the number of full months between the account creation date and the given date. The calculation is based on
    /// year and month difference. If the day of the given date is earlier than the day of the creation date, the current month
    /// is considered incomplete and is not counted. The returned value is always non-negative.
    /// </summary>
    private int GetAccountAgeInMonths(Player player, DateOnly now)
    {
        DateOnly creationDate = DateOnly.FromDateTime(ServerTime.AtDate(player.GetCreationDate()).DateTime);
        int months = (now.Year - creationDate.Year) * 12 + (now.Month - creationDate.Month);
        if (now.Day < creationDate.Day)
        {
            months--;
        }
        return Math.Max(0, months);
    }

    private static DateTimeOffset NowTs()
    {
        return DateTimeOffset.FromUnixTimeSeconds(ServerTime.Now().ToUnixTimeSeconds());
    }

    private static class SingletonHolder
    {
        internal static readonly AtreianPassportService instance = new AtreianPassportService();
    }

    public static AtreianPassportService GetInstance()
    {
        return SingletonHolder.instance;
    }
}
