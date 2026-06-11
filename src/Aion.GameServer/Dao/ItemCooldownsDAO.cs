using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/ItemCooldownsDAO (@author ATracer). JDBC DAO over item_cooldowns. MIXED: load/delete use the commons DB callback
/// helper (DB.Select(ParamReadStH) / DB.InsertUpdate(IUStH)); store uses DatabaseFactory directly with a batch. Anonymous handlers ->
/// nested classes capturing locals via ctor. Inside handlers: setInt->Parameters.Add; rset.next()/getInt/getLong->Read()/GetInt32/GetInt64.
/// System.currentTimeMillis()->DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(). store: copy map, values().removeIf(stale-or-null within 30s)
/// -> filter+Remove; setAutoCommit(false)+addBatch/executeBatch+commit -> MySqlTransaction+MySqlBatch+Commit. broadCastEffects(null) kept.
/// </summary>
public class ItemCooldownsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ItemCooldownsDAO));

    public const string INSERT_QUERY = "INSERT INTO `item_cooldowns` (`player_id`, `delay_id`, `use_delay`, `reuse_time`) VALUES (?,?,?,?)";
    public const string DELETE_QUERY = "DELETE FROM `item_cooldowns` WHERE `player_id`=?";
    public const string SELECT_QUERY = "SELECT `delay_id`, `use_delay`, `reuse_time` FROM `item_cooldowns` WHERE `player_id`=?";

    public static void LoadItemCooldowns(Player player)
    {
        DB.Select(SELECT_QUERY, new LoadHandler(player));
        player.GetEffectController().BroadCastEffects(null);
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
                int delayId = rset.GetInt32(rset.GetOrdinal("delay_id"));
                int useDelay = rset.GetInt32(rset.GetOrdinal("use_delay"));
                long reuseTime = rset.GetInt64(rset.GetOrdinal("reuse_time"));

                if (reuseTime > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    player.AddItemCoolDown(delayId, reuseTime, useDelay);
            }
        }
    }

    public static void StoreItemCooldowns(Player player)
    {
        DeleteItemCooldowns(player);

        IDictionary<int, ItemCooldown> itemCoolDowns = player.GetItemCoolDowns();
        if (itemCoolDowns.Count == 0)
            return;

        itemCoolDowns = new Dictionary<int, ItemCooldown>(itemCoolDowns);
        foreach (int key in itemCoolDowns
            .Where(e => e.Value == null || e.Value.GetReuseTime() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() <= 30000)
            .Select(e => e.Key).ToList())
        {
            itemCoolDowns.Remove(key);
        }

        if (itemCoolDowns.Count == 0)
            return;

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlTransaction transaction = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, transaction);

            foreach (KeyValuePair<int, ItemCooldown> entry in itemCoolDowns)
            {
                MySqlBatchCommand st = new MySqlBatchCommand(INSERT_QUERY);
                st.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
                st.Parameters.Add(new MySqlParameter { Value = entry.Key });
                st.Parameters.Add(new MySqlParameter { Value = entry.Value.GetUseDelay() });
                st.Parameters.Add(new MySqlParameter { Value = entry.Value.GetReuseTime() });
                batch.BatchCommands.Add(st);
            }

            batch.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while storing item cooldowns for " + player);
        }
    }

    private static void DeleteItemCooldowns(Player player)
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
