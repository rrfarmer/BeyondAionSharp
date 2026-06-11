using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.SkillEngine.Model;
using ForceType = Aion.GameServer.SkillEngine.Model.Effect.ForceType;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerEffectsDAO (@author ATracer). JDBC DAO over player_effects (abnormal effects saved across logout). MIXED:
/// load/delete via the commons DB callback helper (DB.Select(ParamReadStH)/DB.InsertUpdate(IUStH), anonymous->nested), store via
/// DatabaseFactory batch. Predicate&lt;Effect&gt; -> Func&lt;Effect,bool&gt; (LINQ Where). Effect.ForceType nested value-class -> ForceType
/// alias; ForceType.getInstance(str)->GetInstance, getName()->GetName; nullable forceType preserved. setString(null)->DBNull.Value;
/// (int) getRemainingTimeMillis cast kept. setAutoCommit(false)+addBatch/executeBatch+commit->MySqlTransaction+MySqlBatch+Commit. SQL verbatim.
/// </summary>
public class PlayerEffectsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerEffectsDAO));

    public const string INSERT_QUERY = "INSERT INTO `player_effects` (`player_id`, `skill_id`, `skill_lvl`, `remaining_time`, `end_time`, `force_type`) VALUES (?,?,?,?,?,?)";
    public const string DELETE_QUERY = "DELETE FROM `player_effects` WHERE `player_id`=?";
    public const string SELECT_QUERY = "SELECT `skill_id`, `skill_lvl`, `remaining_time`, `end_time`,`force_type` FROM `player_effects` WHERE `player_id`=?";

    private static readonly Func<Effect, bool> insertableEffectsPredicate = effect => effect.CanSaveOnLogout() && effect.GetRemainingTimeMillis() > 28000;

    public static void LoadPlayerEffects(Player player)
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
                int skillId = rset.GetInt32(rset.GetOrdinal("skill_id"));
                int skillLvl = rset.GetInt32(rset.GetOrdinal("skill_lvl"));
                int remainingTime = rset.GetInt32(rset.GetOrdinal("remaining_time"));
                long endTime = rset.GetInt64(rset.GetOrdinal("end_time"));
                int ftOrd = rset.GetOrdinal("force_type");
                string forceTypeStr = rset.IsDBNull(ftOrd) ? null : rset.GetString(ftOrd);
                ForceType forceType = forceTypeStr == null ? null : ForceType.GetInstance(forceTypeStr);

                if (remainingTime > 0)
                    player.GetEffectController().AddSavedEffect(skillId, skillLvl, remainingTime, endTime, forceType);
            }
        }
    }

    public static void StorePlayerEffects(Player player)
    {
        DeletePlayerEffects(player);

        List<Effect> effects = player.GetEffectController().GetAbnormalEffects();
        effects = effects.Where(insertableEffectsPredicate).ToList();

        if (effects.Count == 0)
            return;

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlTransaction transaction = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, transaction);

            foreach (Effect effect in effects)
            {
                MySqlBatchCommand ps = new MySqlBatchCommand(INSERT_QUERY);
                ps.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
                ps.Parameters.Add(new MySqlParameter { Value = effect.GetSkillId() });
                ps.Parameters.Add(new MySqlParameter { Value = effect.GetSkillLevel() });
                ps.Parameters.Add(new MySqlParameter { Value = (int)effect.GetRemainingTimeMillis() });
                ps.Parameters.Add(new MySqlParameter { Value = effect.GetEndTime() });
                ps.Parameters.Add(new MySqlParameter { Value = (object)effect.GetForceType()?.GetName() ?? DBNull.Value });
                batch.BatchCommands.Add(ps);
            }

            batch.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Exception while saving effects of player " + player.GetObjectId());
        }
    }

    private static void DeletePlayerEffects(Player player)
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
