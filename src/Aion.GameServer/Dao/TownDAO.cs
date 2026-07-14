using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Town;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/TownDAO (@author ViAl). JDBC DAO over the towns table (per-race town level/points progression).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt("col") ->
/// reader.GetInt32(reader.GetOrdinal("col")); executeUpdate -> ExecuteNonQuery; SQL verbatim. Race.toString() -> Race.ToString();
/// level_up_date uses a nullable SQL epoch projection and FROM_UNIXTIME write (Town ctor takes DateTime?);
/// store() switch-arrow on Persistable.PersistentState -> C# switch on IPersistable.PersistentState (NEW/UPDATE_REQUIRED), set to UPDATED.
/// </summary>
public class TownDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(TownDAO));

    private const string SELECT_QUERY = "SELECT *, CAST(FLOOR(UNIX_TIMESTAMP(`level_up_date`) * 1000) AS SIGNED) AS `level_up_date_epoch_millis` FROM `towns` WHERE `race` = ?";
    private const string INSERT_QUERY = "INSERT INTO `towns`(`id`,`level`,`points`, `race`) VALUES (?,?,?,?)";
    private const string UPDATE_QUERY = "UPDATE `towns` SET `level` = ?, `points` = ?, `level_up_date` = FROM_UNIXTIME(? / 1000.0) WHERE `id` = ?";

    public static Dictionary<int, Town> Load(Race race)
    {
        Dictionary<int, Town> towns = new Dictionary<int, Town>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = race.ToString() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int id = rset.GetInt32(rset.GetOrdinal("id"));
                int level = rset.GetInt32(rset.GetOrdinal("level"));
                int points = rset.GetInt32(rset.GetOrdinal("points"));
                DateTime? levelUpDate = DatabaseTimestamp.ReadNullableUtcDateTime(rset, "level_up_date_epoch_millis");
                Town town = new Town(id, level, points, race, levelUpDate);
                towns[town.GetId()] = town;
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load towns");
        }
        return towns;
    }

    public static void Store(Town town)
    {
        switch (town.GetPersistentState())
        {
            case IPersistable.PersistentState.NEW:
                InsertTown(town);
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                UpdateTown(town);
                break;
        }
    }

    private static void InsertTown(Town town)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = town.GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = town.GetLevel() });
            stmt.Parameters.Add(new MySqlParameter { Value = town.GetPoints() });
            stmt.Parameters.Add(new MySqlParameter { Value = town.GetRace().ToString() });
            stmt.ExecuteNonQuery();
            town.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not insert town " + town.GetId());
        }
    }

    private static void UpdateTown(Town town)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = town.GetLevel() });
            stmt.Parameters.Add(new MySqlParameter { Value = town.GetPoints() });
            stmt.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(town.GetLevelUpDate()) });
            stmt.Parameters.Add(new MySqlParameter { Value = town.GetId() });
            stmt.ExecuteNonQuery();
            town.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not update town " + town.GetId());
        }
    }
}
