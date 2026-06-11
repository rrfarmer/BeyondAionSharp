using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerPasskeyDAO (@author cura). JDBC DAO over player_passkey (per-account second-password gate).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; executeUpdate row-count ->
/// ExecuteNonQuery(); rset.getInt("cnt") -> reader.GetInt32(reader.GetOrdinal("cnt")); execute -> ExecuteNonQuery; SQL verbatim.
/// </summary>
public class PlayerPasskeyDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerPasskeyDAO));

    public const string INSERT_QUERY = "INSERT INTO `player_passkey` (`account_id`, `passkey`) VALUES (?,?)";
    public const string UPDATE_QUERY = "UPDATE `player_passkey` SET `passkey`=? WHERE `account_id`=? AND `passkey`=?";
    public const string UPDATE_FORCE_QUERY = "UPDATE `player_passkey` SET `passkey`=? WHERE `account_id`=?";
    public const string CHECK_QUERY = "SELECT COUNT(*) cnt FROM `player_passkey` WHERE `account_id`=? AND `passkey`=?";
    public const string EXIST_CHECK_QUERY = "SELECT COUNT(*) cnt FROM `player_passkey` WHERE `account_id`=?";

    public static void InsertPlayerPasskey(int accountId, string passkey)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            stmt.Parameters.Add(new MySqlParameter { Value = passkey });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error saving PlayerPasskey. accountId: " + accountId);
        }
    }

    public static bool UpdatePlayerPasskey(int accountId, string oldPasskey, string newPasskey)
    {
        bool result = false;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = newPasskey });
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            stmt.Parameters.Add(new MySqlParameter { Value = oldPasskey });
            if (stmt.ExecuteNonQuery() > 0)
                result = true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Error updating PlayerPasskey. accountId: " + accountId);
        }
        return result;
    }

    public static bool UpdateForcePlayerPasskey(int accountId, string newPasskey)
    {
        bool result = false;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_FORCE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = newPasskey });
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            if (stmt.ExecuteNonQuery() > 0)
                result = true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Error updaing PlayerPasskey. accountId: " + accountId);
        }
        return result;
    }

    public static bool CheckPlayerPasskey(int accountId, string passkey)
    {
        bool passkeyChecked = false;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = CHECK_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            stmt.Parameters.Add(new MySqlParameter { Value = passkey });
            using MySqlDataReader rset = stmt.ExecuteReader();
            if (rset.Read())
            {
                if (rset.GetInt32(rset.GetOrdinal("cnt")) == 1)
                    passkeyChecked = true;
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Error loading PlayerPasskey. accountId: " + accountId);
            return false;
        }
        return passkeyChecked;
    }

    public static bool ExistCheckPlayerPasskey(int accountId)
    {
        bool existPasskeyChecked = false;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = EXIST_CHECK_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            if (rset.Read())
            {
                if (rset.GetInt32(rset.GetOrdinal("cnt")) == 1)
                    existPasskeyChecked = true;
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Error loading PlayerPasskey. accountId: " + accountId);
            return false;
        }
        return existPasskeyChecked;
    }
}
