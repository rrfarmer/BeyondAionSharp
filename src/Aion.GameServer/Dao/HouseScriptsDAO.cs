using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/HouseScriptsDAO (@author Rolandas, Neon, Sykra). JDBC DAO over house_scripts (compressed per-house furniture
/// scripts). DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; executeUpdate ->
/// ExecuteNonQuery; SQL (incl. ON DUPLICATE KEY UPDATE / ORDER BY date_added) verbatim. SLF4J "{}"+throwable -> LogError(e, "{HouseId}").
/// StandardCharsets.UTF_16LE -> Encoding.Unicode (UTF-16LE); scriptXML.getBytes -> Encoding.Unicode.GetBytes; CompressUtil.compress
/// -> CompressUtil.Compress; PlayerScripts.set -> PlayerScripts.Set; bytes.length -> bytes.Length.
/// </summary>
public class HouseScriptsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(HouseScriptsDAO));

    private const string INSERT_QUERY = "INSERT INTO `house_scripts` (`house_id`,`script_id`,`script`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE house_id=VALUES(house_id), script_id=VALUES(script_id), script=VALUES(script)";
    private const string DELETE_QUERY = "DELETE FROM `house_scripts` WHERE `house_id`=? AND `script_id`=?";
    private const string DELETE_ALL_QUERY = "DELETE FROM `house_scripts` WHERE `house_id`=?";
    private const string SELECT_QUERY = "SELECT `script_id`, `script` FROM `house_scripts` WHERE `house_id`=? ORDER BY `date_added`";

    public static void StoreScript(int houseId, int scriptId, string scriptXML)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = houseId });
            stmt.Parameters.Add(new MySqlParameter { Value = scriptId });
            stmt.Parameters.Add(new MySqlParameter { Value = scriptXML });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not save script data for houseId: {HouseId}", houseId);
        }
    }

    public static PlayerScripts GetPlayerScripts(int houseId)
    {
        PlayerScripts scripts = new PlayerScripts(houseId);
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = houseId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                AddScript(scripts, rset.GetInt32(rset.GetOrdinal("script_id")), rset.GetString(rset.GetOrdinal("script")));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore script data for houseId: {HouseId}", houseId);
        }
        return scripts;
    }

    public static void DeleteScript(int houseId, int scriptId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = houseId });
            stmt.Parameters.Add(new MySqlParameter { Value = scriptId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete script for houseId: {HouseId}", houseId);
        }
    }

    public static void DeleteScriptsForHouse(int houseId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_ALL_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = houseId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not delete script for houseId: {HouseId}", houseId);
        }
    }

    private static bool AddScript(PlayerScripts scripts, int id, string scriptXML)
    {
        if (scriptXML == null || scriptXML.Length == 0)
        {
            return scripts.Set(id, new byte[0], 0, false);
        }
        else
        {
            byte[] bytes = Encoding.Unicode.GetBytes(scriptXML);
            byte[] compressedBytes = CompressUtil.Compress(bytes);
            return scripts.Set(id, compressedBytes, bytes.Length, false);
        }
    }
}
