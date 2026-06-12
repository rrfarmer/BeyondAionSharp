using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/Siege&lt;SL extends SiegeLocation&gt; (SoulKeeper, Source) abstract base. C# generic where SL : SiegeLocation; AtomicBoolean finished; synchronized start->lock double-start guard; CompareAndSet stop (+balaur auto-assault); siege counter/boss/timing; abstract onSiegeStart/Finish/isEndless/onAbyssPointsAdded; initSiegeBoss (find BOSS npc), spawn/despawn/broadcast helpers, sendRewardsToParticipants (top-grade GP/kinah/item mail distribution). currentTimeMillis->UtcNow; log.error(msg,ex)->LogError(ex,msg); Math.round(float)->(long)Floor(x+0.5f); getClass().getSimpleName()->GetType().Name. SiegeLocation/SiegeNpc/SiegeResult/templates red-tolerated.</summary>
public abstract class Siege<SL> where SL : SiegeLocation
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("SIEGE_LOG");
    private readonly AtomicBoolean finished = new AtomicBoolean();
    private readonly SiegeCounter siegeCounter = new SiegeCounter();
    private readonly SL siegeLocation;
    private bool bossKilled;
    private SiegeNpc boss;
    private long startTime;
    private bool started;

    public Siege(SL siegeLocation)
    {
        this.siegeLocation = siegeLocation;
    }

    public void StartSiege()
    {
        bool doubleStart = false;

        // keeping synchronization as minimal as possible
        lock (this)
        {
            if (started)
            {
                doubleStart = true;
            }
            else
            {
                startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                started = true;
            }
        }

        if (doubleStart)
        {
            log.LogError(new InvalidOperationException(), "Attempt to start " + this + " twice");
        }
        else
        {
            OnSiegeStart();
        }
    }

    public void StartSiege(int locationId)
    {
        SiegeService.GetInstance().StartSiege(locationId);
    }

    public void StopSiege()
    {
        if (finished.CompareAndSet(false, true))
        {
            OnSiegeFinish();

            if (SiegeConfig.BALAUR_AUTO_ASSAULT)
            {
                BalaurAssaultService.GetInstance().OnSiegeFinish(this);
            }
        }
        else
        {
            log.LogError(new InvalidOperationException(), "Attempt to stop " + this + " twice");
        }
    }

    public SL GetSiegeLocation()
    {
        return siegeLocation;
    }

    public int GetSiegeLocationId()
    {
        return siegeLocation.GetLocationId();
    }

    public bool IsBossKilled()
    {
        return bossKilled;
    }

    public void SetBossKilled(bool bossKilled)
    {
        this.bossKilled = bossKilled;
    }

    public SiegeNpc GetBoss()
    {
        return boss;
    }

    public void SetBoss(SiegeNpc boss)
    {
        this.boss = boss;
    }

    public SiegeCounter GetSiegeCounter()
    {
        return siegeCounter;
    }

    public SiegeRaceCounter GetWinnerRaceCounter()
    {
        return siegeCounter.GetWinnerRaceCounter(GetSiegeLocation().GetRace());
    }

    protected abstract void OnSiegeStart();

    protected abstract void OnSiegeFinish();

    public abstract bool IsEndless();

    public abstract void OnAbyssPointsAdded(Player player, int abyssPoints);

    public bool IsStarted()
    {
        return started;
    }

    public bool IsFinished()
    {
        return finished.Get();
    }

    public long GetStartTime()
    {
        return startTime;
    }

    protected void InitSiegeBoss()
    {
        SiegeNpc boss = null;

        ICollection<SiegeNpc> npcs = Aion.GameServer.World.World.GetInstance().GetLocalSiegeNpcs(GetSiegeLocationId());
        foreach (SiegeNpc npc in npcs)
        {
            if (npc.GetObjectTemplate().GetAbyssNpcType().Equals(AbyssNpcType.BOSS))
            {
                if (boss != null)
                    throw new SiegeException("Found 2 siege bosses for outpost " + GetSiegeLocationId());

                boss = npc;
            }
        }
        if (boss == null)
            throw new SiegeException("Siege Boss not found for siege " + GetSiegeLocationId());

        SetBoss(boss);
    }

    protected void SpawnNpcs(int locationId, SiegeRace race, SiegeModType type)
    {
        SiegeService.GetInstance().SpawnNpcs(locationId, race, type);
    }

    protected void DespawnNpcs(int locationId)
    {
        SiegeService.GetInstance().DeSpawnNpcs(locationId);
    }

    protected void BroadcastState(SiegeLocation location)
    {
        PacketSendUtility.BroadcastToWorld(new SM_SIEGE_LOCATION_STATE(location));
    }

    protected void BroadcastUpdate(SiegeLocation location)
    {
        SiegeService.GetInstance().BroadcastUpdate(location);
    }

    protected void UpdateOutpostStatusByFortress(FortressLocation location)
    {
        SiegeService.GetInstance().UpdateOutpostSiegeState(location);
    }

    protected void SendRewardsToParticipants(SiegeRaceCounter raceCounter, SiegeResult raceResult)
    {
        try
        {
            Dictionary<int, long> playerAbyssPoints = raceCounter.GetPlayerAbyssPoints();
            List<int> topPlayersIds = new List<int>(playerAbyssPoints.Keys);
            bool isWinner = raceResult == SiegeResult.OCCUPY || raceResult == SiegeResult.DEFENDER;

            int playerIndex = 0;
            int rewardLevel = 0;
            long timeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (SiegeReward topGrade in GetSiegeLocation().GetRewards())
            {
                List<int> rewardedGpPlayers = new List<int>();
                long kinahRewardForRewardLevel = GetSiegeLocation().GetTemplate().GetKinahRewardByRewardLevel(rewardLevel);
                AbyssSiegeLevel level = AbyssSiegeLevelExtensions.GetLevelById(++rewardLevel);
                int gp = isWinner ? topGrade.GetGpForWin() : topGrade.GetGpForDefeat();

                for (int i = 0; i < topGrade.GetTop() && playerIndex < topPlayersIds.Count; i++, playerIndex++)
                {
                    int playerId = topPlayersIds[playerIndex];
                    long kinahReward = (long)Math.Floor(kinahRewardForRewardLevel / (float)topGrade.GetTop() + 0.5f);
                    if (kinahReward > 40000000)
                        kinahReward = 40000000;
                    if (isWinner && topGrade.HasItemRewardsForWin())
                        MailFormatter.SendAbyssRewardMail(GetSiegeLocation(), PlayerService.GetOrLoadPlayerCommonData(playerId), level, raceResult, timeMillis,
                            topGrade.GetItemId(), topGrade.GetItemCount(), kinahReward);
                    else if (!isWinner && topGrade.HasItemRewardsForDefeat())
                        MailFormatter.SendCustomAbyssDefeatRewardMail(PlayerService.GetOrLoadPlayerCommonData(playerId), topGrade.GetItemIdDefeat(), topGrade.GetItemCountDefeat());

                    if (gp > 0)
                    {
                        rewardedGpPlayers.Add(playerId);
                        GloryPointsService.AddGp(playerId, gp); // Rates.GP.calcResult() cannot be fairly applied here, because some players could be offline
                    }
                }
                if (LoggingConfig.LOG_SIEGE && rewardedGpPlayers.Count != 0)
                {
                    log.LogInformation(this + ": Distributed " + gp + " " + (isWinner ? "winner" : "loser") + " GP each, to the following players (rank " + rewardLevel
                        + "): " + rewardedGpPlayers);
                }
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while distributing rewards for " + this);
        }
    }

    public override string ToString()
    {
        return GetType().Name + " [locationId=" + GetSiegeLocationId() + "]";
    }
}
