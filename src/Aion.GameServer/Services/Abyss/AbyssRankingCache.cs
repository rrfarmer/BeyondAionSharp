using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using RankingListLegion = Aion.GameServer.Dao.AbyssRankDAO.RankingListLegion;
using RankingListPlayer = Aion.GameServer.Dao.AbyssRankDAO.RankingListPlayer;

namespace Aion.GameServer.Services.Abyss;

/// <summary>Java parity: services/abyss/AbyssRankingCache (VladimirZ, Neon). Singleton cache of player/legion ranking-window packets per race. refreshCache (load DB, group by race, page player packets by 44, build maps via toMap), reloadRankings (refresh + reset per-player view + legion-edit packet), getPlayerRankListPackets (subList->GetRange paging), getPlayers/getLegions/getRankingListPosition. ConcurrentHashMap n/a; streams toMap->ToDictionary; Arrays.asList->List; subList->GetRange; currentTimeMillis->UtcNow. AbyssRankDAO records / SM_ packets red-tolerated.</summary>
public class AbyssRankingCache
{
    private Dictionary<int, RankingListPlayer> rankingListPlayers;
    private Dictionary<int, RankingListLegion> rankingListLegions;
    /// <summary>
    /// Player ranking list that will show up in the abyss ranking window
    /// </summary>
    private Dictionary<Race, List<SM_ABYSS_RANKING_PLAYERS>> playerRankListPackets;

    /// <summary>
    /// Legion ranking list that will show up in the abyss ranking window
    /// </summary>
    private Dictionary<Race, SM_ABYSS_RANKING_LEGIONS> legionRankListPackets;

    /// <summary>
    /// Last update time that will show up in the abyss ranking window
    /// </summary>
    private int lastUpdate;

    private AbyssRankingCache()
    {
        RefreshCache();
    }

    public static AbyssRankingCache GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    /// <summary>
    /// Loads ranking data from DB
    /// </summary>
    private void RefreshCache()
    {
        List<Race> races = new List<Race> { Race.ASMODIANS, Race.ELYOS };
        List<RankingListPlayer> rankingListPlayers = AbyssRankDAO.LoadRankingListPlayers();
        List<RankingListLegion> rankingListLegions = AbyssRankDAO.LoadRankingListLegions();
        Dictionary<Race, List<SM_ABYSS_RANKING_PLAYERS>> newPlayerRankListPackets = new Dictionary<Race, List<SM_ABYSS_RANKING_PLAYERS>>();
        Dictionary<Race, SM_ABYSS_RANKING_LEGIONS> newLegionRankListPackets = new Dictionary<Race, SM_ABYSS_RANKING_LEGIONS>();

        int updateTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);

        foreach (Race race in races)
        {
            List<RankingListPlayer> players = rankingListPlayers.Where(p => p.Race == race).ToList();
            newPlayerRankListPackets[race] = GetPlayerRankListPackets(updateTime, race, players);

            List<RankingListLegion> legions = rankingListLegions.Where(l => l.Race == race).ToList();
            newLegionRankListPackets[race] = new SM_ABYSS_RANKING_LEGIONS(updateTime, legions, race);
        }

        // assign the finished lists
        this.rankingListPlayers = rankingListPlayers.ToDictionary(p => p.Id, p => p);
        this.rankingListLegions = rankingListLegions.ToDictionary(l => l.Id, l => l);
        this.playerRankListPackets = newPlayerRankListPackets;
        this.legionRankListPackets = newLegionRankListPackets;
        this.lastUpdate = updateTime;
    }

    /// <summary>
    /// Reloads player &amp; legion rank data from DB and refreshes the in-game views
    /// </summary>
    public void ReloadRankings()
    {
        // update cache
        RefreshCache();

        Aion.GameServer.World.World.GetInstance().ForEachPlayer(player =>
        {
            player.ResetAbyssRankListUpdated();
            if (player.GetLegion() != null) // update legion rank number
                PacketSendUtility.SendPacket(player, new SM_LEGION_EDIT(0x01, player.GetLegion()));
        });
    }

    private List<SM_ABYSS_RANKING_PLAYERS> GetPlayerRankListPackets(int updateTime, Race race, List<RankingListPlayer> list)
    {
        List<SM_ABYSS_RANKING_PLAYERS> playerPackets = new List<SM_ABYSS_RANKING_PLAYERS>();
        int page = 1;

        for (int i = 0; i < list.Count; i += 44)
        {
            if (list.Count > i + 44)
            {
                playerPackets.Add(new SM_ABYSS_RANKING_PLAYERS(updateTime, list.GetRange(i, 44), race, page, false));
            }
            else
            {
                playerPackets.Add(new SM_ABYSS_RANKING_PLAYERS(updateTime, list.GetRange(i, list.Count - i), race, page, true));
            }
            page++;
        }

        return playerPackets;
    }

    public List<SM_ABYSS_RANKING_PLAYERS> GetPlayers(Race race)
    {
        return playerRankListPackets.GetValueOrDefault(race);
    }

    public SM_ABYSS_RANKING_LEGIONS GetLegions(Race race)
    {
        return legionRankListPackets.GetValueOrDefault(race);
    }

    public int GetRankingListPosition(Player player)
    {
        RankingListPlayer rank = rankingListPlayers.GetValueOrDefault(player.GetObjectId());
        return rank == null ? 0 : rank.Position;
    }

    public int GetRankingListPosition(Legion legion)
    {
        RankingListLegion rank = rankingListLegions.GetValueOrDefault(legion.GetLegionId());
        return rank == null ? 0 : rank.Position;
    }

    public int GetLastUpdate()
    {
        return lastUpdate;
    }

    private static class SingletonHolder
    {
        internal static readonly AbyssRankingCache INSTANCE = new AbyssRankingCache();
    }
}
