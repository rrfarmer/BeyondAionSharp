using Aion.GameServer.Utils.Stats;
using System;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/AbyssRank implements Persistable.</summary>
public class AbyssRank : IPersistable
{
    private int dailyAP;
    private int weeklyAP;
    private int currentAp;
    private int lastAP;
    private int dailyGP;
    private int weeklyGP;
    private int currentGp;
    private int lastGP;
    private Aion.GameServer.Utils.Stats.AbyssRankEnum rank;
    private IPersistable.PersistentState persistentState;
    private int dailyKill;
    private int weeklyKill;
    private int allKill;
    private int maxRank;
    private int lastKill;
    private long lastUpdate;

    public AbyssRank(int dailyAP, int weeklyAP, int ap, int rank, int dailyKill, int weeklyKill, int allKill, int maxRank, int lastKill, int lastAP,
        long lastUpdate, int daily_gp, int weekly_gp, int gp, int last_gp)
    {
        this.dailyAP = dailyAP;
        this.weeklyAP = weeklyAP;
        this.currentAp = ap;
        this.rank = Aion.GameServer.Utils.Stats.AbyssRankEnumExtensions.GetRankById(rank);
        this.dailyKill = dailyKill;
        this.weeklyKill = weeklyKill;
        this.allKill = allKill;
        this.maxRank = maxRank;
        this.lastKill = lastKill;
        this.lastAP = lastAP;
        this.lastUpdate = lastUpdate;
        this.dailyGP = daily_gp;
        this.weeklyGP = weekly_gp;
        this.currentGp = gp > 0 ? gp : 0;
        this.lastGP = last_gp;

        DoUpdate();
    }

    public enum AbyssRankUpdateType
    {
        PLAYER_ELYOS = 1,
        PLAYER_ASMODIANS = 2,
        LEGION_ELYOS = 4,
        LEGION_ASMODIANS = 8,
    }

    /// <summary>Add AP to a player (current player AP + added AP)</summary>
    public void AddAp(int additionalAp)
    {
        if (additionalAp > 0)
        {
            dailyAP += additionalAp;
            if (dailyAP < 0)
                dailyAP = 0;

            weeklyAP += additionalAp;
            if (weeklyAP < 0)
                weeklyAP = 0;
        }

        int cappedCount;
        if (Aion.GameServer.Configs.Main.CustomConfig.ENABLE_AP_CAP)
            cappedCount = currentAp + additionalAp > Aion.GameServer.Configs.Main.CustomConfig.AP_CAP_VALUE ? (int)(Aion.GameServer.Configs.Main.CustomConfig.AP_CAP_VALUE - currentAp) : additionalAp;
        else
            cappedCount = additionalAp;

        currentAp += cappedCount;
        if (currentAp < 0)
            currentAp = 0;

        Aion.GameServer.Utils.Stats.AbyssRankEnum newRank = Aion.GameServer.Utils.Stats.AbyssRankEnumExtensions.GetRankForPoints(currentAp, currentGp);
        if (newRank.GetRequiredGP() == 0)
            SetRank(newRank);
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>
    /// Do not use this method directly. Add or subtract GP by using GloryPointsService.
    /// </summary>
    /// <param name="amount">Amount of GloryPoints to add</param>
    /// <param name="addToStats">true, if daily and weekly gp stats should be modified</param>
    public void AddGp(int amount, bool addToStats)
    {
        if (addToStats)
        {
            dailyGP += amount;
            weeklyGP += amount;
        }
        currentGp += amount;
        if (currentGp < 0)
            currentGp = 0;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>The daily Abyss Point count</summary>
    public int GetDailyAP()
    {
        return dailyAP;
    }

    /// <summary>The weekly Abyss Point count</summary>
    public int GetWeeklyAP()
    {
        return weeklyAP;
    }

    // Property-form bridges over getAp()/getRank() (reworked call sites use abyssRank.Ap, abyssRank.Rank as ints).
    public int Ap => GetAp();
    public int Rank => Aion.GameServer.Utils.Stats.AbyssRankEnumExtensions.GetId(GetRank());

    /// <summary>The all time Abyss Point count</summary>
    public int GetAp()
    {
        return currentAp;
    }

    /// <summary>the rank</summary>
    public Aion.GameServer.Utils.Stats.AbyssRankEnum GetRank()
    {
        return rank;
    }

    public void SetRank(Aion.GameServer.Utils.Stats.AbyssRankEnum rank)
    {
        if (rank.GetId() > this.maxRank)
            this.maxRank = rank.GetId();

        this.rank = rank;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>The daily count kill</summary>
    public int GetDailyKill()
    {
        return dailyKill;
    }

    /// <summary>The weekly count kill</summary>
    public int GetWeeklyKill()
    {
        return weeklyKill;
    }

    /// <summary>all Kill</summary>
    public int GetAllKill()
    {
        return allKill;
    }

    /// <summary>Add one kill to a player</summary>
    public void IncrementAllKills()
    {
        dailyKill++;
        weeklyKill++;
        allKill++;
    }

    /// <summary>max Rank</summary>
    public int GetMaxRank()
    {
        return maxRank;
    }

    /// <summary>The last week count kill</summary>
    public int GetLastKill()
    {
        return lastKill;
    }

    /// <summary>The last week Abyss Point count</summary>
    public int GetLastAP()
    {
        return lastAP;
    }

    /// <summary>the persistentState</summary>
    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    /// <param name="persistentState">the persistentState to set</param>
    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        if (persistentState != IPersistable.PersistentState.UPDATE_REQUIRED || this.persistentState != IPersistable.PersistentState.NEW)
            this.persistentState = persistentState;
    }

    /// <summary>The last update of the AbyssRank</summary>
    public long GetLastUpdate()
    {
        return lastUpdate;
    }

    /// <summary>Make an update for the daily/weekly/last kill &amp; ap counts</summary>
    public void DoUpdate()
    {
        bool needUpdate = false;
        DateTimeOffset lastTime = Aion.GameServer.Utils.Time.ServerTime.OfEpochMilli(lastUpdate);
        DateTimeOffset now = Aion.GameServer.Utils.Time.ServerTime.Now();

        // Checking the day - month & year are checked to prevent if a player come back after 1 month, the same day
        if (lastTime.Day != now.Day || lastTime.Month != now.Month || lastTime.Year != now.Year)
        {
            dailyAP = 0;
            dailyKill = 0;
            dailyGP = 0;
            needUpdate = true;
        }

        // Checking the week - year is checked to prevent if a player come back after 1 year, the same week
        if (System.Globalization.ISOWeek.GetWeekOfYear(lastTime.DateTime) != System.Globalization.ISOWeek.GetWeekOfYear(now.DateTime) || lastTime.Year != now.Year)
        {
            lastKill = weeklyKill;
            lastAP = weeklyAP;
            lastGP = weeklyGP;
            weeklyKill = 0;
            weeklyAP = 0;
            weeklyGP = 0;
            needUpdate = true;
        }

        // For offline changed ranks
        if (rank.GetId() > maxRank)
        {
            maxRank = rank.GetId();
            needUpdate = true;
        }

        // Finally, update the the last update
        lastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (needUpdate)
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public int GetDailyGP()
    {
        return dailyGP;
    }

    public int GetWeeklyGP()
    {
        return weeklyGP;
    }

    public int GetCurrentGP()
    {
        return currentGp;
    }

    public int GetLastGP()
    {
        return lastGP;
    }
}

/// <summary>Java parity: AbyssRank.AbyssRankUpdateType.value() — enum int payload exposed as an extension.</summary>
public static class AbyssRankUpdateTypeExtensions
{
    public static int Value(this AbyssRank.AbyssRankUpdateType type)
    {
        return (int)type;
    }
}
