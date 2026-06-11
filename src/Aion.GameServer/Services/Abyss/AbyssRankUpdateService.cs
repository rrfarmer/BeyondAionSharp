using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;
using RankingListPlayerGp = Aion.GameServer.Dao.AbyssRankDAO.RankingListPlayerGp;

namespace Aion.GameServer.Services.Abyss;

/// <summary>Java parity: services/abyss/AbyssRankUpdateService (ATracer, Neon). scheduleUpdate (cron rank update + daily GP loss), performUpdate (online doUpdate+store, compute min GP rank/quota limit, update ranking lists, quota ranks per race, reload cache), updateQuotaRanksForRace (iterate ranks high->low assigning quota), selectAndUpdateQuotaRank (iterator.remove->RemoveAt), updateToNoQuotaRank, updateRankTo, updateDailyGpLoss. method-ref::->method group (red-tolerated Runnable); ThreadPoolManager.schedule(Runnable,ms)->Schedule(ct-lambda); streams min/max->LINQ OrderBy/DefaultIfEmpty; Map.forEach->foreach; currentTimeMillis->UtcNow. AbyssRankEnum/AbyssRankDAO records red-tolerated.</summary>
public class AbyssRankUpdateService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AbyssRankUpdateService));

    private AbyssRankUpdateService()
    {
    }

    public static void ScheduleUpdate()
    {
        log.LogInformation("Scheduling ranking update task based on cron expression: " + RankingConfig.TOP_RANKING_UPDATE_RULE);
        CronService.GetInstance().Schedule(PerformUpdate, RankingConfig.TOP_RANKING_UPDATE_RULE, true);

        log.LogInformation("Scheduling daily GP loss task based on cron expression: " + RankingConfig.TOP_RANKING_DAILY_GP_LOSS_TIME);
        CronService.GetInstance().Schedule(UpdateDailyGpLoss, RankingConfig.TOP_RANKING_DAILY_GP_LOSS_TIME, true);
    }

    /// <summary>
    /// Perform update of all ranks
    /// </summary>
    public static void PerformUpdate()
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            log.LogInformation("AbyssRankUpdateService: Executing rank update...");
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // update and store rank statistics for all online players (offline players update on login)
            World.GetInstance().ForEachPlayer(player =>
            {
                player.GetAbyssRank().DoUpdate();
                AbyssRankDAO.StoreAbyssRank(player);
            });

            AbyssRankEnum minGpRank = AbyssRankEnum.Values()
                    .Where(rank => rank.GetRequiredGP() > 0)
                    .OrderBy(rank => rank.GetId()).FirstOrDefault() ?? AbyssRankEnum.STAR1_OFFICER;
            int playerLimit = AbyssRankEnum.Values()
                    .Where(rank => rank.GetRequiredGP() > 0)
                    .Select(rank => rank.GetQuota())
                    .DefaultIfEmpty(1000).Max();

            // update and store player & legion DB rank_pos entries (for ▼/▲/= trend in the ranking table)
            AbyssRankDAO.UpdateRankingLists(RankingConfig.TOP_RANKING_MAX_OFFLINE_DAYS, playerLimit, RankingConfig.RANKING_LIST_LEGION_LIMIT);
            // update and store GP ranks
            UpdateQuotaRanksForRace(Race.ASMODIANS, minGpRank);
            UpdateQuotaRanksForRace(Race.ELYOS, minGpRank);

            // update ranking cache
            AbyssRankingCache.GetInstance().ReloadRankings();

            log.LogInformation("AbyssRankUpdateService: Finished in " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime) / 1000 + "s");
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(1000));
    }

    /// <summary>
    /// Update player ranks based on quota for all players (online/offline)
    /// </summary>
    private static void UpdateQuotaRanksForRace(Race race, AbyssRankEnum minRank)
    {
        List<RankingListPlayerGp> rankingListSorted = AbyssRankDAO.LoadRankingListPlayersGp(race);
        if (rankingListSorted == null)
            return;

        // calculate and set new GP ranks
        int usedQuota = 0;
        for (int i = AbyssRankEnum.SUPREME_COMMANDER.GetId(); i >= minRank.GetId(); i--)
            usedQuota = SelectAndUpdateQuotaRank(AbyssRankEnum.GetRankById(i), rankingListSorted, usedQuota);

        // set all players with an old GP rank to AP ranks if they hold no rank position anymore
        Dictionary<int, int> apByPlayerId = AbyssRankDAO.LoadApOfPlayersNotInRankingList(race, minRank);
        if (apByPlayerId != null)
            UpdateToNoQuotaRank(apByPlayerId);
    }

    private static int SelectAndUpdateQuotaRank(AbyssRankEnum rank, List<RankingListPlayerGp> rankingList, int usedQuota)
    {
        for (int idx = 0; idx < rankingList.Count;)
        {
            RankingListPlayerGp rankingListEntry = rankingList[idx];
            if (usedQuota >= rank.GetQuota() || rankingListEntry.Gp() < rank.GetRequiredGP())
                break;
            // remove player and update its rank
            rankingList.RemoveAt(idx);
            UpdateRankTo(rank, rankingListEntry.PlayerId(), rankingListEntry.Position());
            usedQuota++;
        }
        return usedQuota;
    }

    private static void UpdateToNoQuotaRank(Dictionary<int, int> apByPlayerId)
    {
        foreach (KeyValuePair<int, int> entry in apByPlayerId)
        {
            AbyssRankEnum rank = AbyssRankEnum.GetRankForPoints(entry.Value, 0); // no GP -> no officer ranks
            UpdateRankTo(rank, entry.Key, 0);
        }
    }

    private static void UpdateRankTo(AbyssRankEnum newRank, int playerId, int rankingPosition)
    {
        // check if rank has changed for online players
        Player player = World.GetInstance().GetPlayer(playerId);
        if (player != null)
        {
            bool rankChanged = player.GetAbyssRank().GetRank() != newRank;
            if (rankChanged)
            {
                player.GetAbyssRank().SetRank(newRank);
                // save to db now, so cache reload loads the correct rank for online players
                AbyssRankDAO.UpdateAbyssRank(playerId, newRank);
            }
            AbyssPointsService.OnRankChanged(player, false, rankChanged, rankingPosition);
        }
        else
        {
            AbyssRankDAO.UpdateAbyssRank(playerId, newRank);
        }
    }

    private static void UpdateDailyGpLoss()
    {
        foreach (AbyssRankEnum rank in AbyssRankEnum.Values())
        {
            if (rank.GetGpLossPerDay() > 0)
                AbyssRankDAO.DailyUpdateGp(rank);
        }
        foreach (Player p in World.GetInstance().GetAllPlayers())
        {
            if (p.GetAbyssRank().GetRank().GetGpLossPerDay() > 0)
            {
                GloryPointsService.AddGp(p.GetObjectId(), -p.GetAbyssRank().GetRank().GetGpLossPerDay());
            }
        }
    }
}
