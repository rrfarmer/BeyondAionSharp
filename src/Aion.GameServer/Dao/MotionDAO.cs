using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Motion;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/MotionDAO (@author MrPoke). Plain DAO with static JDBC methods over the player_motions table.
/// JDBC try-with-resources(Connection/PreparedStatement/ResultSet) -> ADO.NET DatabaseFactory.GetConnection()/CreateCommand()/
/// ExecuteReader; positional '?' PreparedStatement params -> ordered unnamed MySqlParameter (matches the Java '?' placeholders);
/// stmt.setInt/setBoolean -> Parameters.Add{ Value }; rset.getInt/getBoolean(name) -> reader.GetX(reader.GetOrdinal(name));
/// stmt.execute() -> ExecuteNonQuery. SQL/table/column text preserved verbatim for DB backward-compat. The Java logger category
/// quirk (LoggerFactory.getLogger(PlayerEmotionListDAO.class)) is preserved literally. Synchronous, mirroring the Java DAO shape.
/// </summary>
public class MotionDAO
{
    // Java parity: copy-paste quirk - the Java DAO logs under PlayerEmotionListDAO.class.
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("PlayerEmotionListDAO");

    public const string INSERT_QUERY = "INSERT INTO `player_motions` (`player_id`, `motion_id`, `active`,  `time`) VALUES (?,?,?,?)";
    public const string SELECT_QUERY = "SELECT `motion_id`, `active`, `time` FROM `player_motions` WHERE `player_id`=?";
    public const string DELETE_QUERY = "DELETE FROM `player_motions` WHERE `player_id`=? AND `motion_id`=?";
    public const string UPDATE_QUERY = "UPDATE `player_motions` SET `active`=? WHERE `player_id`=? AND `motion_id`=?";

    public static void LoadMotionList(Player player)
    {
        MotionList motions = new MotionList(player);
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int motionId = rset.GetInt32(rset.GetOrdinal("motion_id"));
                int time = rset.GetInt32(rset.GetOrdinal("time"));
                bool isActive = rset.GetBoolean(rset.GetOrdinal("active"));
                motions.Add(new Motion(motionId, time, isActive), false);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore motions for playerObjId: " + player.GetObjectId() + " from DB: " + e.Message);
        }
        player.SetMotions(motions);
    }

    public static bool StoreMotion(int objectId, Motion motion)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = objectId });
            stmt.Parameters.Add(new MySqlParameter { Value = motion.GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = motion.IsActive() });
            stmt.Parameters.Add(new MySqlParameter { Value = motion.GetExpireTime() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not store motion for player " + objectId + " from DB: " + e.Message);
            return false;
        }
        return true;
    }

    public static bool DeleteMotion(int objectId, int motionId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = objectId });
            stmt.Parameters.Add(new MySqlParameter { Value = motionId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete motion for player " + objectId + " from DB: " + e.Message);
            return false;
        }
        return true;
    }

    public static bool UpdateMotion(int objectId, Motion motion)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = motion.IsActive() });
            stmt.Parameters.Add(new MySqlParameter { Value = objectId });
            stmt.Parameters.Add(new MySqlParameter { Value = motion.GetId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not store motion for player " + objectId + " from DB: " + e.Message);
            return false;
        }
        return true;
    }
}
