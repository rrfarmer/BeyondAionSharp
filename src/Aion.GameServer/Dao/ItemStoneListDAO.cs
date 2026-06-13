using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using ItemStoneType = Aion.GameServer.Model.Items.ItemStone.ItemStoneType;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/ItemStoneListDAO (@author ATracer, Wakizashi). JDBC DAO over item_stones. DatabaseFactory-style. ist.ordinal() ->
/// (int)ist (ItemStoneType is a plain sequential enum MANASTONE=0/GODSTONE=1/FUSIONSTONE=2/IDIANSTONE=3, matching the category column).
/// stream().filter(Persistable.NEW/CHANGED/DELETED) -> Where(IPersistable.NEW/CHANGED/DELETED) (tolerated red predicate statics);
/// instanceof+cast -> is X; Set&lt;? extends ItemStone&gt; -> IEnumerable&lt;ItemStone&gt; (covariant); Collections.singleton -> single-elem
/// HashSet. store helpers: shared connection (setAutoCommit(false)) with per-helper MySqlTransaction+MySqlBatch+Commit. NOTE deleteItemStones'
/// Java quirk (st.execute() THEN addBatch() then executeBatch == double-run) is collapsed to one ExecuteNonQuery per row (idempotent DELETE).
/// GenericValidator.IsBlankOrNull, DataManager.ITEM_DATA, ItemGroup.SPECIAL_MANASTONE and many Item.* methods are tolerated red refs. SQL verbatim.
/// </summary>
public class ItemStoneListDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ItemStoneListDAO));

    public const string INSERT_QUERY = "INSERT INTO `item_stones` (`item_unique_id`, `item_id`, `slot`, `category`, `polishNumber`, `polishCharge`, `proc_count`) VALUES (?,?,?,?,?,?,?)";
    public const string UPDATE_QUERY = "UPDATE `item_stones` SET `item_id`=?, `slot`=?, `polishNumber`=?, `polishCharge`=?, `proc_count`=? where `item_unique_id`=? AND `category`=?";
    public const string DELETE_QUERY = "DELETE FROM `item_stones` WHERE `item_unique_id`=? AND slot=? AND category=?";
    public const string SELECT_QUERY = "SELECT `item_id`, `slot`, `category`, `polishNumber`, `polishCharge`, `proc_count` FROM `item_stones` WHERE `item_unique_id`=?";

    public static void Load(ICollection<Item> items)
    {
        List<Item> validItems = items.Where(i => i.GetItemTemplate().IsArmor() || i.GetItemTemplate().IsWeapon()).ToList();
        if (validItems.Count == 0)
            return;
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            foreach (Item item in validItems)
            {
                stmt.Parameters.Clear();
                stmt.Parameters.Add(new MySqlParameter { Value = item.GetObjectId() });
                using MySqlDataReader rset = stmt.ExecuteReader();
                while (rset.Read())
                {
                    int itemId = rset.GetInt32(rset.GetOrdinal("item_id"));
                    int slot = rset.GetInt32(rset.GetOrdinal("slot"));
                    int stoneType = rset.GetInt32(rset.GetOrdinal("category"));
                    int activatedCount = rset.GetInt32(rset.GetOrdinal("proc_count"));
                    switch (stoneType)
                    {
                        case 0:
                            if (item.GetSockets(false) <= item.GetItemStonesSize())
                            {
                                log.LogWarning("Deleting manastone " + itemId + " due to slot overload from " + item);
                                DeleteItemStone(con, item.GetObjectId(), slot, stoneType);
                                continue;
                            }
                            if (DataManager.ITEM_DATA.GetItemTemplate(itemId).GetItemGroup() == ItemGroup.SPECIAL_MANASTONE
                                && slot >= item.GetItemTemplate().GetSpecialSlots())
                            {
                                log.LogWarning("Deleting special manastone " + itemId + " from normal slot of " + item);
                                DeleteItemStone(con, item.GetObjectId(), slot, stoneType);
                                continue;
                            }
                            item.GetItemStones().Add(new ManaStone(item.GetObjectId(), itemId, slot, IPersistable.PersistentState.UPDATED));
                            break;
                        case 1:
                            item.AddGodStone(itemId, activatedCount);
                            GodStone godstone = item.GetGodStone();
                            if (godstone != null)
                                godstone.SetPersistentState(IPersistable.PersistentState.UPDATED);
                            break;
                        case 2:
                            if (item.GetSockets(true) <= item.GetFusionStonesSize())
                            {
                                log.LogWarning("Deleting manastone " + itemId + " due to slot overload from fusioned item of " + item);
                                DeleteItemStone(con, item.GetObjectId(), slot, stoneType);
                                continue;
                            }
                            if (DataManager.ITEM_DATA.GetItemTemplate(itemId).GetItemGroup() == ItemGroup.SPECIAL_MANASTONE
                                && slot >= item.GetFusionedItemTemplate().GetSpecialSlots())
                            {
                                log.LogWarning("Deleting special manastone " + itemId + " from normal slot of fusioned item of " + item);
                                DeleteItemStone(con, item.GetObjectId(), slot, stoneType);
                                continue;
                            }
                            item.GetFusionStones().Add(new ManaStone(item.GetObjectId(), itemId, slot, IPersistable.PersistentState.UPDATED));
                            break;
                        case 3:
                            item.SetIdianStone(
                                new IdianStone(itemId, IPersistable.PersistentState.UPDATE_REQUIRED, item, rset.GetInt32(rset.GetOrdinal("polishNumber")), rset.GetInt32(rset.GetOrdinal("polishCharge"))));
                            break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore ItemStoneList data from DB: " + e.Message);
        }
    }

    public static void Save(List<Item> items)
    {
        if (GenericValidator.IsBlankOrNull(items))
        {
            return;
        }

        HashSet<ManaStone> manaStones = new HashSet<ManaStone>();
        HashSet<ManaStone> fusionStones = new HashSet<ManaStone>();
        HashSet<GodStone> godStones = new HashSet<GodStone>();
        HashSet<IdianStone> idianStones = new HashSet<IdianStone>();

        foreach (Item item in items)
        {
            if (item.HasManaStones())
            {
                manaStones.UnionWith(item.GetItemStones());
            }

            if (item.HasFusionStones())
            {
                fusionStones.UnionWith(item.GetFusionStones());
            }

            GodStone godStone = item.GetGodStone();
            if (godStone != null)
            {
                godStones.Add(godStone);
            }
            IdianStone idianStone = item.GetIdianStone();
            if (idianStone != null)
            {
                idianStones.Add(idianStone);
            }
        }
        Store(manaStones, ItemStoneType.MANASTONE);
        Store(fusionStones, ItemStoneType.FUSIONSTONE);
        Store(godStones, ItemStoneType.GODSTONE);
        Store(idianStones, ItemStoneType.IDIANSTONE);
    }

    public static void StoreManaStones(HashSet<ManaStone> manaStones)
    {
        Store(manaStones, ItemStoneType.MANASTONE);
    }

    public static void StoreGodStones(GodStone godStones)
    {
        Store(new HashSet<GodStone> { godStones }, ItemStoneType.GODSTONE);
    }

    public static void StoreFusionStone(HashSet<ManaStone> manaStones)
    {
        Store(manaStones, ItemStoneType.FUSIONSTONE);
    }

    public static void StoreIdianStones(IdianStone idianStone)
    {
        Store(new HashSet<IdianStone> { idianStone }, ItemStoneType.IDIANSTONE);
    }

    private static void Store(IEnumerable<ItemStone> stones, ItemStoneType ist)
    {
        if (GenericValidator.IsBlankOrNull(stones))
        {
            return;
        }

        HashSet<ItemStone> stonesToAdd = stones.Where(x => IPersistable.NEW(x)).ToHashSet();
        HashSet<ItemStone> stonesToDelete = stones.Where(x => IPersistable.DELETED(x)).ToHashSet();
        HashSet<ItemStone> stonesToUpdate = stones.Where(x => IPersistable.CHANGED(x)).ToHashSet();

        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();

            DeleteItemStones(con, stonesToDelete, ist);
            AddItemStones(con, stonesToAdd, ist);
            UpdateItemStones(con, stonesToUpdate, ist);
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't save stones");
        }

        foreach (ItemStone @is in stones)
        {
            @is.SetPersistentState(IPersistable.PersistentState.UPDATED);
        }
    }

    private static void AddItemStones(MySqlConnection con, ICollection<ItemStone> itemStones, ItemStoneType ist)
    {
        if (GenericValidator.IsBlankOrNull(itemStones))
        {
            return;
        }

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (ItemStone @is in itemStones)
            {
                MySqlBatchCommand st = new MySqlBatchCommand(INSERT_QUERY);
                st.Parameters.Add(new MySqlParameter { Value = @is.GetItemObjId() });
                st.Parameters.Add(new MySqlParameter { Value = @is.GetItemId() });
                st.Parameters.Add(new MySqlParameter { Value = @is.GetSlot() });
                st.Parameters.Add(new MySqlParameter { Value = (int)ist });
                if (@is is IdianStone stone)
                {
                    st.Parameters.Add(new MySqlParameter { Value = stone.GetPolishNumber() });
                    st.Parameters.Add(new MySqlParameter { Value = stone.GetPolishCharge() });
                }
                else
                {
                    st.Parameters.Add(new MySqlParameter { Value = 0 });
                    st.Parameters.Add(new MySqlParameter { Value = 0 });
                }
                if (@is is GodStone gs)
                {
                    st.Parameters.Add(new MySqlParameter { Value = gs.GetActivatedCount() });
                }
                else
                {
                    st.Parameters.Add(new MySqlParameter { Value = 0 });
                }

                batch.BatchCommands.Add(st);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error occured while saving item stones");
        }
    }

    private static void UpdateItemStones(MySqlConnection con, ICollection<ItemStone> itemStones, ItemStoneType ist)
    {
        if (GenericValidator.IsBlankOrNull(itemStones))
        {
            return;
        }

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            using MySqlBatch batch = new MySqlBatch(con, tx);
            foreach (ItemStone @is in itemStones)
            {
                MySqlBatchCommand st = new MySqlBatchCommand(UPDATE_QUERY);
                st.Parameters.Add(new MySqlParameter { Value = @is.GetItemId() });
                st.Parameters.Add(new MySqlParameter { Value = @is.GetSlot() });
                if (@is is IdianStone stone)
                {
                    st.Parameters.Add(new MySqlParameter { Value = stone.GetPolishNumber() });
                    st.Parameters.Add(new MySqlParameter { Value = stone.GetPolishCharge() });
                }
                else
                {
                    st.Parameters.Add(new MySqlParameter { Value = 0 });
                    st.Parameters.Add(new MySqlParameter { Value = 0 });
                }
                if (@is is GodStone gs)
                {
                    st.Parameters.Add(new MySqlParameter { Value = gs.GetActivatedCount() });
                }
                else
                {
                    st.Parameters.Add(new MySqlParameter { Value = 0 });
                }
                st.Parameters.Add(new MySqlParameter { Value = @is.GetItemObjId() });
                st.Parameters.Add(new MySqlParameter { Value = (int)ist });
                batch.BatchCommands.Add(st);
            }

            batch.ExecuteNonQuery();
            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error occured while saving item stones");
        }
    }

    private static void DeleteItemStones(MySqlConnection con, ICollection<ItemStone> itemStones, ItemStoneType ist)
    {
        if (GenericValidator.IsBlankOrNull(itemStones))
        {
            return;
        }

        try
        {
            using MySqlTransaction tx = con.BeginTransaction();
            // TODO: Shouldn't we update stone slot?
            foreach (ItemStone @is in itemStones)
            {
                using MySqlCommand st = con.CreateCommand();
                st.Transaction = tx;
                st.CommandText = DELETE_QUERY;
                st.Parameters.Add(new MySqlParameter { Value = @is.GetItemObjId() });
                st.Parameters.Add(new MySqlParameter { Value = @is.GetSlot() });
                st.Parameters.Add(new MySqlParameter { Value = (int)ist });
                st.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error occured while saving item stones");
        }
    }

    private static void DeleteItemStone(MySqlConnection con, int uid, int slot, int category)
    {
        try
        {
            using MySqlCommand st = con.CreateCommand();
            st.CommandText = DELETE_QUERY;
            st.Parameters.Add(new MySqlParameter { Value = uid });
            st.Parameters.Add(new MySqlParameter { Value = slot });
            st.Parameters.Add(new MySqlParameter { Value = category });
            st.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error occured while saving item stones");
        }
    }

    /// <summary>Saves stones of player.</summary>
    public static void Save(Player player)
    {
        Save(player.GetAllItems());
    }
}
