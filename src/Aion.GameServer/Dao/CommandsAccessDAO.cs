using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/CommandsAccessDAO (@author ViAl). JDBC DAO over commands_access (per-player granted admin command names).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; executeUpdate -> ExecuteNonQuery;
/// SQL verbatim. Map&lt;Integer,Set&lt;String&gt;&gt; -> Dictionary&lt;int,HashSet&lt;string&gt;&gt;; the loadAccesses Map.compute(key, lambda)
/// (create-set-if-null, add, store back) -> TryGetValue + new HashSet fallback + Add + write-back, preserving the same semantics.
/// </summary>
public class CommandsAccessDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CommandsAccessDAO));

    private const string LOAD_QUERY = "SELECT * FROM commands_access";
    private const string INSERT_QUERY = "INSERT INTO commands_access(player_id, command) VALUES (?,?)";
    private const string DELETE_QUERY = "DELETE FROM commands_access WHERE player_id = ? AND command = ?";
    private const string DELETE_ALL_QUERY = "DELETE FROM commands_access WHERE player_id = ?";

    public static Dictionary<int, HashSet<string>> LoadAccesses()
    {
        Dictionary<int, HashSet<string>> accesses = new Dictionary<int, HashSet<string>>();
        try
        {
            using MySqlConnection conn = DatabaseFactory.GetConnection();
            conn.Open();
            using MySqlCommand stmt = conn.CreateCommand();
            stmt.CommandText = LOAD_QUERY;
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int playerId = rset.GetInt32(rset.GetOrdinal("player_id"));
                string command = rset.GetString(rset.GetOrdinal("command"));
                if (!accesses.TryGetValue(playerId, out HashSet<string> commands) || commands == null)
                    commands = new HashSet<string>();
                commands.Add(command);
                accesses[playerId] = commands;
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while loading commands accesses.");
        }
        return accesses;
    }

    public static void AddAccess(int playerId, string commandName)
    {
        try
        {
            using MySqlConnection conn = DatabaseFactory.GetConnection();
            conn.Open();
            using MySqlCommand stmt = conn.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = commandName });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while adding access on command " + commandName + " to player " + playerId);
        }
    }

    public static void RemoveAccess(int playerId, string commandName)
    {
        try
        {
            using MySqlConnection conn = DatabaseFactory.GetConnection();
            conn.Open();
            using MySqlCommand stmt = conn.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = commandName });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while removing access on command " + commandName + " from player " + playerId);
        }
    }

    public static void RemoveAllAccesses(int playerId)
    {
        try
        {
            using MySqlConnection conn = DatabaseFactory.GetConnection();
            conn.Open();
            using MySqlCommand stmt = conn.CreateCommand();
            stmt.CommandText = DELETE_ALL_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while removing all accesses from player " + playerId);
        }
    }
}
