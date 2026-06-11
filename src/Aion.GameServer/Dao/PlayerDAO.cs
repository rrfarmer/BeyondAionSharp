using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Legion;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerDAO (@author SoulKeeper, Saelya, cura, KID, xTz). JDBC DAO over the players table. MIXED across all three
/// DB-access styles. String[1]/int[1] capture tricks -> nested handlers with Result fields. Gender/Race/PlayerClass valueOf->Enum.Parse,
/// toString()/name()->ToString(); getTimestamp("last_online") (nullable)->IsDBNull?null:GetDateTime (SetLastOnline DateTime?); creation/
/// deletion dates use DateTimeOffset? (new DateTimeOffset(GetDateTime)); setTimestamp(null)->DBNull.Value; getShort/getByte->GetInt16/GetByte;
/// getUsedIDs scrollable->forward-only; deletePlayer via DB.PrepareStatement+ExecuteUpdateAndClose; Mailbox.size()->Size(); Java text-block
/// SQL->verbatim @-string. Nested Java record PlayerAndLegionInfo->C# positional record (Java lowercase components). DataManager.PLAYER_EXPERIENCE_TABLE/
/// GSConfig.RATIO_MIN_REQUIRED_LEVEL red-tolerated. SQL verbatim.
/// </summary>
public class PlayerDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerDAO));

    public static bool IsNameUsed(string name)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT count(id) as cnt FROM players WHERE ? = players.name";
            stmt.Parameters.Add(new MySqlParameter { Value = name });
            using MySqlDataReader rs = stmt.ExecuteReader();
            rs.Read();
            return rs.GetInt32(rs.GetOrdinal("cnt")) > 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't check if name " + name + " is used, returning positive result");
            return true;
        }
    }

    public static void StorePlayer(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText =
                "UPDATE players SET name=?, exp=?, recoverexp=?, x=?, y=?, z=?, heading=?, world_id=?, gender=?, race=?, player_class=?, quest_expands=?, npc_expands=?, item_expands=?, wh_npc_expands=?, wh_bonus_expands=?, note=?, title_id=?, bonus_title_id=?, dp=?, soul_sickness=?, mailbox_letters=?, reposte_energy=?, mentor_flag_time=?, world_owner=? WHERE id=?";
            PlayerCommonData pcd = player.GetCommonData();
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetName() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetExp() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetExpRecoverable() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetX() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetY() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetZ() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetHeading() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetWorldId() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetGender().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetRace().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetPlayerClass().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetQuestExpands() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetNpcExpands() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetItemExpands() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetWhNpcExpands() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetWhBonusExpands() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetNote() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetTitleId() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetBonusTitleId() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetDp() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetDeathCount() });
            Mailbox mailBox = player.GetMailbox();
            int mails = mailBox != null ? mailBox.Size() : pcd.GetMailboxLetters();
            stmt.Parameters.Add(new MySqlParameter { Value = mails });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetCurrentReposeEnergy() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetMentorFlagTime() });
            stmt.Parameters.Add(new MySqlParameter { Value = pcd.GetWorldOwnerId() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error saving " + player);
        }
    }

    public static bool SaveNewPlayer(Player player, int accountId, string accountName)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText =
                "INSERT INTO players(id, `name`, account_id, account_name, x, y, z, heading, world_id, gender, race, player_class , quest_expands, npc_expands, item_expands, wh_npc_expands, wh_bonus_expands, online) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)";
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetName() });
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            stmt.Parameters.Add(new MySqlParameter { Value = accountName });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetCommonData().GetX() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetCommonData().GetY() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetCommonData().GetZ() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetCommonData().GetHeading() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetCommonData().GetMapId() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetGender().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetRace().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetPlayerClass().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetQuestExpands() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetNpcExpands() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetItemExpands() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetWhNpcExpands() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetWhBonusExpands() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error saving new " + player);
            return false;
        }
        return true;
    }

    public static PlayerCommonData LoadPlayerCommonDataByName(string name)
    {
        int playerObjId = 0;

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT id FROM players WHERE name = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = name });
            using MySqlDataReader rset = stmt.ExecuteReader();
            if (rset.Read())
                playerObjId = rset.GetInt32(rset.GetOrdinal("id"));
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore playerId data for player name: " + name + " from DB: " + e.Message);
        }

        if (playerObjId == 0)
        {
            return null;
        }
        return LoadPlayerCommonData(playerObjId);
    }

    public static PlayerCommonData LoadPlayerCommonData(int playerObjId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT * FROM players WHERE id = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjId });
            using MySqlDataReader resultSet = stmt.ExecuteReader();
            if (resultSet.Read())
            {
                PlayerCommonData cd = new PlayerCommonData(playerObjId);
                cd.SetName(resultSet.GetString(resultSet.GetOrdinal("name")));
                cd.SetPlayerClass(Enum.Parse<PlayerClass>(resultSet.GetString(resultSet.GetOrdinal("player_class"))));
                cd.SetExp(resultSet.GetInt64(resultSet.GetOrdinal("exp"))); // set class before exp for daeva determination
                cd.SetRecoverableExp(resultSet.GetInt64(resultSet.GetOrdinal("recoverexp")));
                cd.SetRace(Enum.Parse<Race>(resultSet.GetString(resultSet.GetOrdinal("race"))));
                cd.SetGender(Enum.Parse<Gender>(resultSet.GetString(resultSet.GetOrdinal("gender"))));
                int loOrd = resultSet.GetOrdinal("last_online");
                cd.SetLastOnline(resultSet.IsDBNull(loOrd) ? (DateTime?)null : resultSet.GetDateTime(loOrd));
                cd.SetNote(resultSet.GetString(resultSet.GetOrdinal("note")));
                cd.SetQuestExpands(resultSet.GetInt32(resultSet.GetOrdinal("quest_expands")));
                cd.SetNpcExpands(resultSet.GetInt32(resultSet.GetOrdinal("npc_expands")));
                cd.SetItemExpands(resultSet.GetInt32(resultSet.GetOrdinal("item_expands")));
                cd.SetTitleId(resultSet.GetInt32(resultSet.GetOrdinal("title_id")));
                cd.SetBonusTitleId(resultSet.GetInt32(resultSet.GetOrdinal("bonus_title_id")));
                cd.SetWhNpcExpands(resultSet.GetInt32(resultSet.GetOrdinal("wh_npc_expands")));
                cd.SetWhBonusExpands(resultSet.GetInt32(resultSet.GetOrdinal("wh_bonus_expands")));
                cd.SetOnline(resultSet.GetBoolean(resultSet.GetOrdinal("online")));
                cd.SetMailboxLetters(resultSet.GetInt32(resultSet.GetOrdinal("mailbox_letters")));
                cd.SetDp(resultSet.GetInt32(resultSet.GetOrdinal("dp")));
                cd.SetDeathCount(resultSet.GetInt32(resultSet.GetOrdinal("soul_sickness")));
                cd.SetCurrentReposeEnergy(resultSet.GetInt64(resultSet.GetOrdinal("reposte_energy")));
                cd.SetX(resultSet.GetFloat(resultSet.GetOrdinal("x")));
                cd.SetY(resultSet.GetFloat(resultSet.GetOrdinal("y")));
                cd.SetZ(resultSet.GetFloat(resultSet.GetOrdinal("z")));
                cd.SetHeading(resultSet.GetByte(resultSet.GetOrdinal("heading")));
                cd.SetMapId(resultSet.GetInt32(resultSet.GetOrdinal("world_id")));
                cd.SetWorldOwnerId(resultSet.GetInt32(resultSet.GetOrdinal("world_owner")));
                cd.SetMentorFlagTime(resultSet.GetInt32(resultSet.GetOrdinal("mentor_flag_time")));
                cd.SetLastTransferTime(resultSet.GetInt64(resultSet.GetOrdinal("last_transfer_time")));
                return cd;
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load PlayerCommonData data for player: " + playerObjId);
        }
        return null;
    }

    /// <summary>Removes player and all related data (via CASCADE DELETION)</summary>
    public static void DeletePlayer(int playerId)
    {
        MySqlCommand statement = DB.PrepareStatement("DELETE FROM players WHERE id = ?");
        try
        {
            statement.Parameters.Add(new MySqlParameter { Value = playerId });
        }
        catch (Exception e)
        {
            log.LogError(e, "Some crap, can't set int parameter to PreparedStatement");
        }
        DB.ExecuteUpdateAndClose(statement);
    }

    public static List<int> GetPlayerOidsOnAccount(int accountId)
    {
        List<int> result = new List<int>();
        bool success = DB.Select("SELECT id FROM players WHERE account_id = ?", new OidsOnAccountHandler(accountId, result));

        return success ? result : null;
    }

    private sealed class OidsOnAccountHandler : ParamReadStH
    {
        private readonly int accountId;
        private readonly List<int> result;

        internal OidsOnAccountHandler(int accountId, List<int> result)
        {
            this.accountId = accountId;
            this.result = result;
        }

        public void HandleRead(MySqlDataReader resultSet)
        {
            while (resultSet.Read())
            {
                result.Add(resultSet.GetInt32(resultSet.GetOrdinal("id")));
            }
        }

        public void SetParams(MySqlCommand preparedStatement)
        {
            preparedStatement.Parameters.Add(new MySqlParameter { Value = accountId });
        }
    }

    public static List<int> GetPlayerOidsOnAccount(int accountId, long exp)
    {
        List<int> result = new List<int>();
        bool success = DB.Select("SELECT id FROM players WHERE account_id = ? AND exp <= ?", new OidsOnAccountByExpHandler(accountId, exp, result));

        return success ? result : null;
    }

    private sealed class OidsOnAccountByExpHandler : ParamReadStH
    {
        private readonly int accountId;
        private readonly long exp;
        private readonly List<int> result;

        internal OidsOnAccountByExpHandler(int accountId, long exp, List<int> result)
        {
            this.accountId = accountId;
            this.exp = exp;
            this.result = result;
        }

        public void HandleRead(MySqlDataReader resultSet)
        {
            while (resultSet.Read())
            {
                result.Add(resultSet.GetInt32(resultSet.GetOrdinal("id")));
            }
        }

        public void SetParams(MySqlCommand preparedStatement)
        {
            preparedStatement.Parameters.Add(new MySqlParameter { Value = accountId });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = exp });
        }
    }

    public static void SetCreationDeletionTime(PlayerAccountData acData)
    {
        DB.Select("SELECT creation_date, deletion_date FROM players WHERE id = ?", new CreationDeletionHandler(acData));
    }

    private sealed class CreationDeletionHandler : ParamReadStH
    {
        private readonly PlayerAccountData acData;

        internal CreationDeletionHandler(PlayerAccountData acData)
        {
            this.acData = acData;
        }

        public void SetParams(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = acData.GetPlayerCommonData().GetPlayerObjId() });
        }

        public void HandleRead(MySqlDataReader rset)
        {
            rset.Read();

            int ddOrd = rset.GetOrdinal("deletion_date");
            acData.SetDeletionDate(rset.IsDBNull(ddOrd) ? (DateTimeOffset?)null : new DateTimeOffset(rset.GetDateTime(ddOrd)));
            int cdOrd = rset.GetOrdinal("creation_date");
            acData.SetCreationDate(rset.IsDBNull(cdOrd) ? (DateTimeOffset?)null : new DateTimeOffset(rset.GetDateTime(cdOrd)));
        }
    }

    public static void UpdateDeletionTime(int objectId, DateTime? deletionDate)
    {
        DB.InsertUpdate("UPDATE players set deletion_date = ? where id = ?", new UpdateTimeHandler(deletionDate, objectId));
    }

    public static void StoreCreationTime(int objectId, DateTime? creationDate)
    {
        DB.InsertUpdate("UPDATE players set creation_date = ? where id = ?", new UpdateTimeHandler(creationDate, objectId));
    }

    public static void StoreLastOnlineTime(int objectId, DateTime? lastOnline)
    {
        DB.InsertUpdate("UPDATE players set last_online = ? where id = ?", new UpdateTimeHandler(lastOnline, objectId));
    }

    private sealed class UpdateTimeHandler : IUStH
    {
        private readonly DateTime? time;
        private readonly int objectId;

        internal UpdateTimeHandler(DateTime? time, int objectId)
        {
            this.time = time;
            this.objectId = objectId;
        }

        public void HandleInsertUpdate(MySqlCommand preparedStatement)
        {
            preparedStatement.Parameters.Add(new MySqlParameter { Value = (object)time ?? DBNull.Value });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = objectId });
            preparedStatement.ExecuteNonQuery();
        }
    }

    public static int[] GetUsedIDs()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT id FROM players";
            using MySqlDataReader rs = stmt.ExecuteReader();
            List<int> ids = new List<int>();
            while (rs.Read())
                ids.Add(rs.GetInt32(rs.GetOrdinal("id")));
            return ids.ToArray();
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't get list of IDs from players table");
            return null;
        }
    }

    public static bool IsOnline(int playerId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT online FROM players WHERE id=?";
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rs = stmt.ExecuteReader();
            if (rs.Read())
                return rs.GetBoolean(rs.GetOrdinal("online"));
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't get online state of player " + playerId);
        }
        return false;
    }

    public static void OnlinePlayer(Player player, bool online)
    {
        DB.InsertUpdate("UPDATE players SET online=? WHERE id=?", new OnlinePlayerHandler(player, online));
    }

    private sealed class OnlinePlayerHandler : IUStH
    {
        private readonly Player player;
        private readonly bool online;

        internal OnlinePlayerHandler(Player player, bool online)
        {
            this.player = player;
            this.online = online;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = online });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
    }

    public static void SetAllPlayersOffline()
    {
        DB.InsertUpdate("UPDATE players SET online=?", new SetAllOfflineHandler());
    }

    private sealed class SetAllOfflineHandler : IUStH
    {
        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = false });
            stmt.ExecuteNonQuery();
        }
    }

    public static string GetPlayerNameByObjId(int playerObjId)
    {
        GetNameHandler handler = new GetNameHandler(playerObjId);
        DB.Select("SELECT name FROM players WHERE id = ?", handler);
        return handler.Result;
    }

    private sealed class GetNameHandler : ParamReadStH
    {
        private readonly int playerObjId;
        internal string Result;

        internal GetNameHandler(int playerObjId)
        {
            this.playerObjId = playerObjId;
        }

        public void HandleRead(MySqlDataReader arg0)
        {
            if (arg0.Read())
                Result = arg0.GetString(arg0.GetOrdinal("name"));
        }

        public void SetParams(MySqlCommand arg0)
        {
            arg0.Parameters.Add(new MySqlParameter { Value = playerObjId });
        }
    }

    public static int GetPlayerIdByName(string playerName)
    {
        GetIdHandler handler = new GetIdHandler(playerName);
        DB.Select("SELECT id FROM players WHERE name = ?", handler);
        return handler.Result;
    }

    private sealed class GetIdHandler : ParamReadStH
    {
        private readonly string playerName;
        internal int Result;

        internal GetIdHandler(string playerName)
        {
            this.playerName = playerName;
        }

        public void HandleRead(MySqlDataReader arg0)
        {
            if (arg0.Read())
                Result = arg0.GetInt32(arg0.GetOrdinal("id"));
        }

        public void SetParams(MySqlCommand arg0)
        {
            arg0.Parameters.Add(new MySqlParameter { Value = playerName });
        }
    }

    public static int GetAccountIdByName(string name)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT `account_id` FROM `players` WHERE `name` = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = name });
            using MySqlDataReader rs = stmt.ExecuteReader();
            if (rs.Read())
                return rs.GetInt32(rs.GetOrdinal("account_id"));
        }
        catch (Exception e)
        {
            log.LogError(e, "");
        }
        return 0;
    }

    public static int GetAccountId(int playerId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT `account_id` FROM `players` WHERE `id` = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rs = stmt.ExecuteReader();
            if (rs.Read())
                return rs.GetInt32(rs.GetOrdinal("account_id"));
        }
        catch (Exception e)
        {
            log.LogError(e, "");
        }
        return 0;
    }

    public static void StorePlayerName(PlayerCommonData recipientCommonData)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "UPDATE players SET name=? WHERE id=?";
            stmt.Parameters.Add(new MySqlParameter { Value = recipientCommonData.GetName() });
            stmt.Parameters.Add(new MySqlParameter { Value = recipientCommonData.GetPlayerObjId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error saving playerName: " + recipientCommonData.GetPlayerObjId() + " " + recipientCommonData.GetName());
        }
    }

    public static int GetCharacterCountOnAccount(int accountId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT COUNT(*) AS cnt FROM `players` WHERE `account_id` = ? AND (players.deletion_date IS NULL || players.deletion_date > CURRENT_TIMESTAMP)";
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            using MySqlDataReader rs = stmt.ExecuteReader();
            rs.Read();
            return rs.GetInt32(rs.GetOrdinal("cnt"));
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static int GetCharacterCountForRace(Race race)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT COUNT(DISTINCT(`account_id`)) AS `count` FROM `players` WHERE `race` = ? AND `exp` >= ?";
            stmt.Parameters.Add(new MySqlParameter { Value = race.ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(GSConfig.RATIO_MIN_REQUIRED_LEVEL) });
            using MySqlDataReader rs = stmt.ExecuteReader();
            rs.Read();
            return rs.GetInt32(rs.GetOrdinal("count"));
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static List<PlayerAndLegionInfo> GetPlayersOnInactiveAccounts(long maxExp, int daysOfAccountInactivity)
    {
        List<PlayerAndLegionInfo> players = new List<PlayerAndLegionInfo>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = @"SELECT p.id, p.name, m.legion_id, m.rank
FROM players p
LEFT JOIN legion_members m ON p.id = m.player_id
WHERE p.exp <= ? AND p.account_id IN (SELECT account_id FROM players GROUP BY account_id HAVING MAX(last_online) < NOW() - INTERVAL ? DAY)
";
            stmt.Parameters.Add(new MySqlParameter { Value = maxExp });
            stmt.Parameters.Add(new MySqlParameter { Value = daysOfAccountInactivity });
            using MySqlDataReader rs = stmt.ExecuteReader();
            while (rs.Read())
            {
                int rankOrd = rs.GetOrdinal("rank");
                string legionRank = rs.IsDBNull(rankOrd) ? null : rs.GetString(rankOrd);
                players.Add(new PlayerAndLegionInfo(rs.GetInt32(rs.GetOrdinal("id")), rs.GetString(rs.GetOrdinal("name")), rs.GetInt32(rs.GetOrdinal("legion_id")), legionRank == null ? (LegionRank?)null : Enum.Parse<LegionRank>(legionRank)));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't get inactive players");
        }
        return players;
    }

    public static void SetPlayerLastTransferTime(int playerId, long time)
    {
        DB.InsertUpdate("UPDATE players SET last_transfer_time=? WHERE id=?", new LastTransferTimeHandler(playerId, time));
    }

    private sealed class LastTransferTimeHandler : IUStH
    {
        private readonly int playerId;
        private readonly long time;

        internal LastTransferTimeHandler(int playerId, long time)
        {
            this.playerId = playerId;
            this.time = time;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = time });
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.ExecuteNonQuery();
        }
    }

    public static int GetOldCharacterLevel(int playerObjectId)
    {
        int oldLevel = 0;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT old_level FROM players WHERE id=?";
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjectId });
            using MySqlDataReader rs = stmt.ExecuteReader();
            if (rs.Read())
                oldLevel = rs.GetInt32(rs.GetOrdinal("old_level"));
        }
        catch (Exception e)
        {
            log.LogError(e, "Error reading old_level for player: " + playerObjectId);
        }
        return oldLevel;
    }

    public static void StoreOldCharacterLevel(int playerObjectId, int level)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "UPDATE players SET old_level=? WHERE id=?";
            stmt.Parameters.Add(new MySqlParameter { Value = level });
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjectId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error storing old_level: " + level + " for player: " + playerObjectId);
        }
    }

    public record PlayerAndLegionInfo(int playerId, string name, int legionId, LegionRank? legionRank);
}
