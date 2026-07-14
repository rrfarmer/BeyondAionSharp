using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.LegionDominion;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/LegionDominionDAO (@author Yeats). JDBC DAO over legion_dominion_locations + legion_dominion_participants. MIXED:
/// loadOrCreate via DatabaseFactory (nested insert per missing location), update/store via DB.InsertUpdate(IUStH->nested), loadParticipants
/// via DB.Select(ParamReadStH->nested), delete via low-level DB.PrepareStatement+ExecuteUpdateAndClose. Map.get->indexer; Map.put->indexer;
/// TreeMap->SortedDictionary; list.remove((Integer)x)->List.Remove(x) (by value). Nullable Timestamp values use SQL epoch projections
/// and FROM_UNIXTIME writes (Location/Info use DateTimeOffset?). StoreNewInfo consumed by LegionDominionLocation.
/// </summary>
public class LegionDominionDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(LegionDominionDAO));

    private const string UPDATE_LOC = "UPDATE legion_dominion_locations SET legion_id=?, occupied_date=FROM_UNIXTIME(? / 1000.0) WHERE id=?";
    private const string LOAD1 = "SELECT *, CAST(FLOOR(UNIX_TIMESTAMP(`occupied_date`) * 1000) AS SIGNED) AS `occupied_date_epoch_millis` FROM `legion_dominion_locations`";
    private const string LOAD2 = "SELECT *, CAST(FLOOR(UNIX_TIMESTAMP(`participated_date`) * 1000) AS SIGNED) AS `participated_date_epoch_millis` FROM `legion_dominion_participants` WHERE `legion_dominion_id`=? ";
    private const string INSERT_NEW_LOCATION = "INSERT INTO legion_dominion_locations(`id`,`legion_id`) VALUES(?,?)";
    private const string INSERT_NEW = "INSERT INTO legion_dominion_participants(`legion_dominion_id`, `legion_id`) VALUES (?, ?)";
    private const string UPDATE_PARTICIPANT = "UPDATE legion_dominion_participants SET points=?, survived_time=?, participated_date=FROM_UNIXTIME(? / 1000.0) WHERE legion_id=?";
    private const string DELETE_INFO = "DELETE FROM legion_dominion_participants WHERE legion_id=?";

    public static bool LoadOrCreateLegionDominionLocations(IDictionary<int, LegionDominionLocation> locations)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand ps = con.CreateCommand();
            ps.CommandText = LOAD1;
            List<int> nonExistingLocations = new List<int>(locations.Keys);
            using (MySqlDataReader rs = ps.ExecuteReader())
            {
                while (rs.Read())
                {
                    LegionDominionLocation loc = locations[rs.GetInt32(rs.GetOrdinal("id"))];
                    loc.SetLegionId(rs.GetInt32(rs.GetOrdinal("legion_id")));
                    loc.SetOccupiedDate(DatabaseTimestamp.ReadNullableDateTimeOffset(rs, "occupied_date_epoch_millis"));
                    nonExistingLocations.Remove(loc.GetLocationId());
                }
            }

            foreach (int locationId in nonExistingLocations)
            {
                using MySqlCommand insertPs = con.CreateCommand();
                insertPs.CommandText = INSERT_NEW_LOCATION;
                insertPs.Parameters.Add(new MySqlParameter { Value = locationId });
                insertPs.Parameters.Add(new MySqlParameter { Value = 0 });
                insertPs.ExecuteNonQuery();
            }
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Error loading Legion Dominion location from Database");
            return false;
        }
    }

    public static void UpdateLegionDominionLocation(LegionDominionLocation loc)
    {
        DB.InsertUpdate(UPDATE_LOC, new UpdateLocHandler(loc));
    }

    private sealed class UpdateLocHandler : IUStH
    {
        private readonly LegionDominionLocation loc;

        internal UpdateLocHandler(LegionDominionLocation loc)
        {
            this.loc = loc;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = loc.GetLegionId() });
            stmt.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(loc.GetOccupiedDate()) });
            stmt.Parameters.Add(new MySqlParameter { Value = loc.GetLocationId() });
            stmt.ExecuteNonQuery();
        }
    }

    public static IDictionary<int, LegionDominionParticipantInfo> LoadParticipants(LegionDominionLocation loc)
    {
        SortedDictionary<int, LegionDominionParticipantInfo> info = new SortedDictionary<int, LegionDominionParticipantInfo>();
        DB.Select(LOAD2, new LoadParticipantsHandler(loc, info));
        return info;
    }

    private sealed class LoadParticipantsHandler : ParamReadStH
    {
        private readonly LegionDominionLocation loc;
        private readonly SortedDictionary<int, LegionDominionParticipantInfo> info;

        internal LoadParticipantsHandler(LegionDominionLocation loc, SortedDictionary<int, LegionDominionParticipantInfo> info)
        {
            this.loc = loc;
            this.info = info;
        }

        public void HandleRead(MySqlDataReader rset)
        {
            while (rset.Read())
            {
                LegionDominionParticipantInfo info2 = new LegionDominionParticipantInfo();
                int legionId = rset.GetInt32(rset.GetOrdinal("legion_id"));
                info2.SetLegionId(legionId);
                info2.SetPoints(rset.GetInt32(rset.GetOrdinal("points")));
                info2.SetTime(rset.GetInt32(rset.GetOrdinal("survived_time")));
                info2.SetDate(DatabaseTimestamp.ReadNullableDateTimeOffset(rset, "participated_date_epoch_millis"));
                if (!info.ContainsKey(legionId))
                {
                    info[legionId] = info2;
                }
            }
        }

        public void SetParams(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = loc.GetLocationId() });
        }
    }

    public static void StoreNewInfo(int id, LegionDominionParticipantInfo info)
    {
        DB.InsertUpdate(INSERT_NEW, new StoreNewHandler(id, info));
    }

    private sealed class StoreNewHandler : IUStH
    {
        private readonly int id;
        private readonly LegionDominionParticipantInfo info;

        internal StoreNewHandler(int id, LegionDominionParticipantInfo info)
        {
            this.id = id;
            this.info = info;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = id });
            stmt.Parameters.Add(new MySqlParameter { Value = info.GetLegionId() });
            stmt.ExecuteNonQuery();
        }
    }

    public static void UpdateInfo(LegionDominionParticipantInfo info)
    {
        DB.InsertUpdate(UPDATE_PARTICIPANT, new UpdateInfoHandler(info));
    }

    private sealed class UpdateInfoHandler : IUStH
    {
        private readonly LegionDominionParticipantInfo info;

        internal UpdateInfoHandler(LegionDominionParticipantInfo info)
        {
            this.info = info;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = info.GetPoints() });
            stmt.Parameters.Add(new MySqlParameter { Value = info.GetTime() });
            stmt.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMillisecondsOrDbNull(info.GetDateAsTimeStamp()) });
            stmt.Parameters.Add(new MySqlParameter { Value = info.GetLegionId() });
            stmt.ExecuteNonQuery();
        }
    }

    public static void Delete(LegionDominionParticipantInfo info)
    {
        MySqlCommand statement = DB.PrepareStatement(DELETE_INFO);
        try
        {
            statement.Parameters.Add(new MySqlParameter { Value = info.GetLegionId() });
        }
        catch (Exception e)
        {
            log.LogError(e, "Deleting ParticipantInfo");
        }
        DB.ExecuteUpdateAndClose(statement);
    }
}
