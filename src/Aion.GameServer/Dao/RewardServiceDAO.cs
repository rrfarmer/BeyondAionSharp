using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/RewardServiceDAO (@author KID, Neon). JDBC DAO over player_web_rewards (out-of-game web reward delivery).
/// DatabaseFactory-style; loadUnreceived: executeQuery -> ExecuteReader + RewardEntryItem(entryId,itemId,count). storeReceived: Java
/// setAutoCommit(false)+addBatch/executeBatch+commit -> explicit MySqlTransaction + MySqlBatch (BatchCommands) + Commit (closest
/// ADO.NET analog of JDBC batching). new Timestamp(long millis) -> DateTimeOffset.FromUnixTimeMilliseconds(...).LocalDateTime
/// (Java Timestamp(long) is epoch-millis rendered in local tz). Arrays.toString(ids) -> "[" + string.Join(", ", ids) + "]". SQL verbatim.
/// </summary>
public class RewardServiceDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(RewardServiceDAO));

    private const string UPDATE_QUERY = "UPDATE `player_web_rewards` SET `received`=? WHERE `entry_id`=?";
    private const string SELECT_QUERY = "SELECT entry_id, item_id, item_count FROM `player_web_rewards` WHERE `player_id`=? AND `received` IS NULL";

    public static List<RewardEntryItem> LoadUnreceived(int playerId)
    {
        List<RewardEntryItem> list = new List<RewardEntryItem>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int entryId = rset.GetInt32(rset.GetOrdinal("entry_id"));
                int itemId = rset.GetInt32(rset.GetOrdinal("item_id"));
                long count = rset.GetInt64(rset.GetOrdinal("item_count"));
                list.Add(new RewardEntryItem(entryId, itemId, count));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't load unreceived web rewards for player " + playerId);
        }
        return list;
    }

    public static void StoreReceived(List<int> ids, long timeReceived)
    {
        DateTime time = DateTimeOffset.FromUnixTimeMilliseconds(timeReceived).LocalDateTime;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlTransaction transaction = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, transaction);
            foreach (int entryId in ids)
            {
                MySqlBatchCommand cmd = new MySqlBatchCommand(UPDATE_QUERY);
                cmd.Parameters.Add(new MySqlParameter { Value = time });
                cmd.Parameters.Add(new MySqlParameter { Value = entryId });
                batch.BatchCommands.Add(cmd);
            }
            batch.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error saving received web rewards, player could potentially receive rewards multiple times! Check entry_id's: "
                + "[" + string.Join(", ", ids) + "]");
        }
    }
}
