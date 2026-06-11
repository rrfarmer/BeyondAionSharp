using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/ServerVariablesDAO (@author Ben, Neon). JDBC DAO over server_variables (key/value persisted server state).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; ps.executeUpdate() row count ->
/// ExecuteNonQuery(); SQL verbatim. Boxed return types preserved: Integer loadInt -> int?, Long loadLong -> long? (null when absent);
/// Integer.parseInt/Long.parseLong -> int.Parse/long.Parse. delete() is an INSTANCE method in Java (the others are static) - preserved.
/// </summary>
public class ServerVariablesDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ServerVariablesDAO));

    public static int? LoadInt(string var)
    {
        string value = Load(var);
        return value == null ? (int?)null : int.Parse(value);
    }

    public static long? LoadLong(string var)
    {
        string value = Load(var);
        return value == null ? (long?)null : long.Parse(value);
    }

    public static bool Store(string var, object value)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand ps = con.CreateCommand();
            ps.CommandText = "REPLACE INTO `server_variables` (`key`,`value`) VALUES (?,?)";
            ps.Parameters.Add(new MySqlParameter { Value = var });
            ps.Parameters.Add(new MySqlParameter { Value = value.ToString() });
            return ps.ExecuteNonQuery() > 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "Error storing " + value + " for variable " + var);
            return false;
        }
    }

    public bool Delete(string var)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand ps = con.CreateCommand();
            ps.CommandText = "DELETE FROM `server_variables` WHERE `key`=?";
            ps.Parameters.Add(new MySqlParameter { Value = var });
            return ps.ExecuteNonQuery() > 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "Error loading value for " + var);
            return false;
        }
    }

    private static string Load(string var)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand ps = con.CreateCommand();
            ps.CommandText = "SELECT `value` FROM `server_variables` WHERE `key`=?";
            ps.Parameters.Add(new MySqlParameter { Value = var });
            using MySqlDataReader rs = ps.ExecuteReader();
            if (rs.Read())
                return rs.GetString(rs.GetOrdinal("value"));
        }
        catch (Exception e)
        {
            log.LogError(e, "Error loading value for " + var);
        }
        return null;
    }
}
