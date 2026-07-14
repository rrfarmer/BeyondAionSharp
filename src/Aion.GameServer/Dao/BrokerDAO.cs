using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.Broker;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/BrokerDAO. JDBC DAO over the broker table via the commons DB callback helper. Anonymous ReadStH/IUStH -> private
/// nested classes capturing locals via ctor. BrokerRace.valueOf->Enum.Parse, String.valueOf(race)->race.ToString(); getBoolean->GetBoolean;
/// getInt/getLong/getString->GetX(GetOrdinal). The reworked BrokerItem ctor/GetExpireTime/GetSettleTime use NON-nullable DateTimeOffset
/// (Java Timestamp was nullable) -> SQL epoch projection, DB-null -> default(DateTimeOffset) sentinel; writes and timestamp predicates
/// use FROM_UNIXTIME(epoch). store() switch-arrow NEW/DELETED/UPDATE_REQUIRED -> C# switch on
/// IPersistable.PersistentState, set UPDATED on success. InventoryDAO.LoadBrokerItems/Store and ItemStoneListDAO.Load are tolerated red
/// refs (those DAOs not yet ported). SQL verbatim.
/// </summary>
public class BrokerDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(BrokerDAO));

    public static List<BrokerItem> LoadBroker()
    {
        List<BrokerItem> brokerItems = new List<BrokerItem>();

        List<Item> items = InventoryDAO.LoadBrokerItems();
        ItemStoneListDAO.Load(items);

        DB.Select("SELECT *, CAST(FLOOR(UNIX_TIMESTAMP(`expire_time`) * 1000) AS SIGNED) AS `expire_time_epoch_millis`, CAST(FLOOR(UNIX_TIMESTAMP(`settle_time`) * 1000) AS SIGNED) AS `settle_time_epoch_millis` FROM broker", new LoadHandler(brokerItems, items));

        return brokerItems;
    }

    private sealed class LoadHandler : ReadStH
    {
        private readonly List<BrokerItem> brokerItems;
        private readonly List<Item> items;

        internal LoadHandler(List<BrokerItem> brokerItems, List<Item> items)
        {
            this.brokerItems = brokerItems;
            this.items = items;
        }

        public void HandleRead(MySqlDataReader rset)
        {
            while (rset.Read())
            {
                int itemPointer = rset.GetInt32(rset.GetOrdinal("item_pointer"));
                int itemId = rset.GetInt32(rset.GetOrdinal("item_id"));
                long itemCount = rset.GetInt64(rset.GetOrdinal("item_count"));
                int icOrd = rset.GetOrdinal("item_creator"); // item_creator is nullable (DEFAULT NULL — null for non-crafted items); Java getString tolerates null, C# GetString throws on DBNull
                string itemCreator = rset.IsDBNull(icOrd) ? null : rset.GetString(icOrd);
                int sellerId = rset.GetInt32(rset.GetOrdinal("seller_id"));
                long price = rset.GetInt64(rset.GetOrdinal("price"));
                BrokerRace itemBrokerRace = Enum.Parse<BrokerRace>(rset.GetString(rset.GetOrdinal("broker_race")));
                DateTimeOffset expireTime = DatabaseTimestamp.ReadNullableDateTimeOffset(rset, "expire_time_epoch_millis") ?? default;
                DateTimeOffset settleTime = DatabaseTimestamp.ReadNullableDateTimeOffset(rset, "settle_time_epoch_millis") ?? default;
                bool isSold = rset.GetBoolean(rset.GetOrdinal("is_sold"));
                bool isSettled = rset.GetBoolean(rset.GetOrdinal("is_settled"));
                bool splittingAvailable = rset.GetBoolean(rset.GetOrdinal("splitting_available"));

                Item item = null;
                if (!isSold)
                    foreach (Item brItem in items)
                    {
                        if (itemPointer == brItem.GetObjectId())
                        {
                            item = brItem;
                            break;
                        }
                    }

                brokerItems.Add(new BrokerItem(item, itemId, itemPointer, itemCount, itemCreator, price, sellerId, itemBrokerRace, isSold,
                    isSettled, expireTime, settleTime, splittingAvailable));
            }
        }
    }

    public static bool Store(BrokerItem item)
    {
        bool result = false;

        if (item == null)
        {
            log.LogWarning("Null broker item on save");
            return result;
        }

        switch (item.GetPersistentState())
        {
            case IPersistable.PersistentState.NEW:
                result = InsertBrokerItem(item);
                if (item.GetItem() != null)
                    InventoryDAO.Store(item.GetItem(), item.GetSellerId());
                break;

            case IPersistable.PersistentState.DELETED:
                result = DeleteBrokerItem(item);
                break;

            case IPersistable.PersistentState.UPDATE_REQUIRED:
                result = UpdateBrokerItem(item);
                break;
        }

        if (result)
            item.SetPersistentState(IPersistable.PersistentState.UPDATED);

        return result;
    }

    private static bool InsertBrokerItem(BrokerItem item)
    {
        bool result = DB.InsertUpdate(
            "INSERT INTO `broker` (`item_pointer`, `item_id`, `item_count`, `item_creator`, `price`, `broker_race`, `expire_time`, `seller_id`, `is_sold`, `is_settled`, `splitting_available`) VALUES (?,?,?,?,?,?,FROM_UNIXTIME(? / 1000.0),?,?,?,?)",
            new InsertHandler(item));

        return result;
    }

    private sealed class InsertHandler : IUStH
    {
        private readonly BrokerItem item;

        internal InsertHandler(BrokerItem item)
        {
            this.item = item;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemUniqueId() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemId() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemCount() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemCreator() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetPrice() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemBrokerRace().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(item.GetExpireTime()) });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetSellerId() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.IsSold() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.IsSettled() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.IsSplittingAvailable() });
            stmt.ExecuteNonQuery();
        }
    }

    private static bool DeleteBrokerItem(BrokerItem item)
    {
        bool result = DB.InsertUpdate("DELETE FROM `broker` WHERE `item_pointer` = ? AND `seller_id` = ? AND `expire_time` = FROM_UNIXTIME(? / 1000.0)", new DeleteHandler(item));

        return result;
    }

    private sealed class DeleteHandler : IUStH
    {
        private readonly BrokerItem item;

        internal DeleteHandler(BrokerItem item)
        {
            this.item = item;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemUniqueId() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetSellerId() });
            stmt.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(item.GetExpireTime()) });
            stmt.ExecuteNonQuery();
        }
    }

    private static bool UpdateBrokerItem(BrokerItem item)
    {
        bool result = DB.InsertUpdate(
            "UPDATE broker SET `is_sold` = ?, `is_settled` = ?, `settle_time` = FROM_UNIXTIME(? / 1000.0), `item_count` = ? WHERE `item_pointer` = ? AND `expire_time` = FROM_UNIXTIME(? / 1000.0) AND `seller_id` = ? AND `is_settled` = 0",
            new UpdateHandler(item));

        return result;
    }

    private sealed class UpdateHandler : IUStH
    {
        private readonly BrokerItem item;

        internal UpdateHandler(BrokerItem item)
        {
            this.item = item;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = item.IsSold() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.IsSettled() });
            stmt.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(item.GetSettleTime()) });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemCount() });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetItemUniqueId() });
            stmt.Parameters.Add(new MySqlParameter { Value = DatabaseTimestamp.ToUnixTimeMilliseconds(item.GetExpireTime()) });
            stmt.Parameters.Add(new MySqlParameter { Value = item.GetSellerId() });
            stmt.ExecuteNonQuery();
        }
    }
}
