using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/AbyssRankDAO (@author ATracer, Divinity, nrg). JDBC DAO over abyss_rank + ranking lists. DatabaseFactory-style.
/// AbyssRankEnum.GetId/GetGpLossPerDay are extension methods (Utils.Stats); Race/PlayerClass/Gender valueOf->Enum.Parse, toString()->ToString();
/// System.currentTimeMillis()->DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); storeAbyssRank switch NEW/UPDATE_REQUIRED->IPersistable.PersistentState.
/// updateRankingLists mixes literal addBatch("SET @a=0;") with parameterized addBatch() on one PreparedStatement -> MySqlBatch with literal
/// MySqlBatchCommands + parameterized MySqlBatchCommands (autocommit, no explicit transaction, as Java). Table-qualified column labels
/// (a.rank_pos/p.id/l.name) preserved verbatim. Nested Java records -> C# positional records (Java lowercase component names kept).
/// DataManager.PLAYER_EXPERIENCE_TABLE red-tolerated. SQL verbatim.
/// </summary>
public class AbyssRankDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AbyssRankDAO));

    private const string SELECT_QUERY = "SELECT daily_ap, weekly_ap, ap, daily_gp, weekly_gp, gp, `rank`, daily_kill, weekly_kill, all_kill, max_rank, last_kill, last_ap, last_gp, last_update FROM abyss_rank WHERE player_id = ?";
    private const string INSERT_QUERY = "INSERT INTO abyss_rank (player_id, daily_ap, weekly_ap, ap, `rank`, daily_kill, weekly_kill, all_kill, max_rank, last_kill, last_ap, last_update, daily_gp, weekly_gp, gp, last_gp) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
    private const string UPDATE_QUERY = "UPDATE abyss_rank SET  daily_ap = ?, weekly_ap = ?, ap = ?, `rank` = ?, daily_kill = ?, weekly_kill = ?, all_kill = ?, max_rank = ?, last_kill = ?, last_ap = ?, last_update = ?, daily_gp = ?, weekly_gp = ?, gp = ?, last_gp = ? WHERE player_id = ?";
    private const string DECREASE_GP_DAILY = "UPDATE abyss_rank SET gp = GREATEST(gp - ?, 0) WHERE `rank` = ?";
    private const string INCREASE_GP_QUERY = "UPDATE abyss_rank SET gp = GREATEST(gp + ?, 0) WHERE player_id = ?";
    private const string INCREASE_GP_QUERY_WITH_STATS = "UPDATE abyss_rank SET gp = gp + ?, daily_gp = daily_gp + ?, weekly_gp = weekly_gp + ? WHERE player_id = ?";
    private const string UPDATE_RANK = "UPDATE abyss_rank SET `rank` = ? WHERE player_id = ?";
    private const string SELECT_RANKING_LIST_PLAYERS = "SELECT a.rank_pos, a.old_rank_pos, p.id, p.name, p.race, p.exp, a.rank, a.ap, a.gp, p.title_id, p.player_class, p.gender, l.name FROM abyss_rank a JOIN players p ON a.player_id = p.id LEFT JOIN legion_members lm ON lm.player_id = p.id LEFT JOIN legions l ON l.id = lm.legion_id WHERE a.rank_pos > 0";
    private const string SELECT_RANKING_LIST_LEGIONS = "SELECT l.rank_pos, l.old_rank_pos, l.id, l.name, p.race, l.level, l.contribution_points FROM legions l, legion_members lm, players p WHERE lm.rank = 'BRIGADE_GENERAL' AND lm.player_id = p.id AND lm.legion_id = l.id AND l.rank_pos > 0";
    private const string SELECT_RANKING_LIST_PLAYERS_GP = "SELECT a.rank_pos, a.player_id, a.gp FROM abyss_rank a, players p WHERE a.player_id = p.id AND p.race = ? AND a.rank_pos > 0 ORDER by a.rank_pos";
    private const string SELECT_UNRANKED_PLAYERS_AP = "SELECT a.player_id, a.ap FROM abyss_rank a, players p WHERE a.player_id = p.id AND p.race = ? AND a.rank_pos = 0 AND a.rank >= ?";
    private const string SELECT_LEGION_COUNT = "SELECT COUNT(player_id) as players FROM legion_members WHERE legion_id = ?";
    private const string RESET_RANKING_LIST_PLAYERS = "UPDATE abyss_rank a SET a.old_rank_pos = a.rank_pos, a.rank_pos = 0 WHERE a.rank_pos > 0";
    private const string RESET_RANKING_LIST_LEGIONS = "UPDATE legions l SET l.old_rank_pos = l.rank_pos, l.rank_pos = 0 WHERE l.rank_pos > 0";
    private const string UPDATE_RANKING_LIST_PLAYERS_POSITIONS = "UPDATE abyss_rank SET rank_pos = @a:=@a+1 WHERE gp > 0 AND player_id IN (SELECT id FROM players WHERE race = ? AND (@minLastOnline IS NULL OR last_online >= @minLastOnline)) ORDER BY gp DESC LIMIT ?";
    private const string UPDATE_RANKING_LIST_LEGIONS_POSITIONS = "UPDATE legions SET rank_pos = @a:=@a+1 WHERE id IN (SELECT legion_id FROM legion_members lm, players WHERE `rank` = 'BRIGADE_GENERAL' AND players.id = lm.player_id and players.race = ?) ORDER BY contribution_points DESC LIMIT ?";

    public static AbyssRank LoadAbyssRank(int playerId)
    {
        AbyssRank abyssRank = null;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rs = stmt.ExecuteReader();
            if (rs.Read())
            {
                int daily_ap = rs.GetInt32(rs.GetOrdinal("daily_ap"));
                int weekly_ap = rs.GetInt32(rs.GetOrdinal("weekly_ap"));
                int ap = rs.GetInt32(rs.GetOrdinal("ap"));
                int rank = rs.GetInt32(rs.GetOrdinal("rank"));
                int daily_kill = rs.GetInt32(rs.GetOrdinal("daily_kill"));
                int weekly_kill = rs.GetInt32(rs.GetOrdinal("weekly_kill"));
                int all_kill = rs.GetInt32(rs.GetOrdinal("all_kill"));
                int max_rank = rs.GetInt32(rs.GetOrdinal("max_rank"));
                int last_kill = rs.GetInt32(rs.GetOrdinal("last_kill"));
                int last_ap = rs.GetInt32(rs.GetOrdinal("last_ap"));
                long last_update = rs.GetInt64(rs.GetOrdinal("last_update"));
                int daily_gp = rs.GetInt32(rs.GetOrdinal("daily_gp"));
                int weekly_gp = rs.GetInt32(rs.GetOrdinal("weekly_gp"));
                int gp = rs.GetInt32(rs.GetOrdinal("gp"));
                int last_gp = rs.GetInt32(rs.GetOrdinal("last_gp"));

                abyssRank = new AbyssRank(daily_ap, weekly_ap, ap, rank, daily_kill, weekly_kill, all_kill, max_rank, last_kill, last_ap, last_update,
                    daily_gp, weekly_gp, gp, last_gp);
                abyssRank.SetPersistentState(IPersistable.PersistentState.UPDATED);
            }
            else
            {
                abyssRank = new AbyssRank(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0, 0, 0, 0);
                abyssRank.SetPersistentState(IPersistable.PersistentState.NEW);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't load abyss rank for player " + playerId);
        }
        return abyssRank;
    }

    public static void LoadAbyssRank(Player player)
    {
        AbyssRank rank = LoadAbyssRank(player.GetObjectId());
        player.SetAbyssRank(rank);
    }

    public static bool StoreAbyssRank(Player player)
    {
        AbyssRank rank = player.GetAbyssRank();
        bool result = false;
        switch (rank.GetPersistentState())
        {
            case IPersistable.PersistentState.NEW:
                result = InsertRank(player.GetObjectId(), rank);
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                result = UpdateRank(player.GetObjectId(), rank);
                break;
        }
        rank.SetPersistentState(IPersistable.PersistentState.UPDATED);
        return result;
    }

    private static bool InsertRank(int playerId, AbyssRank rank)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetDailyAP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetWeeklyAP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetAp() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetRank().GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetDailyKill() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetWeeklyKill() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetAllKill() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetMaxRank() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetLastKill() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetLastAP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetLastUpdate() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetDailyGP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetWeeklyGP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetCurrentGP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetLastGP() });
            stmt.ExecuteNonQuery();
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't insert abyss rank for player " + playerId);
            return false;
        }
    }

    private static bool UpdateRank(int playerId, AbyssRank rank)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetDailyAP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetWeeklyAP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetAp() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetRank().GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetDailyKill() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetWeeklyKill() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetAllKill() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetMaxRank() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetLastKill() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetLastAP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetLastUpdate() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetDailyGP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetWeeklyGP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetCurrentGP() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetLastGP() });
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.ExecuteNonQuery();
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't update abyss rank of player " + playerId);
            return false;
        }
    }

    public static void DailyUpdateGp(AbyssRankEnum rank)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DECREASE_GP_DAILY;
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetGpLossPerDay() });
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't decrease daily GP for rank " + rank);
        }
    }

    public static void AddGp(int playerObjId, int additionalGp, bool modifyStats)
    {
        string updateQuery = modifyStats ? INCREASE_GP_QUERY_WITH_STATS : INCREASE_GP_QUERY;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = updateQuery;
            if (modifyStats)
            {
                stmt.Parameters.Add(new MySqlParameter { Value = additionalGp });
                stmt.Parameters.Add(new MySqlParameter { Value = additionalGp });
                stmt.Parameters.Add(new MySqlParameter { Value = additionalGp });
                stmt.Parameters.Add(new MySqlParameter { Value = playerObjId });
            }
            else
            {
                stmt.Parameters.Add(new MySqlParameter { Value = additionalGp });
                stmt.Parameters.Add(new MySqlParameter { Value = playerObjId });
            }
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't increase {AdditionalGp} GP for player {PlayerObjId}", additionalGp, playerObjId);
        }
    }

    public static List<RankingListPlayer> LoadRankingListPlayers()
    {
        List<RankingListPlayer> results = new List<RankingListPlayer>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_RANKING_LIST_PLAYERS;
            using MySqlDataReader rs = stmt.ExecuteReader();
            while (rs.Read())
            {
                int position = rs.GetInt32(rs.GetOrdinal("a.rank_pos"));
                int oldPosition = rs.GetInt32(rs.GetOrdinal("a.old_rank_pos"));
                int id = rs.GetInt32(rs.GetOrdinal("p.id"));
                string name = rs.GetString(rs.GetOrdinal("p.name"));
                Race race = Enum.Parse<Race>(rs.GetString(rs.GetOrdinal("p.race")));
                int level = DataManager.PLAYER_EXPERIENCE_TABLE.GetLevelForExp(rs.GetInt64(rs.GetOrdinal("p.exp")));
                int rank = rs.GetInt32(rs.GetOrdinal("a.rank"));
                int ap = rs.GetInt32(rs.GetOrdinal("a.ap"));
                int gp = rs.GetInt32(rs.GetOrdinal("a.gp"));
                int title = rs.GetInt32(rs.GetOrdinal("p.title_id"));
                PlayerClass playerClass = Enum.Parse<PlayerClass>(rs.GetString(rs.GetOrdinal("p.player_class")));
                Gender gender = Enum.Parse<Gender>(rs.GetString(rs.GetOrdinal("p.gender")));
                string legionName = rs.GetString(rs.GetOrdinal("l.name"));
                results.Add(new RankingListPlayer(position, oldPosition, id, name, race, level, rank, ap, gp, title, playerClass, gender, legionName));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, null);
        }
        return results;
    }

    public static List<RankingListLegion> LoadRankingListLegions()
    {
        List<RankingListLegion> results = new List<RankingListLegion>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_RANKING_LIST_LEGIONS;
            using MySqlDataReader rs = stmt.ExecuteReader();
            while (rs.Read())
            {
                int position = rs.GetInt32(rs.GetOrdinal("l.rank_pos"));
                int oldPosition = rs.GetInt32(rs.GetOrdinal("l.old_rank_pos"));
                int id = rs.GetInt32(rs.GetOrdinal("l.id"));
                string name = rs.GetString(rs.GetOrdinal("l.name"));
                Race race = Enum.Parse<Race>(rs.GetString(rs.GetOrdinal("p.race")));
                int level = rs.GetInt32(rs.GetOrdinal("l.level"));
                long contributionPoints = rs.GetInt64(rs.GetOrdinal("l.contribution_points"));
                int memberCount = LoadLegionMemberCount(con, id);
                results.Add(new RankingListLegion(position, oldPosition, id, name, race, level, contributionPoints, memberCount));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, null);
        }
        return results;
    }

    private static int LoadLegionMemberCount(MySqlConnection con, int legionId)
    {
        try
        {
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_LEGION_COUNT;
            stmt.Parameters.Add(new MySqlParameter { Value = legionId });
            using MySqlDataReader rs = stmt.ExecuteReader();
            rs.Read();
            return rs.GetInt32(rs.GetOrdinal("players"));
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't load legion member count for legion " + legionId);
            return 0;
        }
    }

    public static List<RankingListPlayerGp> LoadRankingListPlayersGp(Race race)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_RANKING_LIST_PLAYERS_GP;
            stmt.Parameters.Add(new MySqlParameter { Value = race.ToString() });
            using MySqlDataReader rs = stmt.ExecuteReader();
            List<RankingListPlayerGp> rankingList = new List<RankingListPlayerGp>();
            while (rs.Read())
            {
                int rankPos = rs.GetInt32(rs.GetOrdinal("rank_pos"));
                int playerId = rs.GetInt32(rs.GetOrdinal("player_id"));
                int gp = rs.GetInt32(rs.GetOrdinal("gp"));
                rankingList.Add(new RankingListPlayerGp(rankPos, playerId, gp));
            }
            return rankingList;
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't load top ranks for race " + race);
            return null;
        }
    }

    public static Dictionary<int, int> LoadApOfPlayersNotInRankingList(Race race, AbyssRankEnum minRank)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_UNRANKED_PLAYERS_AP;
            stmt.Parameters.Add(new MySqlParameter { Value = race.ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = minRank.GetId() });
            using MySqlDataReader rs = stmt.ExecuteReader();
            Dictionary<int, int> apByPlayerId = new Dictionary<int, int>();
            while (rs.Read())
                apByPlayerId[rs.GetInt32(rs.GetOrdinal("player_id"))] = rs.GetInt32(rs.GetOrdinal("ap"));
            return apByPlayerId;
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't load ranks for race " + race + " (minRank " + minRank + ")");
            return null;
        }
    }

    public static void UpdateAbyssRank(int playerId, AbyssRankEnum rank)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_RANK;
            stmt.Parameters.Add(new MySqlParameter { Value = rank.GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't update abyss rank of player " + playerId + " to " + rank);
        }
    }

    public static void UpdateRankingLists(int maxOfflineDays, int playerLimit, int legionLimit)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using (MySqlCommand stmt = con.CreateCommand())
            {
                stmt.CommandText = RESET_RANKING_LIST_PLAYERS;
                stmt.ExecuteNonQuery();
            }
            using (MySqlBatch batch = new MySqlBatch(con))
            {
                if (maxOfflineDays > 0)
                    batch.BatchCommands.Add(new MySqlBatchCommand("SET @minLastOnline = CURDATE() - INTERVAL " + maxOfflineDays + " DAY;"));
                batch.BatchCommands.Add(new MySqlBatchCommand("SET @a = 0;"));
                MySqlBatchCommand elyos = new MySqlBatchCommand(UPDATE_RANKING_LIST_PLAYERS_POSITIONS);
                elyos.Parameters.Add(new MySqlParameter { Value = "ELYOS" });
                elyos.Parameters.Add(new MySqlParameter { Value = playerLimit });
                batch.BatchCommands.Add(elyos);
                batch.BatchCommands.Add(new MySqlBatchCommand("SET @a = 0;"));
                MySqlBatchCommand asmo = new MySqlBatchCommand(UPDATE_RANKING_LIST_PLAYERS_POSITIONS);
                asmo.Parameters.Add(new MySqlParameter { Value = "ASMODIANS" });
                asmo.Parameters.Add(new MySqlParameter { Value = playerLimit });
                batch.BatchCommands.Add(asmo);
                if (maxOfflineDays > 0)
                    batch.BatchCommands.Add(new MySqlBatchCommand("SET @minLastOnline = NULL;"));
                batch.ExecuteNonQuery();
            }
            using (MySqlCommand stmt = con.CreateCommand())
            {
                stmt.CommandText = RESET_RANKING_LIST_LEGIONS;
                stmt.ExecuteNonQuery();
            }
            using (MySqlBatch batch = new MySqlBatch(con))
            {
                batch.BatchCommands.Add(new MySqlBatchCommand("SET @a = 0;"));
                MySqlBatchCommand elyos = new MySqlBatchCommand(UPDATE_RANKING_LIST_LEGIONS_POSITIONS);
                elyos.Parameters.Add(new MySqlParameter { Value = "ELYOS" });
                elyos.Parameters.Add(new MySqlParameter { Value = legionLimit });
                batch.BatchCommands.Add(elyos);
                batch.BatchCommands.Add(new MySqlBatchCommand("SET @a = 0;"));
                MySqlBatchCommand asmo = new MySqlBatchCommand(UPDATE_RANKING_LIST_LEGIONS_POSITIONS);
                asmo.Parameters.Add(new MySqlParameter { Value = "ASMODIANS" });
                asmo.Parameters.Add(new MySqlParameter { Value = legionLimit });
                batch.BatchCommands.Add(asmo);
                batch.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            log.LogError(e, null);
        }
    }

    public record RankingListPlayerGp(int position, int playerId, int gp);

    public record RankingListPlayer(int position, int oldPosition, int id, string name, Race race, int level, int abyssRank, int ap, int gp, int title,
        PlayerClass playerClass, Gender gender, string legionName);

    public record RankingListLegion(int position, int oldPosition, int id, string name, Race race, int level, long contributionPoints,
        int memberCount);
}
