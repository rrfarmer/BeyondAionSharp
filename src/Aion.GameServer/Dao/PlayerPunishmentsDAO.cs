using System;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using PunishmentType = Aion.GameServer.Services.PunishmentService.PunishmentType;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerPunishmentsDAO (@author lord_rex, Cura, nrg). JDBC DAO over player_punishments via the commons DB callback
/// helper. Anonymous ParamReadStH/IUStH -> private nested classes capturing locals via ctor. PunishmentType.valueOf->Enum.Parse,
/// .toString()->ToString(); System.currentTimeMillis()->DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); setInt/setLong/setString->
/// Parameters.Add; rs.next()/getLong/getString->Read()/GetInt64/GetString(GetOrdinal). getCharBanInfo's Java CharacterBanInfo[1] capture
/// trick -> nested handler with a Result field. SQL verbatim. These methods are consumed by the reworked PunishmentService.
/// </summary>
public class PlayerPunishmentsDAO
{
    public const string SELECT_DURATIONS_QUERY = "SELECT `punishment_type`, `duration` FROM `player_punishments` WHERE `player_id`=?";
    public const string SELECT_QUERY = "SELECT `player_id`, `start_time`, `duration`, `reason` FROM `player_punishments` WHERE `player_id`=? AND `punishment_type`=?";
    public const string UPDATE_QUERY = "UPDATE `player_punishments` SET `duration`=? WHERE `player_id`=? AND `punishment_type`=?";
    public const string REPLACE_QUERY = "REPLACE INTO `player_punishments` VALUES (?,?,?,?,?)";
    public const string DELETE_QUERY = "DELETE FROM `player_punishments` WHERE `player_id`=? AND `punishment_type`=?";

    public static void LoadPlayerPunishments(Player player)
    {
        DB.Select(SELECT_DURATIONS_QUERY, new LoadHandler(player));
    }

    private sealed class LoadHandler : ParamReadStH
    {
        private readonly Player player;

        internal LoadHandler(Player player)
        {
            this.player = player;
        }

        public void SetParams(MySqlCommand ps)
        {
            ps.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
        }

        public void HandleRead(MySqlDataReader rs)
        {
            while (rs.Read())
            {
                PunishmentType punishmentType = Enum.Parse<PunishmentType>(rs.GetString(rs.GetOrdinal("punishment_type")));
                if (punishmentType == PunishmentType.PRISON)
                {
                    player.SetPrisonEndTimeMillis(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + rs.GetInt64(rs.GetOrdinal("duration")) * 1000);
                }
                else if (punishmentType == PunishmentType.GATHER)
                {
                    player.SetGatherRestrictionExpirationTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + rs.GetInt64(rs.GetOrdinal("duration")) * 1000);
                }
            }
        }
    }

    public static void StorePlayerPunishment(Player player, PunishmentType punishmentType)
    {
        DB.InsertUpdate(UPDATE_QUERY, new StoreHandler(player, punishmentType));
    }

    private sealed class StoreHandler : IUStH
    {
        private readonly Player player;
        private readonly PunishmentType punishmentType;

        internal StoreHandler(Player player, PunishmentType punishmentType)
        {
            this.player = player;
            this.punishmentType = punishmentType;
        }

        public void HandleInsertUpdate(MySqlCommand ps)
        {
            if (punishmentType == PunishmentType.PRISON)
            {
                ps.Parameters.Add(new MySqlParameter { Value = player.GetPrisonDurationSeconds() });
            }
            else if (punishmentType == PunishmentType.GATHER)
            {
                ps.Parameters.Add(new MySqlParameter { Value = player.GetGatherRestrictionDurationSeconds() });
            }
            ps.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            ps.Parameters.Add(new MySqlParameter { Value = punishmentType.ToString() });
            ps.ExecuteNonQuery();
        }
    }

    public static void PunishPlayer(int playerId, PunishmentType punishmentType, long duration, string reason)
    {
        DB.InsertUpdate(REPLACE_QUERY, new PunishHandler(playerId, punishmentType, duration, reason));
    }

    private sealed class PunishHandler : IUStH
    {
        private readonly int playerId;
        private readonly PunishmentType punishmentType;
        private readonly long duration;
        private readonly string reason;

        internal PunishHandler(int playerId, PunishmentType punishmentType, long duration, string reason)
        {
            this.playerId = playerId;
            this.punishmentType = punishmentType;
            this.duration = duration;
            this.reason = reason;
        }

        public void HandleInsertUpdate(MySqlCommand ps)
        {
            ps.Parameters.Add(new MySqlParameter { Value = playerId });
            ps.Parameters.Add(new MySqlParameter { Value = punishmentType.ToString() });
            ps.Parameters.Add(new MySqlParameter { Value = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000 });
            ps.Parameters.Add(new MySqlParameter { Value = duration });
            ps.Parameters.Add(new MySqlParameter { Value = reason });
            ps.ExecuteNonQuery();
        }
    }

    public static void PunishPlayer(Player player, PunishmentType punishmentType, string reason)
    {
        if (punishmentType == PunishmentType.PRISON)
            PunishPlayer(player.GetObjectId(), punishmentType, player.GetPrisonDurationSeconds(), reason);
        else if (punishmentType == PunishmentType.GATHER)
            PunishPlayer(player.GetObjectId(), punishmentType, player.GetGatherRestrictionDurationSeconds(), reason);
    }

    public static void UnpunishPlayer(int playerId, PunishmentType punishmentType)
    {
        DB.InsertUpdate(DELETE_QUERY, new UnpunishHandler(playerId, punishmentType));
    }

    private sealed class UnpunishHandler : IUStH
    {
        private readonly int playerId;
        private readonly PunishmentType punishmentType;

        internal UnpunishHandler(int playerId, PunishmentType punishmentType)
        {
            this.playerId = playerId;
            this.punishmentType = punishmentType;
        }

        public void HandleInsertUpdate(MySqlCommand ps)
        {
            ps.Parameters.Add(new MySqlParameter { Value = playerId });
            ps.Parameters.Add(new MySqlParameter { Value = punishmentType.ToString() });
            ps.ExecuteNonQuery();
        }
    }

    public static CharacterBanInfo GetCharBanInfo(int playerId)
    {
        CharBanHandler handler = new CharBanHandler(playerId);
        DB.Select(SELECT_QUERY, handler);
        return handler.Result;
    }

    private sealed class CharBanHandler : ParamReadStH
    {
        private readonly int playerId;
        internal CharacterBanInfo Result;

        internal CharBanHandler(int playerId)
        {
            this.playerId = playerId;
        }

        public void SetParams(MySqlCommand ps)
        {
            ps.Parameters.Add(new MySqlParameter { Value = playerId });
            ps.Parameters.Add(new MySqlParameter { Value = PunishmentType.CHARBAN.ToString() });
        }

        public void HandleRead(MySqlDataReader rs)
        {
            while (rs.Read())
            {
                int reasonOrd = rs.GetOrdinal("reason"); // reason is nullable (text, no NOT NULL); Java getString tolerates null, C# GetString throws on DBNull
                string reason = rs.IsDBNull(reasonOrd) ? null : rs.GetString(reasonOrd);
                Result = new CharacterBanInfo(rs.GetInt64(rs.GetOrdinal("start_time")), rs.GetInt64(rs.GetOrdinal("duration")), reason);
            }
        }
    }
}
