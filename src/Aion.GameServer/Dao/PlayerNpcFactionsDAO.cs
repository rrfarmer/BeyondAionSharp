using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Npcfaction;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerNpcFactionsDAO (@author MrPoke). JDBC DAO over player_npc_factions (per-player NPC-faction quest progress).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; rset.getInt/getBoolean/getString("col")
/// -> reader.GetX(reader.GetOrdinal("col")); execute -> ExecuteNonQuery; SQL verbatim. ENpcFactionQuestState.valueOf(name) ->
/// Enum.Parse&lt;ENpcFactionQuestState&gt;(name); state.name() -> GetState().ToString(); Persistable.PersistentState ->
/// IPersistable.PersistentState (NEW/UPDATE_REQUIRED switch, set UPDATED). Npcfaction namespace casing.
/// </summary>
public class PlayerNpcFactionsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerNpcFactionsDAO));

    public const string SELECT_QUERY = "SELECT `faction_id`, `active`, `time`, `state`, `quest_id` FROM player_npc_factions WHERE `player_id`=?";
    public const string INSERT_QUERY = "INSERT INTO player_npc_factions (`player_id`, `faction_id`, `active`, `time`, `state`, `quest_id`) VALUES (?,?,?,?,?,?)";
    public const string UPDATE_QUERY = "UPDATE player_npc_factions SET `active`=?, `time`=?, `state`=?, `quest_id`=?  WHERE `player_id`=? AND `faction_id`=?";

    public static void LoadNpcFactions(Player player)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            NpcFactions factions = new NpcFactions(player);
            player.SetNpcFactions(factions);
            while (rset.Read())
            {
                int faction_id = rset.GetInt32(rset.GetOrdinal("faction_id"));
                bool active = rset.GetBoolean(rset.GetOrdinal("active"));
                int time = rset.GetInt32(rset.GetOrdinal("time"));
                int questId = rset.GetInt32(rset.GetOrdinal("quest_id"));
                ENpcFactionQuestState state = Enum.Parse<ENpcFactionQuestState>(rset.GetString(rset.GetOrdinal("state")));
                NpcFaction faction = new NpcFaction(faction_id, time, active, state, questId);
                faction.SetPersistentState(IPersistable.PersistentState.UPDATED);
                factions.AddNpcFaction(faction);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore Npc faction data for playerObjId: " + player.GetObjectId() + " from DB: " + e.Message);
        }
    }

    public static void StoreNpcFactions(Player player)
    {
        foreach (NpcFaction npcFaction in player.GetNpcFactions().GetNpcFactions())
        {
            switch (npcFaction.GetPersistentState())
            {
                case IPersistable.PersistentState.NEW:
                    InsertNpcFaction(player.GetObjectId(), npcFaction);
                    break;
                case IPersistable.PersistentState.UPDATE_REQUIRED:
                    UpdateNpcFaction(player.GetObjectId(), npcFaction);
                    break;
            }
        }
    }

    private static void InsertNpcFaction(int playerObjectId, NpcFaction faction)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = INSERT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjectId });
            stmt.Parameters.Add(new MySqlParameter { Value = faction.GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = faction.IsActive() });
            stmt.Parameters.Add(new MySqlParameter { Value = faction.GetTime() });
            stmt.Parameters.Add(new MySqlParameter { Value = faction.GetState().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = faction.GetQuestId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not insert Npc faction data for playerObjId: " + playerObjectId + " from DB: " + e.Message);
        }
    }

    private static void UpdateNpcFaction(int playerObjectId, NpcFaction faction)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = faction.IsActive() });
            stmt.Parameters.Add(new MySqlParameter { Value = faction.GetTime() });
            stmt.Parameters.Add(new MySqlParameter { Value = faction.GetState().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = faction.GetQuestId() });
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjectId });
            stmt.Parameters.Add(new MySqlParameter { Value = faction.GetId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not update Npc faction data for playerObjId: " + playerObjectId + " from DB: " + e.Message);
        }
    }
}
