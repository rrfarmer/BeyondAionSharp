using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/InventoryDAO (@author ATracer). JDBC DAO over the inventory table (all storages). DatabaseFactory-style; the
/// 28-column item INSERT/UPDATE preserved verbatim. Consumer&lt;Item&gt;->Action&lt;Item&gt; (storage::onLoadHandler->storage.OnLoadHandler,
/// items::add->items.Add). 28-arg Item ctor; itemColor (Integer) rset.getObject->IsDBNull?(int?)null:GetInt32; ItemStoneType.GODSTONE.ordinal()
/// ->(int) (plain enum); setObject(Types.INTEGER) null->DBNull.Value; getItemOwnerId returns int (Integer auto-unbox -> .Value, NPE-faithful).
/// stream().filter(Persistable.NEW/CHANGED/DELETED)->Where(IPersistable.*) red predicates; store helpers per-helper MySqlTransaction+MySqlBatch+Commit.
/// getUsedIDs scrollable->forward-only List+ToArray. **Resolves BrokerDAO's red refs (LoadBrokerItems/Store).** GenericValidator/IDFactory.
/// GetInstance()/PlayerAccountData.VisibleItem/many Item.* red-tolerated. SQL verbatim.
/// </summary>
public class InventoryDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(InventoryDAO));

    public const string SELECT_QUERY = "SELECT * FROM `inventory` WHERE `item_owner`=? AND `item_location`=?";
    public const string SELECT_ALL_QUERY = "SELECT * FROM `inventory` WHERE `item_location`=?";
    public const string SELECT_EQUIPPED_QUERY = "SELECT i.item_skin, i.slot, i.item_color, s.item_id godstone_item_id FROM inventory i LEFT JOIN item_stones s ON s.item_unique_id = i.item_unique_id AND s.slot = 0 AND s.category = ? WHERE i.item_owner = ? AND i.item_location = ? AND i.is_equipped = 1";
    public const string INSERT_QUERY = "INSERT INTO `inventory` (`item_unique_id`, `item_id`, `item_count`, `item_color`, `color_expires`, `item_creator`, `expire_time`, `activation_count`, `item_owner`, `is_equipped`, is_soul_bound, `slot`, `item_location`, `enchant`, `enchant_bonus`, `item_skin`, `fusioned_item`, `optional_socket`, `optional_fusion_socket`, `charge`, `tune_count`, `rnd_bonus`, `fusion_rnd_bonus`, `tempering`, `pack_count`, `is_amplified`, `buff_skill`, `rnd_plume_bonus`) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)";
    public const string UPDATE_QUERY = "UPDATE inventory SET item_count=?, item_color=?, color_expires=?, item_creator=?, expire_time=?, activation_count=?, item_owner=?, is_equipped=?, is_soul_bound=?, slot=?, item_location=?, enchant=?, enchant_bonus=?, item_skin=?, fusioned_item=?, optional_socket=?, optional_fusion_socket=?, charge=?, tune_count=?, rnd_bonus=?, fusion_rnd_bonus=?, tempering=?, pack_count=?, is_amplified=?, buff_skill=?, rnd_plume_bonus=? WHERE item_unique_id=?";
    public const string DELETE_QUERY = "DELETE FROM inventory WHERE item_unique_id=?";
    public const string DELETE_CLEAN_QUERY = "DELETE FROM inventory WHERE item_owner=? AND item_location != 2"; // exclude acc wh since item_owner (acc id) is no idfactory id
    public const string SELECT_ACCOUNT_QUERY = "SELECT `account_id` FROM `players` WHERE `id`=?";
    public const string SELECT_LEGION_QUERY = "SELECT `legion_id` FROM `legion_members` WHERE `player_id`=?";
    public const string DELETE_ACCOUNT_WH = "DELETE FROM inventory WHERE item_owner=? AND item_location=2";

    public static void LoadStorage(int ownerId, Storage storage)
    {
        LoadItems(ownerId, storage.GetStorageType(), storage.OnLoadHandler);
    }

    public static List<Item> LoadItems(int ownerId, StorageType storageType)
    {
        List<Item> items = new List<Item>();
        LoadItems(ownerId, storageType, items.Add);
        return items;
    }

    private static void LoadItems(int ownerId, StorageType storageType, Action<Item> itemConsumer)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = ownerId });
            stmt.Parameters.Add(new MySqlParameter { Value = storageType.GetId() });
            using MySqlDataReader rs = stmt.ExecuteReader();
            while (rs.Read())
            {
                Item item = ConstructItem(storageType.GetId(), rs);
                item.SetPersistentState(IPersistable.PersistentState.UPDATED);
                itemConsumer(item);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load " + storageType + " items of owner " + ownerId);
        }
    }

    public static List<Item> LoadBrokerItems()
    {
        List<Item> items = new List<Item>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_ALL_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = StorageType.BROKER.GetId() });
            using MySqlDataReader rs = stmt.ExecuteReader();
            while (rs.Read())
            {
                Item item = ConstructItem(StorageType.BROKER.GetId(), rs);
                item.SetPersistentState(IPersistable.PersistentState.UPDATED);
                items.Add(item);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load broker items");
        }
        return items;
    }

    public static List<PlayerAccountData.VisibleItem> LoadVisibleEquipment(int ownerId)
    {
        List<PlayerAccountData.VisibleItem> items = new List<PlayerAccountData.VisibleItem>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_EQUIPPED_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = (int)ItemStone.ItemStoneType.GODSTONE });
            stmt.Parameters.Add(new MySqlParameter { Value = ownerId });
            stmt.Parameters.Add(new MySqlParameter { Value = StorageType.CUBE.GetId() });
            using MySqlDataReader rs = stmt.ExecuteReader();
            while (rs.Read())
            {
                byte slotType = ItemSlot.GetEquipmentSlotType(rs.GetInt64(rs.GetOrdinal("slot")));
                if (slotType == 0)
                    continue; // skip equipment like rings and secondary weapons, as they are not visible and AbstractPlayerInfoPacket supports only 16 items
                int itemSkinId = rs.GetInt32(rs.GetOrdinal("item_skin"));
                int godStoneItemId = rs.GetInt32(rs.GetOrdinal("godstone_item_id"));
                int icOrd = rs.GetOrdinal("item_color");
                int? itemColor = rs.IsDBNull(icOrd) ? (int?)null : rs.GetInt32(icOrd);
                items.Add(new PlayerAccountData.VisibleItem(slotType, itemSkinId, godStoneItemId, itemColor));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load equipped items of owner " + ownerId);
        }
        return items;
    }

    private static Item ConstructItem(int storage, MySqlDataReader rset)
    {
        int itemUniqueId = rset.GetInt32(rset.GetOrdinal("item_unique_id"));
        int itemId = rset.GetInt32(rset.GetOrdinal("item_id"));
        long itemCount = rset.GetInt64(rset.GetOrdinal("item_count"));
        int icOrd = rset.GetOrdinal("item_color");
        int? itemColor = rset.IsDBNull(icOrd) ? (int?)null : rset.GetInt32(icOrd); // accepts null (which means not dyed)
        int colorExpireTime = rset.GetInt32(rset.GetOrdinal("color_expires"));
        string itemCreator = rset.GetString(rset.GetOrdinal("item_creator"));
        int expireTime = rset.GetInt32(rset.GetOrdinal("expire_time"));
        int activationCount = rset.GetInt32(rset.GetOrdinal("activation_count"));
        int isEquiped = rset.GetInt32(rset.GetOrdinal("is_equipped"));
        int isSoulBound = rset.GetInt32(rset.GetOrdinal("is_soul_bound"));
        long slot = rset.GetInt64(rset.GetOrdinal("slot"));
        int enchant = rset.GetInt32(rset.GetOrdinal("enchant"));
        int enchantBonus = rset.GetInt32(rset.GetOrdinal("enchant_bonus"));
        int itemSkin = rset.GetInt32(rset.GetOrdinal("item_skin"));
        int fusionedItem = rset.GetInt32(rset.GetOrdinal("fusioned_item"));
        int optionalSocket = rset.GetInt32(rset.GetOrdinal("optional_socket"));
        int optionalFusionSocket = rset.GetInt32(rset.GetOrdinal("optional_fusion_socket"));
        int charge = rset.GetInt32(rset.GetOrdinal("charge"));
        int tuneCount = rset.GetInt32(rset.GetOrdinal("tune_count"));
        int bonusStatsId = rset.GetInt32(rset.GetOrdinal("rnd_bonus"));
        int fusionedItemBonusStatsId = rset.GetInt32(rset.GetOrdinal("fusion_rnd_bonus"));
        int tempering = rset.GetInt32(rset.GetOrdinal("tempering"));
        int packCount = rset.GetInt32(rset.GetOrdinal("pack_count"));
        int isAmplified = rset.GetInt32(rset.GetOrdinal("is_amplified"));
        int buffSkill = rset.GetInt32(rset.GetOrdinal("buff_skill"));
        int rndPlumeBonusValue = rset.GetInt32(rset.GetOrdinal("rnd_plume_bonus"));

        return new Item(itemUniqueId, itemId, itemCount, itemColor, colorExpireTime, itemCreator, expireTime, activationCount, isEquiped == 1,
            isSoulBound == 1, slot, storage, enchant, enchantBonus, itemSkin, fusionedItem, optionalSocket, optionalFusionSocket, charge, tuneCount,
            bonusStatsId, fusionedItemBonusStatsId, tempering, packCount, isAmplified == 1, buffSkill, rndPlumeBonusValue);
    }

    private static int LoadPlayerAccountId(int playerId)
    {
        int accountId = 0;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_ACCOUNT_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            if (rset.Read())
            {
                accountId = rset.GetInt32(rset.GetOrdinal("account_id"));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore accountId data for player: " + playerId + " from DB: " + e.Message);
        }
        return accountId;
    }

    public static int LoadLegionId(int playerId)
    {
        int legionId = 0;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_LEGION_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader rset = stmt.ExecuteReader();
            if (rset.Read())
            {
                legionId = rset.GetInt32(rset.GetOrdinal("legion_id"));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to load legion id for player id: " + playerId);
        }
        return legionId;
    }

    public static bool Store(Item item, int playerId)
    {
        return Store(new List<Item> { item }, playerId);
    }

    public static bool Store(Item item, int? playerId, int? accountId, int? legionId)
    {
        return Store(new List<Item> { item }, playerId, accountId, legionId);
    }

    public static bool Store(Player player)
    {
        int playerId = player.GetObjectId();
        int? accountId = player.GetAccount() != null ? player.GetAccount().GetId() : (int?)null;
        int? legionId = player.GetLegion() != null ? player.GetLegion().GetLegionId() : (int?)null;

        List<Item> allPlayerItems = player.GetDirtyItemsToUpdate();
        return Store(allPlayerItems, playerId, accountId, legionId);
    }

    public static bool Store(Item item, Player player)
    {
        int playerId = player.GetObjectId();
        int accountId = player.GetAccount().GetId();
        int? legionId = player.GetLegion() != null ? player.GetLegion().GetLegionId() : (int?)null;

        return Store(item, playerId, accountId, legionId);
    }

    public static bool Store(List<Item> items, int playerId)
    {
        int? accountId = null;
        int? legionId = null;

        foreach (Item item in items)
        {
            if (accountId == null && item.GetItemLocation() == StorageType.ACCOUNT_WAREHOUSE.GetId())
            {
                accountId = LoadPlayerAccountId(playerId);
            }

            if (legionId == null && item.GetItemLocation() == StorageType.LEGION_WAREHOUSE.GetId())
            {
                int localLegionId = LoadLegionId(playerId);
                if (localLegionId > 0)
                    legionId = localLegionId;
            }
        }

        return Store(items, playerId, accountId, legionId);
    }

    public static bool Store(List<Item> items, int? playerId, int? accountId, int? legionId)
    {
        ICollection<Item> itemsToUpdate = items.Where(IPersistable.CHANGED).ToList();
        ICollection<Item> itemsToInsert = items.Where(IPersistable.NEW).ToList();
        ICollection<Item> itemsToDelete = items.Where(IPersistable.DELETED).ToList();

        bool deleteResult = false;
        bool insertResult = false;
        bool updateResult = false;

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            deleteResult = DeleteItems(con, itemsToDelete);
            insertResult = InsertItems(con, itemsToInsert, playerId, accountId, legionId);
            updateResult = UpdateItems(con, itemsToUpdate, playerId, accountId, legionId);
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't save inventory for player: " + playerId);
        }

        foreach (Item item in items)
        {
            item.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }

        if (deleteResult)
            IDFactory.GetInstance().ReleaseObjectIds(itemsToDelete);

        return deleteResult && insertResult && updateResult;
    }

    private static int GetItemOwnerId(Item item, int? playerId, int? accountId, int? legionId)
    {
        if (item.GetItemLocation() == StorageType.ACCOUNT_WAREHOUSE.GetId())
        {
            return accountId.Value;
        }

        if (item.GetItemLocation() == StorageType.LEGION_WAREHOUSE.GetId())
        {
            return legionId != null ? legionId.Value : playerId.Value;
        }

        return playerId.Value;
    }

    private static bool InsertItems(MySqlConnection con, ICollection<Item> items, int? playerId, int? accountId, int? legionId)
    {
        if (GenericValidator.IsBlankOrNull(items))
        {
            return true;
        }

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (Item item in items)
            {
                MySqlBatchCommand stmt = new MySqlBatchCommand(INSERT_QUERY);
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetObjectId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemTemplate().GetTemplateId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = (object)item.GetItemColor() ?? DBNull.Value }); // supports inserting null value
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetColorExpireTime() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemCreator() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetExpireTime() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetActivationCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = GetItemOwnerId(item, playerId, accountId, legionId) });
                stmt.Parameters.Add(new MySqlParameter { Value = item.IsEquipped() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.IsSoulBound() ? 1 : 0 });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetEquipmentSlot() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemLocation() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetEnchantLevel() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetEnchantBonus() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemSkinTemplate().GetTemplateId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetFusionedItemId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetOptionalSockets() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetFusionedItemOptionalSockets() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetChargePoints() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetTuneCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetBonusStatsId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetFusionedItemBonusStatsId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetTempering() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetPackCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.IsAmplified() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetBuffSkill() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetRndPlumeBonusValue() });
                batch.BatchCommands.Add(stmt);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to execute insert batch");
            return false;
        }
        return true;
    }

    private static bool UpdateItems(MySqlConnection con, ICollection<Item> items, int? playerId, int? accountId, int? legionId)
    {
        if (GenericValidator.IsBlankOrNull(items))
        {
            return true;
        }

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (Item item in items)
            {
                MySqlBatchCommand stmt = new MySqlBatchCommand(UPDATE_QUERY);
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = (object)item.GetItemColor() ?? DBNull.Value }); // supports inserting null value
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetColorExpireTime() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemCreator() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetExpireTime() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetActivationCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = GetItemOwnerId(item, playerId, accountId, legionId) });
                stmt.Parameters.Add(new MySqlParameter { Value = item.IsEquipped() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.IsSoulBound() ? 1 : 0 });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetEquipmentSlot() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemLocation() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetEnchantLevel() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetEnchantBonus() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemSkinTemplate().GetTemplateId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetFusionedItemId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetOptionalSockets() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetFusionedItemOptionalSockets() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetChargePoints() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetTuneCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetBonusStatsId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetFusionedItemBonusStatsId() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetTempering() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetPackCount() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.IsAmplified() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetBuffSkill() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetRndPlumeBonusValue() });
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetObjectId() });
                batch.BatchCommands.Add(stmt);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to execute update batch");
            return false;
        }
        return true;
    }

    private static bool DeleteItems(MySqlConnection con, ICollection<Item> items)
    {
        if (GenericValidator.IsBlankOrNull(items))
        {
            return true;
        }

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (Item item in items)
            {
                MySqlBatchCommand stmt = new MySqlBatchCommand(DELETE_QUERY);
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetObjectId() });
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

    /// <summary>Since inventory is not using FK - need to clean items</summary>
    public static bool DeletePlayerOrLegionItems(int playerOrLegionId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_CLEAN_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerOrLegionId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error deleting all player or legion items. playerOrLegionId: " + playerOrLegionId);
            return false;
        }
        return true;
    }

    public static void DeleteAccountWH(int accountId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_ACCOUNT_WH;
            stmt.Parameters.Add(new MySqlParameter { Value = accountId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error deleting all items from account WH. AccountId: " + accountId);
        }
    }

    public static int[] GetUsedIDs()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT item_unique_id FROM inventory";
            using MySqlDataReader rs = stmt.ExecuteReader();
            List<int> ids = new List<int>();
            while (rs.Read())
                ids.Add(rs.GetInt32(rs.GetOrdinal("item_unique_id")));
            return ids.ToArray();
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't get list of IDs from inventory table");
            return null;
        }
    }
}
