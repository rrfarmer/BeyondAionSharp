using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerCooldownsDAO (@author nrg). JDBC DAO over player_cooldowns. MIXED: load/delete via the commons DB callback
/// helper (DB.Select(ParamReadStH)/DB.InsertUpdate(IUStH), anonymous->nested), store via DatabaseFactory batch. setInt/setLong->Parameters.Add;
/// rset.next()/getInt/getLong->Read()/GetInt32/GetInt64(GetOrdinal). System.currentTimeMillis()->DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().
/// values().removeIf(null||reuseTime-now&lt;=28000)->Where+Remove on a dictionary copy (C# Dictionary&lt;int,long&gt; values are non-null
/// so the null check is moot). setAutoCommit(false)+addBatch/executeBatch+commit->MySqlTransaction+MySqlBatch+Commit. SQL verbatim.
/// player.SetSkillCoolDown/GetSkillCoolDowns; GetSkillCoolDowns may be null (guarded).
/// </summary>
public class PlayerCooldownsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerCooldownsDAO));

    public const string INSERT_QUERY = "INSERT INTO `player_cooldowns` (`player_id`, `cooldown_id`, `reuse_delay`) VALUES (?,?,?)";
    public const string DELETE_QUERY = "DELETE FROM `player_cooldowns` WHERE `player_id`=?";
    public const string SELECT_QUERY = "SELECT `cooldown_id`, `reuse_delay` FROM `player_cooldowns` WHERE `player_id`=?";

    public static void LoadPlayerCooldowns(Player player)
    {
        DB.Select(SELECT_QUERY, new LoadHandler(player));
    }

    private sealed class LoadHandler : ParamReadStH
    {
        private readonly Player player;

        internal LoadHandler(Player player)
        {
            this.player = player;
        }

        public void SetParams(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
        }

        public void HandleRead(MySqlDataReader rset)
        {
            while (rset.Read())
            {
                int cooldownId = rset.GetInt32(rset.GetOrdinal("cooldown_id"));
                long reuseDelay = rset.GetInt64(rset.GetOrdinal("reuse_delay"));

                if (reuseDelay > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    player.SetSkillCoolDown(cooldownId, reuseDelay);
            }
        }
    }

    public static void StorePlayerCooldowns(Player player)
    {
        DeletePlayerCooldowns(player);

        IDictionary<int, long> cooldowns = player.GetSkillCoolDowns();
        if (cooldowns == null || cooldowns.Count == 0)
            return;

        cooldowns = new Dictionary<int, long>(cooldowns);
        foreach (int key in cooldowns
            .Where(e => e.Value - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() <= 28000)
            .Select(e => e.Key).ToList())
        {
            cooldowns.Remove(key);
        }

        if (cooldowns.Count == 0)
            return;

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlTransaction transaction = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, transaction);

            foreach (KeyValuePair<int, long> entry in cooldowns)
            {
                MySqlBatchCommand st = new MySqlBatchCommand(INSERT_QUERY);
                st.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
                st.Parameters.Add(new MySqlParameter { Value = entry.Key });
                st.Parameters.Add(new MySqlParameter { Value = entry.Value });
                batch.BatchCommands.Add(st);
            }

            batch.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Couldn't save cooldowns for " + player);
        }
    }

    private static void DeletePlayerCooldowns(Player player)
    {
        DB.InsertUpdate(DELETE_QUERY, new DeleteHandler(player));
    }

    private sealed class DeleteHandler : IUStH
    {
        private readonly Player player;

        internal DeleteHandler(Player player)
        {
            this.player = player;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
    }
}
