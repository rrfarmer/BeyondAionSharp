using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.Siege;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/SiegeDAO (@author Sarynth). JDBC DAO over siege_locations (per-location owning race/legion/balance state).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt/getString("col") ->
/// reader.GetX(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. Map&lt;Integer,SiegeLocation&gt; ->
/// Dictionary&lt;int,SiegeLocation&gt;; Map.get -> indexer; entrySet() -> foreach KeyValuePair; SiegeRace.valueOf(name) ->
/// Enum.Parse&lt;SiegeRace&gt;(name); getRace().toString() -> GetRace().ToString(). loadSiegeLocations also inserts any not-yet-persisted
/// locations, exactly as Java.
/// </summary>
public class SiegeDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(SiegeDAO));

    public const string SELECT_QUERY = "SELECT `id`, `race`, `legion_id`, `occupy_count`, `faction_balance` FROM `siege_locations`";
    public const string INSERT_QUERY = "INSERT INTO `siege_locations` (`id`, `race`, `legion_id`, `occupy_count`, `faction_balance`) VALUES(?, ?, ?, ?, ?)";
    public const string UPDATE_QUERY = "UPDATE `siege_locations` SET  `race` = ?, `legion_id` = ?, `occupy_count` = ?, `faction_balance` = ? WHERE `id` = ?";

    public static bool LoadSiegeLocations(Dictionary<int, SiegeLocation> locations)
    {
        bool success = true;
        List<int> loaded = new List<int>();

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            using MySqlDataReader resultSet = stmt.ExecuteReader();
            while (resultSet.Read())
            {
                SiegeLocation loc = locations[resultSet.GetInt32(resultSet.GetOrdinal("id"))];
                loc.SetRace(Enum.Parse<SiegeRace>(resultSet.GetString(resultSet.GetOrdinal("race"))));
                loc.SetLegionId(resultSet.GetInt32(resultSet.GetOrdinal("legion_id")));
                loc.SetOccupiedCount(resultSet.GetInt32(resultSet.GetOrdinal("occupy_count")));
                loc.SetFactionBalance(resultSet.GetInt32(resultSet.GetOrdinal("faction_balance")));
                loaded.Add(loc.GetLocationId());
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load siege locations");
            success = false;
        }

        foreach (KeyValuePair<int, SiegeLocation> entry in locations)
        {
            SiegeLocation sLoc = entry.Value;
            if (!loaded.Contains(sLoc.GetLocationId()))
            {
                InsertSiegeLocation(sLoc);
            }
        }

        return success;
    }

    public static bool UpdateSiegeLocation(SiegeLocation siegeLocation)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetRace().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetLegionId() });
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetOccupiedCount() });
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetFactionBalance() });
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetLocationId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not update siege location " + siegeLocation.GetLocationId() + " (race: " + siegeLocation.GetRace() + ")");
            return false;
        }
        return true;
    }

    private static bool InsertSiegeLocation(SiegeLocation siegeLocation)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetLocationId() });
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetRace().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetLegionId() });
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetOccupiedCount() });
            stmt.Parameters.Add(new MySqlParameter { Value = siegeLocation.GetFactionBalance() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not insert siege location " + siegeLocation.GetLocationId());
            return false;
        }
        return true;
    }
}
