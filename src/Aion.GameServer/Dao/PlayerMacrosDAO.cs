using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerMacrosDAO (@author Aquanox). JDBC DAO over player_macrosses. MIXED: add/update/delete via the commons DB
/// callback helper (DB.InsertUpdate(IUStH), anonymous->nested handlers); loadMacros via DatabaseFactory. setInt/setString->Parameters.Add;
/// execute()->ExecuteNonQuery; rset.next()/getInt/getString->Read()/GetInt32/GetString(GetOrdinal); Macros.Add(order, macro). SQL verbatim
/// (note the `order` reserved-word column kept backticked).
/// </summary>
public class PlayerMacrosDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerMacrosDAO));

    public const string INSERT_QUERY = "INSERT INTO `player_macrosses` (`player_id`, `order`, `macro`) VALUES (?,?,?)";
    public const string UPDATE_QUERY = "UPDATE `player_macrosses` SET `macro`=? WHERE `player_id`=? AND `order`=?";
    public const string DELETE_QUERY = "DELETE FROM `player_macrosses` WHERE `player_id`=? AND `order`=?";
    public const string SELECT_QUERY = "SELECT `order`, `macro` FROM `player_macrosses` WHERE `player_id`=?";

    public static void AddMacro(int playerId, int macroPosition, string macro)
    {
        DB.InsertUpdate(INSERT_QUERY, new AddMacroHandler(playerId, macroPosition, macro));
    }

    private sealed class AddMacroHandler : IUStH
    {
        private readonly int playerId;
        private readonly int macroPosition;
        private readonly string macro;

        internal AddMacroHandler(int playerId, int macroPosition, string macro)
        {
            this.playerId = playerId;
            this.macroPosition = macroPosition;
            this.macro = macro;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = macroPosition });
            stmt.Parameters.Add(new MySqlParameter { Value = macro });
            stmt.ExecuteNonQuery();
        }
    }

    public static void UpdateMacro(int playerId, int macroPosition, string macro)
    {
        DB.InsertUpdate(UPDATE_QUERY, new UpdateMacroHandler(playerId, macroPosition, macro));
    }

    private sealed class UpdateMacroHandler : IUStH
    {
        private readonly int playerId;
        private readonly int macroPosition;
        private readonly string macro;

        internal UpdateMacroHandler(int playerId, int macroPosition, string macro)
        {
            this.playerId = playerId;
            this.macroPosition = macroPosition;
            this.macro = macro;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = macro });
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = macroPosition });
            stmt.ExecuteNonQuery();
        }
    }

    public static void DeleteMacro(int playerId, int macroPosition)
    {
        DB.InsertUpdate(DELETE_QUERY, new DeleteMacroHandler(playerId, macroPosition));
    }

    private sealed class DeleteMacroHandler : IUStH
    {
        private readonly int playerId;
        private readonly int macroPosition;

        internal DeleteMacroHandler(int playerId, int macroPosition)
        {
            this.playerId = playerId;
            this.macroPosition = macroPosition;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = macroPosition });
            stmt.ExecuteNonQuery();
        }
    }

    public static Macros LoadMacros(int playerId)
    {
        Macros macros = new Macros();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                macros.Add(rset.GetInt32(rset.GetOrdinal("order")), rset.GetString(rset.GetOrdinal("macro")));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load macros for player " + playerId);
        }
        return macros;
    }
}
