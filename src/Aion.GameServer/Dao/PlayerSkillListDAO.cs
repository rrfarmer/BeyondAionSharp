using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerSkillListDAO (@author SoulKeeper, IceReaper, orfeo087, Avol, AEJTester). JDBC DAO over player_skills.
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; SQL verbatim. The store path mirrors
/// Java: one shared connection with con.setAutoCommit(false) -> the helpers run their add/update/delete batches under an explicit
/// MySqlTransaction (one per helper, Commit each, matching Java's three con.commit() calls). addBatch/executeBatch -> MySqlBatch.
/// stream().filter(Persistable.NEW/CHANGED/DELETED) -> Where(IPersistable.NEW/CHANGED/DELETED) (Predicate&lt;Persistable&gt; statics;
/// contravariant Func&lt;IPersistable,bool&gt; - these predicate statics aren't yet on the reworked IPersistable, so they're tolerated
/// red refs until ported). PlayerSkillEntry(id, lv, 0, UPDATED); PlayerSkillList(list). PersistentState set UPDATED after store.
/// </summary>
public class PlayerSkillListDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerSkillListDAO));

    public const string INSERT_QUERY = "REPLACE INTO `player_skills` (`player_id`, `skill_id`, `skill_level`) VALUES (?, ?, ?)";
    public const string UPDATE_QUERY = "UPDATE `player_skills` set skill_level=? where player_id=? AND skill_id=?";
    public const string DELETE_QUERY = "DELETE FROM `player_skills` WHERE `player_id`=? AND skill_id=?";
    public const string SELECT_QUERY = "SELECT `skill_id`, `skill_level` FROM `player_skills` WHERE `player_id`=?";

    public static PlayerSkillList LoadSkillList(int playerId)
    {
        List<PlayerSkillEntry> skills = new List<PlayerSkillEntry>();
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
                int id = rset.GetInt32(rset.GetOrdinal("skill_id"));
                int lv = rset.GetInt32(rset.GetOrdinal("skill_level"));
                skills.Add(new PlayerSkillEntry(id, lv, 0, IPersistable.PersistentState.UPDATED));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore SkillList data for player: " + playerId + " from DB: " + e.Message);
        }
        return new PlayerSkillList(skills);
    }

    /// <summary>Stores all player skills according to their persistence state</summary>
    public static bool StoreSkills(Player player)
    {
        Store(player, player.GetSkillList().GetDeletedSkills());
        Store(player, player.GetSkillList().GetAllSkills());

        return true;
    }

    private static void Store(Player player, List<PlayerSkillEntry> skills)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();

            DeleteSkills(con, player, skills);
            AddSkills(con, player, skills);
            UpdateSkills(con, player, skills);
        }
        catch (Exception)
        {
            log.LogError("Failed to open connection to database while saving SkillList for player " + player.GetObjectId());
        }

        foreach (PlayerSkillEntry skill in skills)
        {
            skill.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
    }

    private static void AddSkills(MySqlConnection con, Player player, List<PlayerSkillEntry> skills)
    {
        skills = skills.Where(x => IPersistable.NEW(x)).ToList();
        if (skills.Count == 0)
            return;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (PlayerSkillEntry skill in skills)
            {
                MySqlBatchCommand cmd = new MySqlBatchCommand(INSERT_QUERY);
                cmd.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
                cmd.Parameters.Add(new MySqlParameter { Value = skill.GetSkillId() });
                cmd.Parameters.Add(new MySqlParameter { Value = skill.GetSkillLevel() });
                batch.BatchCommands.Add(cmd);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't add skills for player: " + player.GetObjectId());
        }
    }

    private static void UpdateSkills(MySqlConnection con, Player player, List<PlayerSkillEntry> skills)
    {
        skills = skills.Where(x => IPersistable.CHANGED(x)).ToList();
        if (skills.Count == 0)
            return;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (PlayerSkillEntry skill in skills)
            {
                MySqlBatchCommand cmd = new MySqlBatchCommand(UPDATE_QUERY);
                cmd.Parameters.Add(new MySqlParameter { Value = skill.GetSkillLevel() });
                cmd.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
                cmd.Parameters.Add(new MySqlParameter { Value = skill.GetSkillId() });
                batch.BatchCommands.Add(cmd);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception)
        {
            log.LogError("Can't update skills for player: " + player.GetObjectId());
        }
    }

    private static void DeleteSkills(MySqlConnection con, Player player, List<PlayerSkillEntry> skills)
    {
        skills = skills.Where(x => IPersistable.DELETED(x)).ToList();
        if (skills.Count == 0)
            return;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (PlayerSkillEntry skill in skills)
            {
                MySqlBatchCommand cmd = new MySqlBatchCommand(DELETE_QUERY);
                cmd.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
                cmd.Parameters.Add(new MySqlParameter { Value = skill.GetSkillId() });
                batch.BatchCommands.Add(cmd);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception)
        {
            log.LogError("Can't delete skills for player: " + player.GetObjectId());
        }
    }
}
