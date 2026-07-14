using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.House;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/HouseBidsDAO (@author Rolandas). JDBC DAO over house_bids (house-auction bids + bidder existence check).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; execute/executeUpdate ->
/// ExecuteNonQuery; SQL (incl. the IF(...) playerExists join + ORDER BY) verbatim. Map&lt;Integer,HouseBids&gt; -> Dictionary; Map.get
/// -> GetValueOrDefault (null when absent); Map.put -> indexer; Set&lt;Integer&gt; -> HashSet&lt;int&gt;. Timestamp&lt;-&gt;millis:
/// getTimestamp(col).getTime() -> SQL epoch milliseconds; new Timestamp(millis) -> FROM_UNIXTIME(epoch).
/// Java HouseBids.bid(...) -> C# DoBid(...); nested HouseBids.Bid kept.
/// deleteOrDisableBids reuses one connection across the delete loop + disable, as Java.
/// </summary>
public class HouseBidsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(HouseBidsDAO));

    public const string LOAD_QUERY = "SELECT b.*, IF(b.player_id = p.id, 1, 0) playerExists, CAST(FLOOR(UNIX_TIMESTAMP(b.bid_time) * 1000) AS SIGNED) AS bid_time_epoch_millis FROM `house_bids` b LEFT JOIN `players` p ON p.id = b.player_id ORDER BY `bid`, `bid_time`";
    public const string INSERT_QUERY = "INSERT INTO `house_bids` (`player_id`, `house_id`, `bid`, `bid_time`) VALUES (?, ?, ?, FROM_UNIXTIME(? / 1000.0))";
    public const string DELETE_QUERY = "DELETE FROM `house_bids` WHERE `house_id` = ?";
    public const string DELETE_SINGLE_BID_QUERY = "DELETE FROM `house_bids` WHERE `player_id` = ? AND `house_id` = ? AND `bid` = ?";
    public const string DISABLE_QUERY = "UPDATE `house_bids` SET `player_id` = 0 WHERE `player_id` = ?";

    public static HashSet<int> LoadBids(IDictionary<int, HouseBids> bidsById)
    {
        HashSet<int> deletedPlayerIds = new HashSet<int>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = LOAD_QUERY;
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int playerId = rset.GetInt32(rset.GetOrdinal("player_id"));
                int houseId = rset.GetInt32(rset.GetOrdinal("house_id"));
                long bidOffer = rset.GetInt64(rset.GetOrdinal("bid"));
                long time = rset.GetInt64(rset.GetOrdinal("bid_time_epoch_millis"));
                HouseBids houseBids = bidsById.TryGetValue(houseId, out var existingBids) ? existingBids : null;
                if (houseBids == null)
                    bidsById[houseId] = new HouseBids(houseId, bidOffer, time);
                else
                {
                    houseBids.DoBid(playerId, bidOffer, time);
                    if (playerId != 0 && !rset.GetBoolean(rset.GetOrdinal("playerExists")))
                        deletedPlayerIds.Add(playerId);
                }
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Cannot read house bids");
        }
        return deletedPlayerIds;
    }

    public static bool AddBid(HouseBids.Bid bid)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = bid.GetPlayerObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = bid.GetHouseObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = bid.GetKinah() });
            stmt.Parameters.Add(new MySqlParameter { Value = bid.GetTime() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Cannot insert house bid");
            return false;
        }
        return true;
    }

    public static bool DeleteOrDisableBids(int playerObjectId, List<HouseBids.Bid> bidsToDelete)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            if (bidsToDelete.Count != 0)
            {
                using MySqlCommand delStmt = con.CreateCommand();
                delStmt.CommandText = DELETE_SINGLE_BID_QUERY;
                foreach (HouseBids.Bid bid in bidsToDelete)
                {
                    delStmt.Parameters.Clear();
                    delStmt.Parameters.Add(new MySqlParameter { Value = bid.GetPlayerObjectId() });
                    delStmt.Parameters.Add(new MySqlParameter { Value = bid.GetHouseObjectId() });
                    delStmt.Parameters.Add(new MySqlParameter { Value = bid.GetKinah() });
                    delStmt.ExecuteNonQuery();
                }
            }
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DISABLE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjectId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Cannot delete or disable house bids for player " + playerObjectId);
            return false;
        }
        return true;
    }

    public static bool DeleteHouseBids(int houseId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = houseId });
            stmt.ExecuteNonQuery();
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Cannot delete house bids");
            return false;
        }
    }
}
