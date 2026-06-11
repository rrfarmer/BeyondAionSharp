using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/AccountPassportsDAO (@author ViAl, Luzien, SVDNESS). JDBC DAO over account_passports + account_stamps (attendance
/// passport rewards). DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; executeUpdate ->
/// ExecuteNonQuery; SQL (incl. ON DUPLICATE KEY UPDATE GREATEST(...) / GREATEST) verbatim. rewarded int!=0 &lt;-&gt; isRewarded?1:0.
/// getTimestamp(null) on last_stamp -> IsDBNull?null:new DateTimeOffset(GetDateTime) (Account.GetLastStamp is DateTimeOffset?);
/// setTimestamp(null) -> DBNull.Value. normTs truncates to whole seconds: Timestamp.from(toInstant().truncatedTo(SECONDS)) -> drop
/// sub-second Ticks. NOTE the reworked Passport ctor/GetArriveDate use non-nullable DateTime (Java Timestamp was nullable), so the
/// Java null-guard in normTs is moot here - arrive_date is read non-null. storePassportList switch-arrow NEW/UPDATE_REQUIRED/DELETED
/// -> C# switch on IPersistable.PersistentState; UPDATED set after.
/// </summary>
public class AccountPassportsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AccountPassportsDAO));
    private const string SELECT_QUERY = "SELECT `passport_id`, `rewarded`, `arrive_date` FROM `account_passports` WHERE `account_id`=?";
    private const string UPDATE_QUERY = "UPDATE `account_passports` SET `rewarded`=? WHERE `account_id`=? AND `passport_id`=? AND `arrive_date`=?";
    private const string RESET_LAST_STAMPS_QUERY = "UPDATE `account_stamps` SET `last_stamp`=NULL";
    private const string RESET_STAMPS_QUERY = "UPDATE `account_stamps` SET `stamps`=0";
    private const string INSERT_QUERY = "INSERT INTO `account_passports` (`account_id`, `passport_id`, `rewarded`, `arrive_date`) VALUES (?,?,?,?) ON DUPLICATE KEY UPDATE `rewarded` = GREATEST(`rewarded`, VALUES(`rewarded`))";
    private const string DELETE_QUERY = "DELETE FROM `account_passports` WHERE account_id = ? AND passport_id = ? and arrive_date = ?";
    private const string INSERT_STAMPS_QUERY = "INSERT INTO `account_stamps` (`account_id`, `stamps`, `last_stamp`) VALUES (?,?,?)";
    private const string UPDATE_STAMPS_QUERY = "UPDATE `account_stamps` SET `stamps`= ?, `last_stamp`  = ? WHERE `account_id` = ?";
    private const string SELECT_STAMPS_QUERY = "SELECT `stamps`, `last_stamp` FROM `account_stamps` WHERE `account_id`=?";

    public static void LoadPassport(Account account)
    {
        PassportsList passportList = new PassportsList();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using (MySqlCommand stmt = con.CreateCommand())
            {
                stmt.CommandText = SELECT_QUERY;
                stmt.Parameters.Add(new MySqlParameter { Value = account.GetId() });
                using MySqlDataReader rset = stmt.ExecuteReader();
                while (rset.Read())
                {
                    int passport_id = rset.GetInt32(rset.GetOrdinal("passport_id"));
                    bool rewarded = rset.GetInt32(rset.GetOrdinal("rewarded")) != 0;
                    DateTime arriveDate = NormTs(rset.GetDateTime(rset.GetOrdinal("arrive_date")));
                    Passport pp = new Passport(passport_id, rewarded, arriveDate);
                    pp.SetPersistentState(IPersistable.PersistentState.UPDATED);
                    passportList.AddPassport(pp);
                }
                account.SetPassportsList(passportList);
            }
            using (MySqlCommand stmt = con.CreateCommand())
            {
                stmt.CommandText = SELECT_STAMPS_QUERY;
                stmt.Parameters.Add(new MySqlParameter { Value = account.GetId() });
                using MySqlDataReader rset = stmt.ExecuteReader();
                int stamps = 0;
                DateTimeOffset? lastStamp = null;
                if (rset.Read())
                {
                    stamps = rset.GetInt32(rset.GetOrdinal("stamps"));
                    int lsOrd = rset.GetOrdinal("last_stamp");
                    lastStamp = rset.IsDBNull(lsOrd) ? (DateTimeOffset?)null : new DateTimeOffset(rset.GetDateTime(lsOrd));
                }
                else
                {
                    InsertStamps(account.GetId());
                }
                account.SetPassportStamps(stamps);
                account.SetLastStamp(lastStamp);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore completed passport data for account: {AccountId} from DB.", account.GetId());
        }
    }

    public static void StorePassportList(int accountId, List<Passport> pList)
    {
        foreach (Passport passport in pList)
        {
            switch (passport.GetPersistentState())
            {
                case IPersistable.PersistentState.NEW:
                    AddPassports(accountId, passport);
                    break;
                case IPersistable.PersistentState.UPDATE_REQUIRED:
                    UpdatePassport(accountId, passport);
                    break;
                case IPersistable.PersistentState.DELETED:
                    DeletePassport(accountId, passport);
                    break;
            }
            passport.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
    }

    public static void StorePassport(Account account)
    {
        StorePassportList(account.GetId(), account.GetPassportsList().GetAllPassports());
        UpdateStamps(account);
    }

    private static void AddPassports(int accountId, Passport passport)
    {
        try
        {
            using MySqlConnection conn = DatabaseFactory.GetConnection();
            conn.Open();
            using MySqlCommand ps = conn.CreateCommand();
            ps.CommandText = INSERT_QUERY;
            ps.Parameters.Add(new MySqlParameter { Value = accountId });
            ps.Parameters.Add(new MySqlParameter { Value = passport.GetId() });
            ps.Parameters.Add(new MySqlParameter { Value = passport.IsRewarded() ? 1 : 0 });
            ps.Parameters.Add(new MySqlParameter { Value = passport.GetArriveDate() });
            ps.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while adding passports for account {AccountId}.", accountId);
        }
    }

    private static void UpdatePassport(int accountId, Passport passport)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand ps = con.CreateCommand();
            ps.CommandText = UPDATE_QUERY;
            ps.Parameters.Add(new MySqlParameter { Value = passport.IsRewarded() ? 1 : 0 });
            ps.Parameters.Add(new MySqlParameter { Value = accountId });
            ps.Parameters.Add(new MySqlParameter { Value = passport.GetId() });
            ps.Parameters.Add(new MySqlParameter { Value = passport.GetArriveDate() });
            ps.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to update existing passports for account {AccountId}.", accountId);
        }
    }

    private static void DeletePassport(int accountId, Passport passport)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand ps = con.CreateCommand();
            ps.CommandText = DELETE_QUERY;
            ps.Parameters.Add(new MySqlParameter { Value = accountId });
            ps.Parameters.Add(new MySqlParameter { Value = passport.GetId() });
            ps.Parameters.Add(new MySqlParameter { Value = passport.GetArriveDate() });
            ps.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to delete passports for account {AccountId}.", accountId);
        }
    }

    private static void InsertStamps(int accountId)
    {
        try
        {
            using MySqlConnection conn = DatabaseFactory.GetConnection();
            conn.Open();
            using MySqlCommand ps = conn.CreateCommand();
            ps.CommandText = INSERT_STAMPS_QUERY;
            ps.Parameters.Add(new MySqlParameter { Value = accountId });
            ps.Parameters.Add(new MySqlParameter { Value = 0 });
            ps.Parameters.Add(new MySqlParameter { Value = DBNull.Value });
            ps.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while adding stamps for account {AccountId}.", accountId);
        }
    }

    private static void UpdateStamps(Account account)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand ps = con.CreateCommand();
            ps.CommandText = UPDATE_STAMPS_QUERY;
            ps.Parameters.Add(new MySqlParameter { Value = account.GetPassportStamps() });
            ps.Parameters.Add(new MySqlParameter { Value = (object)account.GetLastStamp() ?? DBNull.Value });
            ps.Parameters.Add(new MySqlParameter { Value = account.GetId() });
            ps.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to update existing passports for account {AccountId}.", account.GetId());
        }
    }

    public static void ResetAllLastStamps()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand ps = con.CreateCommand();
            ps.CommandText = RESET_LAST_STAMPS_QUERY;
            ps.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to reset all last stamps.");
        }
    }

    public static void ResetAllStamps()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand ps = con.CreateCommand();
            ps.CommandText = RESET_STAMPS_QUERY;
            ps.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to reset all stamps.");
        }
    }

    private static DateTime NormTs(DateTime ts)
    {
        return new DateTime(ts.Ticks - (ts.Ticks % TimeSpan.TicksPerSecond), ts.Kind);
    }
}
