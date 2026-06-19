using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/HousesDAO (@author Rolandas). JDBC DAO over the houses table. DatabaseFactory-style; positional '?' -> ordered
/// MySqlParameter; executeQuery -> ExecuteReader; getInt/getString->GetX(GetOrdinal); execute->ExecuteNonQuery; SQL verbatim. getUsedIDs
/// scrollable ResultSet (TYPE_SCROLL_INSENSITIVE/last/getRow/beforeFirst -> pre-size int[]) -> forward-only List&lt;int&gt;+ToArray (scroll
/// flags dropped). nullable getTimestamp (acquire_time/next_pay) -> IsDBNull?null:new DateTimeOffset(GetDateTime) (House uses DateTimeOffset?);
/// setTimestamp(null)->DBNull.Value. Map.get/put->indexer, containsKey->ContainsKey; building.getType()->GetType_(); Persistable.PersistentState
/// ->IPersistable.PersistentState (NEW/UPDATE_REQUIRED). House(houseId, building, address, 0).
/// </summary>
public class HousesDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(HousesDAO));

    private const string SELECT_HOUSES_QUERY = "SELECT * FROM houses WHERE address <> 2001 AND address <> 3001";
    private const string SELECT_STUDIOS_QUERY = "SELECT * FROM houses WHERE address = 2001 OR address = 3001";

    private const string ADD_HOUSE_QUERY = "INSERT INTO houses (id, address, building_id, player_id, acquire_time, settings, next_pay, sign_notice) "
        + " VALUES (?,?,?,?,?,?,?,?)";

    private const string UPDATE_HOUSE_QUERY = "UPDATE houses SET building_id=?, player_id=?, acquire_time=?, settings=?, next_pay=?, sign_notice=? WHERE id=?";
    private const string DELETE_HOUSE_QUERY = "DELETE FROM houses WHERE player_id=?";

    public static int[] GetUsedIDs()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT DISTINCT id FROM houses";
            using MySqlDataReader rs = stmt.ExecuteReader();
            List<int> ids = new List<int>();
            while (rs.Read())
                ids.Add(rs.GetInt32(rs.GetOrdinal("id")));
            return ids.ToArray();
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't get list of IDs from houses table");
            return null;
        }
    }

    public static void StoreHouse(House house)
    {
        if (house.GetPersistentState() == IPersistable.PersistentState.NEW)
            InsertNewHouse(house);
        else if (house.GetPersistentState() == IPersistable.PersistentState.UPDATE_REQUIRED)
            UpdateHouse(house);
    }

    private static void InsertNewHouse(House house)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = ADD_HOUSE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetAddress().GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetBuilding().GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetOwnerId() });
            stmt.Parameters.Add(new MySqlParameter { Value = (object)house.GetAcquiredTime() ?? DBNull.Value });
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetPermissionsForDB() });
            stmt.Parameters.Add(new MySqlParameter { Value = (object)house.GetNextPay() ?? DBNull.Value });
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetSignNotice() });

            stmt.ExecuteNonQuery();
            house.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not insert house " + house.GetObjectId());
        }
    }

    private static void UpdateHouse(House house)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_HOUSE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetBuilding().GetId() });
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetOwnerId() });
            stmt.Parameters.Add(new MySqlParameter { Value = (object)house.GetAcquiredTime() ?? DBNull.Value });
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetPermissionsForDB() });
            stmt.Parameters.Add(new MySqlParameter { Value = (object)house.GetNextPay() ?? DBNull.Value });
            stmt.Parameters.Add(new MySqlParameter { Value = house.GetSignNotice() });

            stmt.Parameters.Add(new MySqlParameter { Value = house.GetObjectId() });
            stmt.ExecuteNonQuery();
            house.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not store house " + house.GetObjectId());
        }
    }

    public static Dictionary<int, House> LoadHouses(ICollection<HousingLand> lands, bool studios)
    {
        Dictionary<int, House> houses = new Dictionary<int, House>();
        Dictionary<int, HouseAddress> addressesById = new Dictionary<int, HouseAddress>();
        Dictionary<int, List<Building>> buildingsForAddress = new Dictionary<int, List<Building>>();
        foreach (HousingLand land in lands)
        {
            foreach (HouseAddress address in land.GetAddresses())
            {
                addressesById[address.GetId()] = address;
                buildingsForAddress[address.GetId()] = land.GetBuildings();
            }
        }

        Dictionary<int, int> addressHouseIds = new Dictionary<int, int>();

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = studios ? SELECT_STUDIOS_QUERY : SELECT_HOUSES_QUERY;
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int houseId = rset.GetInt32(rset.GetOrdinal("id"));
                int buildingId = rset.GetInt32(rset.GetOrdinal("building_id"));
                HouseAddress address = addressesById[rset.GetInt32(rset.GetOrdinal("address"))];
                Building building = null;
                foreach (Building b in buildingsForAddress[address.GetId()])
                {
                    if (b.GetId() == buildingId)
                    {
                        building = b;
                        break;
                    }
                }

                House house;
                if (building == null)
                {
                    log.LogWarning("Missing building type for address " + address.GetId());
                    continue;
                }
                else if (addressHouseIds.ContainsKey(address.GetId()))
                {
                    log.LogWarning("Duplicate house address " + address.GetId() + "!");
                    continue;
                }
                else
                {
                    house = new House(houseId, building, address, 0);
                    if (building.GetType_() == BuildingType.PERSONAL_FIELD)
                        addressHouseIds[address.GetId()] = houseId;
                }

                house.SetOwnerId(rset.GetInt32(rset.GetOrdinal("player_id")));
                int atOrd = rset.GetOrdinal("acquire_time");
                house.SetAcquiredTime(rset.IsDBNull(atOrd) ? (DateTimeOffset?)null : new DateTimeOffset(rset.GetDateTime(atOrd)));
                house.SetPermissionsFromDB(rset.GetInt32(rset.GetOrdinal("settings")));
                int npOrd = rset.GetOrdinal("next_pay");
                house.SetNextPay(rset.IsDBNull(npOrd) ? (DateTimeOffset?)null : new DateTimeOffset(rset.GetDateTime(npOrd)));
                int snOrd = rset.GetOrdinal("sign_notice"); // sign_notice is nullable (DEFAULT NULL); Java getString tolerates null, C# GetString throws on DBNull
                house.SetSignNotice(rset.IsDBNull(snOrd) ? null : rset.GetString(snOrd));
                house.SetPersistentState(IPersistable.PersistentState.UPDATED);

                int id = studios ? house.GetOwnerId() : address.GetId();
                houses[id] = house;
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load houses");
        }
        return houses;
    }

    public static void DeleteHouse(int playerId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_HOUSE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Delete House failed");
        }
    }
}
