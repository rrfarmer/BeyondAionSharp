using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.Templates.Survey;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/SurveyControllerDAO (@author KID). JDBC DAO over the surveys table (unused web-survey reward items).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt/getLong/getString("col")
/// -> reader.GetX(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL (incl. used_time=NOW()) verbatim. SurveyItem public fields
/// keep Java camelCase. used flag 0/1 passed as int, matching Java setInt(1, 0)/setInt(1, 1).
/// </summary>
public class SurveyControllerDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(SurveyControllerDAO));

    public const string UPDATE_QUERY = "UPDATE `surveys` SET `used`=?, used_time=NOW() WHERE `unique_id`=?";
    public const string SELECT_QUERY = "SELECT * FROM `surveys` WHERE `used`=?";

    public static List<SurveyItem> GetAllUnused()
    {
        List<SurveyItem> list = new List<SurveyItem>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = 0 });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                SurveyItem item = new SurveyItem();
                item.uniqueId = rset.GetInt32(rset.GetOrdinal("unique_id"));
                item.ownerId = rset.GetInt32(rset.GetOrdinal("owner_id"));
                item.itemId = rset.GetInt32(rset.GetOrdinal("item_id"));
                item.count = rset.GetInt64(rset.GetOrdinal("item_count"));
                item.html = rset.GetString(rset.GetOrdinal("html_text"));
                item.radio = rset.GetString(rset.GetOrdinal("html_radio"));
                list.Add(item);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load new surveys");
        }
        return list;
    }

    public static bool UseItem(int id)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = 1 });
            stmt.Parameters.Add(new MySqlParameter { Value = id });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not set used state for survey " + id);
            return false;
        }
        return true;
    }
}
