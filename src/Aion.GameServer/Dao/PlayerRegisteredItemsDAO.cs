using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils.Idfactory;
using Aion.GameServer.World;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerRegisteredItemsDAO (@author Rolandas). JDBC DAO over player_registered_items (house objects + decorations).
/// DatabaseFactory-style. Java wildcard HouseObject&lt;?&gt; -> HouseObject&lt;PlaceableHouseObject&gt; (the concrete type HouseRegistry/
/// HouseObjectFactory use). getUsedIDs scrollable->forward-only List+ToArray. stream().filter(Persistable.NEW/CHANGED/DELETED)->
/// Where(IPersistable.*) red predicates; instanceof+cast->is X; setObject(x, Types.INTEGER) null-supporting + setNull->DBNull.Value;
/// rset.getObject("color")(Integer)->IsDBNull?null:GetInt32 (SetColor int?); store helpers per-helper MySqlTransaction+MySqlBatch+Commit;
/// IllegalAccessException->Exception. World.GetInstance().FindVisibleObject, HouseObjectFactory.CreateNew, IDFactory.GetInstance().
/// ReleaseObjectIds, and registry/obj accessors are tolerated red refs. SQL verbatim.
/// </summary>
public class PlayerRegisteredItemsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerRegisteredItemsDAO));

    public const string CLEAN_PLAYER_QUERY = "DELETE FROM `player_registered_items` WHERE `player_id` = ?";
    public const string SELECT_QUERY = "SELECT * FROM `player_registered_items` WHERE `player_id`=?";
    public const string INSERT_QUERY = "INSERT INTO `player_registered_items` "
        + "(`expire_time`,`color`,`color_expires`,`owner_use_count`,`visitor_use_count`,`x`,`y`,`z`,`h`,`area`,`room`,`player_id`,`item_unique_id`,`item_id`) VALUES "
        + "(?,?,?,?,?,?,?,?,?,?,?,?,?,?)";
    public const string UPDATE_QUERY = "UPDATE `player_registered_items` SET "
        + "`expire_time`=?,`color`=?,`color_expires`=?,`owner_use_count`=?,`visitor_use_count`=?,`x`=?,`y`=?,`z`=?,`h`=?,`area`=?,`room`=? "
        + "WHERE `player_id`=? AND `item_unique_id`=? AND `item_id`=?";
    public const string DELETE_QUERY = "DELETE FROM `player_registered_items` WHERE `item_unique_id` = ?";
    public const string RESET_QUERY = "UPDATE `player_registered_items` SET x=0,y=0,z=0,h=0,area='NONE' WHERE `player_id`=? AND `area` != 'DECOR'";

    public static int[] GetUsedIDs()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT item_unique_id FROM player_registered_items WHERE item_unique_id <> 0";
            using MySqlDataReader rs = stmt.ExecuteReader();
            List<int> ids = new List<int>();
            while (rs.Read())
                ids.Add(rs.GetInt32(rs.GetOrdinal("item_unique_id")));
            return ids.ToArray();
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't get list of IDs from player_registered_items table");
            return null;
        }
    }

    public static void LoadRegistry(HouseRegistry registry)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = registry.GetOwner().GetOwnerId() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                string area = rset.GetString(rset.GetOrdinal("area"));
                if ("DECOR".Equals(area))
                {
                    registry.PutDecor(CreateDecoration(registry, rset), false);
                }
                else
                {
                    registry.PutObject(ConstructObject(registry, rset), false);
                }
            }
            bool hasInvalidDecors = registry.GetDecors().Any(decor => decor.GetPersistentState() == IPersistable.PersistentState.DELETED);
            registry.SetPersistentState(hasInvalidDecors ? IPersistable.PersistentState.UPDATE_REQUIRED : IPersistable.PersistentState.UPDATED);
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load house registry data for player " + registry.GetOwner().GetOwnerId());
        }
    }

    private static HouseObject<PlaceableHouseObject> ConstructObject(HouseRegistry registry, MySqlDataReader rset)
    {
        int itemUniqueId = rset.GetInt32(rset.GetOrdinal("item_unique_id"));
        VisibleObject visObj = World.GetInstance().FindVisibleObject(itemUniqueId);
        HouseObject<PlaceableHouseObject> obj;
        if (visObj != null)
        {
            if (visObj is HouseObject<PlaceableHouseObject> ho)
                obj = ho;
            else
            {
                throw new Exception("Someone stole my house object id : " + itemUniqueId);
            }
        }
        else
        {
            obj = registry.GetObjectByObjId(itemUniqueId);
            if (obj == null)
                obj = HouseObjectFactory.CreateNew(registry, itemUniqueId, rset.GetInt32(rset.GetOrdinal("item_id")));
        }
        obj.SetOwnerUsedCount(rset.GetInt32(rset.GetOrdinal("owner_use_count")));
        obj.SetVisitorUsedCount(rset.GetInt32(rset.GetOrdinal("visitor_use_count")));
        obj.SetX(rset.GetFloat(rset.GetOrdinal("x")));
        obj.SetY(rset.GetFloat(rset.GetOrdinal("y")));
        obj.SetZ(rset.GetFloat(rset.GetOrdinal("z")));
        obj.SetHeading((byte)rset.GetInt32(rset.GetOrdinal("h")));
        int colorOrd = rset.GetOrdinal("color");
        obj.SetColor(rset.IsDBNull(colorOrd) ? (int?)null : rset.GetInt32(colorOrd));
        obj.SetColorExpireEnd(rset.GetInt32(rset.GetOrdinal("color_expires")));
        if (obj.GetObjectTemplate().GetUseDays() > 0)
            obj.SetExpireTime(rset.GetInt32(rset.GetOrdinal("expire_time")));
        obj.SetPersistentState(IPersistable.PersistentState.UPDATED);
        return obj;
    }

    private static HouseDecoration CreateDecoration(HouseRegistry registry, MySqlDataReader rset)
    {
        int itemUniqueId = rset.GetInt32(rset.GetOrdinal("item_unique_id"));
        int itemId = rset.GetInt32(rset.GetOrdinal("item_id"));
        int room = rset.GetByte(rset.GetOrdinal("room"));
        HouseDecoration decor = new HouseDecoration(itemUniqueId, itemId, room);

        if (!decor.GetTemplate().IsForBuilding(registry.GetOwner().GetBuilding())
            || decor.GetRoom() > 0 && registry.GetOwner().GetHouseType() != HouseType.PALACE)
            decor.SetPersistentState(IPersistable.PersistentState.DELETED);
        else
            decor.SetPersistentState(IPersistable.PersistentState.UPDATED);

        return decor;
    }

    public static bool Store(HouseRegistry registry, int playerId)
    {
        List<HouseObject<PlaceableHouseObject>> objects = registry.GetObjects();
        List<HouseDecoration> decors = registry.GetDecors();
        List<HouseObject<PlaceableHouseObject>> objectsToAdd = objects.Where(IPersistable.NEW).ToList();
        List<HouseObject<PlaceableHouseObject>> objectsToUpdate = objects.Where(IPersistable.CHANGED).ToList();
        List<HouseObject<PlaceableHouseObject>> objectsToDelete = objects.Where(IPersistable.DELETED).ToList();
        List<HouseDecoration> decorsToAdd = decors.Where(IPersistable.NEW).ToList();
        List<HouseDecoration> decorsToUpdate = decors.Where(IPersistable.CHANGED).ToList();
        List<HouseDecoration> decorsToDelete = decors.Where(IPersistable.DELETED).ToList();

        bool objectDeleteResult = false;
        bool decorsDeleteResult = false;

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            objectDeleteResult = DeleteObjects(con, objectsToDelete);
            decorsDeleteResult = DeleteDecors(con, decorsToDelete);
            StoreObjects(con, objectsToUpdate, playerId, false);
            StoreDecors(con, decorsToUpdate, playerId, false);
            StoreObjects(con, objectsToAdd, playerId, true);
            StoreDecors(con, decorsToAdd, playerId, true);
            registry.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't save registered items for player: " + playerId);
        }

        foreach (HouseObject<PlaceableHouseObject> obj in objects)
        {
            if (obj.GetPersistentState() == IPersistable.PersistentState.DELETED)
                registry.DiscardObject(obj, true);
            else
                obj.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }

        foreach (HouseDecoration decor in decors)
        {
            if (decor.GetPersistentState() == IPersistable.PersistentState.DELETED)
                registry.DiscardDecor(decor, true);
            else
                decor.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }

        if (objectDeleteResult)
            IDFactory.GetInstance().ReleaseObjectIds(objectsToDelete);

        if (decorsDeleteResult)
            IDFactory.GetInstance().ReleaseObjectIds(decorsToDelete);

        return true;
    }

    private static bool StoreObjects(MySqlConnection con, ICollection<HouseObject<PlaceableHouseObject>> objects, int playerId, bool isNew)
    {
        if (objects.Count == 0)
            return true;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (HouseObject<PlaceableHouseObject> obj in objects)
            {
                MySqlBatchCommand stmt = new MySqlBatchCommand(isNew ? INSERT_QUERY : UPDATE_QUERY);
                stmt.Parameters.Add(new MySqlParameter { Value = (object)(obj.GetExpireTime() > 0 ? (int?)obj.GetExpireTime() : null) ?? DBNull.Value });
                stmt.Parameters.Add(new MySqlParameter { Value = (object)obj.GetColor() ?? DBNull.Value });
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetColorExpireEnd() });
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetOwnerUsedCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetVisitorUsedCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetX() });
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetY() });
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetZ() });
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetHeading() });
                if (obj.GetX() > 0 || obj.GetY() > 0 || obj.GetZ() > 0)
                    stmt.Parameters.Add(new MySqlParameter { Value = obj.GetPlaceArea().ToString() });
                else
                    stmt.Parameters.Add(new MySqlParameter { Value = "NONE" });
                stmt.Parameters.Add(new MySqlParameter { Value = (byte)0 });
                stmt.Parameters.Add(new MySqlParameter { Value = playerId });
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetObjectId() });
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetObjectTemplate().GetTemplateId() });
                batch.BatchCommands.Add(stmt);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to execute house object update batch");
            return false;
        }
        return true;
    }

    private static bool StoreDecors(MySqlConnection con, ICollection<HouseDecoration> decors, int playerId, bool isNew)
    {
        if (decors.Count == 0)
            return true;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (HouseDecoration decor in decors)
            {
                MySqlBatchCommand stmt = new MySqlBatchCommand(isNew ? INSERT_QUERY : UPDATE_QUERY);
                stmt.Parameters.Add(new MySqlParameter { Value = DBNull.Value });
                stmt.Parameters.Add(new MySqlParameter { Value = DBNull.Value });
                stmt.Parameters.Add(new MySqlParameter { Value = 0 });
                stmt.Parameters.Add(new MySqlParameter { Value = 0 });
                stmt.Parameters.Add(new MySqlParameter { Value = 0 });
                stmt.Parameters.Add(new MySqlParameter { Value = 0f });
                stmt.Parameters.Add(new MySqlParameter { Value = 0f });
                stmt.Parameters.Add(new MySqlParameter { Value = 0f });
                stmt.Parameters.Add(new MySqlParameter { Value = 0 });
                stmt.Parameters.Add(new MySqlParameter { Value = "DECOR" });
                stmt.Parameters.Add(new MySqlParameter { Value = decor.GetRoom() });
                stmt.Parameters.Add(new MySqlParameter { Value = playerId });
                stmt.Parameters.Add(new MySqlParameter { Value = decor.GetObjectId() });
                stmt.Parameters.Add(new MySqlParameter { Value = decor.GetTemplateId() });
                batch.BatchCommands.Add(stmt);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to execute house decor update batch");
            return false;
        }
        return true;
    }

    private static bool DeleteObjects(MySqlConnection con, ICollection<HouseObject<PlaceableHouseObject>> objects)
    {
        if (objects.Count == 0)
            return true;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (HouseObject<PlaceableHouseObject> obj in objects)
            {
                MySqlBatchCommand stmt = new MySqlBatchCommand(DELETE_QUERY);
                stmt.Parameters.Add(new MySqlParameter { Value = obj.GetObjectId() });
                batch.BatchCommands.Add(stmt);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to execute delete batch");
            return false;
        }
        return true;
    }

    private static bool DeleteDecors(MySqlConnection con, ICollection<HouseDecoration> decors)
    {
        if (decors.Count == 0)
            return true;

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (HouseDecoration decor in decors)
            {
                MySqlBatchCommand stmt = new MySqlBatchCommand(DELETE_QUERY);
                stmt.Parameters.Add(new MySqlParameter { Value = decor.GetObjectId() });
                batch.BatchCommands.Add(stmt);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to execute delete batch");
            return false;
        }
        return true;
    }

    public static bool DeletePlayerItems(int playerId)
    {
        log.LogInformation("Deleting player items");
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = CLEAN_PLAYER_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error in deleting all player registered items. PlayerObjId: " + playerId);
            return false;
        }
        return true;
    }

    public static void ResetRegistry(int playerId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = RESET_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error in resetting player registered items. PlayerObjId: " + playerId);
        }
    }
}
