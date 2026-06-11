using System;
using System.Collections.Generic;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/HeadhuntingDAO (@author Estrayl). JDBC DAO over headhunting via the commons DB callback helper. Anonymous
/// ParamReadStH/IUStH -> private nested classes capturing locals via ctor. TreeMap -> SortedDictionary (sorted by hunter id, as Java).
/// rset.next()/getInt->Read()/GetInt32(GetOrdinal); getTimestamp("last_update").getTime()->new DateTimeOffset(GetDateTime).ToUnixTimeMilliseconds();
/// new Timestamp(millis)->DateTimeOffset.FromUnixTimeMilliseconds(millis).LocalDateTime. Persistable.PersistentState->IPersistable.PersistentState.
/// PvpService.GetInstance().GetHeadhunter red-tolerated if absent. SQL verbatim.
/// </summary>
public class HeadhuntingDAO
{
    private const string SELECT_QUERY = "SELECT * FROM `headhunting`";
    private const string UPDATE_QUERY = "REPLACE INTO `headhunting` (`hunter_id`, `accumulated_kills`, `last_update`) VALUES (?,?,?)";
    private const string DELETE_QUERY = "DELETE FROM `headhunting`";

    public static IDictionary<int, Headhunter> LoadHeadhunters()
    {
        SortedDictionary<int, Headhunter> loadedHunters = new SortedDictionary<int, Headhunter>();
        DB.Select(SELECT_QUERY, new LoadHandler(loadedHunters));

        return loadedHunters;
    }

    private sealed class LoadHandler : ParamReadStH
    {
        private readonly SortedDictionary<int, Headhunter> loadedHunters;

        internal LoadHandler(SortedDictionary<int, Headhunter> loadedHunters)
        {
            this.loadedHunters = loadedHunters;
        }

        public void HandleRead(MySqlDataReader rset)
        {
            while (rset.Read())
            {
                int playerId = rset.GetInt32(rset.GetOrdinal("hunter_id"));
                if (!loadedHunters.ContainsKey(playerId))
                {
                    int accumulatedKills = rset.GetInt32(rset.GetOrdinal("accumulated_kills"));
                    long lastUpdate = new DateTimeOffset(rset.GetDateTime(rset.GetOrdinal("last_update"))).ToUnixTimeMilliseconds();
                    loadedHunters[playerId] = new Headhunter(playerId, accumulatedKills, lastUpdate, IPersistable.PersistentState.UPDATED);
                }
            }
        }

        public void SetParams(MySqlCommand stmt)
        {
        }
    }

    public static bool ClearTables()
    {
        return DB.InsertUpdate(DELETE_QUERY, new ClearHandler());
    }

    private sealed class ClearHandler : IUStH
    {
        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.ExecuteNonQuery();
        }
    }

    public static void StoreHeadhunter(int hunterId)
    {
        Headhunter hunter = PvpService.GetInstance().GetHeadhunter(hunterId);
        if (hunter == null || hunter.GetPersistentState() != IPersistable.PersistentState.UPDATE_REQUIRED)
            return;

        bool success = DB.InsertUpdate(UPDATE_QUERY, new StoreHandler(hunter));

        if (success)
            hunter.SetPersistentState(IPersistable.PersistentState.UPDATED);
    }

    private sealed class StoreHandler : IUStH
    {
        private readonly Headhunter hunter;

        internal StoreHandler(Headhunter hunter)
        {
            this.hunter = hunter;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = hunter.GetHunterId() });
            stmt.Parameters.Add(new MySqlParameter { Value = hunter.GetKills() });
            stmt.Parameters.Add(new MySqlParameter { Value = DateTimeOffset.FromUnixTimeMilliseconds(hunter.GetLastUpdate()).LocalDateTime });
            stmt.ExecuteNonQuery();
        }
    }
}
