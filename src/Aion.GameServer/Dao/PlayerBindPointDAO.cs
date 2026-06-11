using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerBindPointDAO (@author evilset). JDBC DAO over player_bind_point (per-player resurrection bind location).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt/getFloat/getByte("col")
/// -> reader.GetInt32/GetFloat/GetByte(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. Persistable.PersistentState
/// -> IPersistable.PersistentState (NEW/UPDATE_REQUIRED switch, set UPDATED). Java quirk preserved: updateBindPoint binds the player_id
/// WHERE param via setFloat (an int passed as float) -> (float)player.GetObjectId().
/// </summary>
public class PlayerBindPointDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerBindPointDAO));

    public const string INSERT_QUERY = "REPLACE INTO `player_bind_point` (`player_id`, `map_id`, `x`, `y`, `z`, `heading`) VALUES (?,?,?,?,?,?)";
    public const string SELECT_QUERY = "SELECT `map_id`, `x`, `y`, `z`, `heading` FROM `player_bind_point` WHERE `player_id`=?";
    public const string UPDATE_QUERY = "UPDATE player_bind_point set `map_id`=?, `x`=?, `y`=? , `z`=?, `heading`=? WHERE `player_id`=?";

    public static void LoadBindPoint(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            if (rset.Read())
            {
                int mapId = rset.GetInt32(rset.GetOrdinal("map_id"));
                float x = rset.GetFloat(rset.GetOrdinal("x"));
                float y = rset.GetFloat(rset.GetOrdinal("y"));
                float z = rset.GetFloat(rset.GetOrdinal("z"));
                byte heading = rset.GetByte(rset.GetOrdinal("heading"));
                BindPointPosition bind = new BindPointPosition(mapId, x, y, z, heading);
                bind.SetPersistentState(IPersistable.PersistentState.UPDATED);
                player.SetBindPoint(bind);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore BindPointPosition data for playerObjId: " + player.GetObjectId() + " from DB: " + e.Message);
        }
    }

    public static bool InsertBindPoint(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            BindPointPosition bpp = player.GetBindPoint();
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetMapId() });
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetX() });
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetY() });
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetZ() });
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetHeading() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not store BindPointPosition data for player " + player.GetObjectId() + " from DB: " + e.Message);
            return false;
        }
        return true;
    }

    public static bool UpdateBindPoint(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            BindPointPosition bpp = player.GetBindPoint();
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetMapId() });
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetX() });
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetY() });
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetZ() });
            stmt.Parameters.Add(new MySqlParameter { Value = bpp.GetHeading() });
            stmt.Parameters.Add(new MySqlParameter { Value = (float)player.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not update BindPointPosition data for player " + player.GetObjectId() + " from DB: " + e.Message);
            return false;
        }
        return true;
    }

    public static bool Store(Player player)
    {
        bool insert = false;
        BindPointPosition bind = player.GetBindPoint();

        switch (bind.GetPersistentState())
        {
            case IPersistable.PersistentState.NEW:
                insert = InsertBindPoint(player);
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                insert = UpdateBindPoint(player);
                break;
        }
        bind.SetPersistentState(IPersistable.PersistentState.UPDATED);
        return insert;
    }
}
