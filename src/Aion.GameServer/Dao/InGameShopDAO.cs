using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.Ingameshop;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/InGameShopDAO (@author xTz, KID). JDBC DAO over the ingameshop table (cash-shop catalog by category).
/// DatabaseFactory-style; positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; execute -> ExecuteNonQuery; SQL
/// (incl. saveIngameShopItem's space-less concat) verbatim. Java signed byte -> C# sbyte (matching IGItem's ctor): rset.getByte("col")
/// -> (sbyte)reader.GetByte(reader.GetOrdinal("col")); Map&lt;Byte,List&lt;IGItem&gt;&gt; -> Dictionary&lt;sbyte,List&lt;IGItem&gt;&gt;
/// (containsKey->ContainsKey, put->indexer, get->indexer). Java's setInt on a byte (delete category/sub_category) -> (int)cast; setByte
/// on save -> sbyte value. subCategory&lt;3 skip preserved.
/// </summary>
public class InGameShopDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(InGameShopDAO));

    public const string SELECT_QUERY = "SELECT `object_id`, `item_id`, `item_count`, `item_price`, `category`, `sub_category`, `list`, `sales_ranking`, `item_type`, `gift`, `title_description`, `description` FROM `ingameshop`";
    public const string DELETE_QUERY = "DELETE FROM `ingameshop` WHERE `item_id`=? AND `category`=? AND `sub_category`=? AND `list`=?";
    public const string UPDATE_SALES_QUERY = "UPDATE `ingameshop` SET `sales_ranking`=? WHERE `object_id`=?";

    public static Dictionary<sbyte, List<IGItem>> LoadInGameShopItems()
    {
        Dictionary<sbyte, List<IGItem>> items = new Dictionary<sbyte, List<IGItem>>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_QUERY;
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                sbyte category = (sbyte)rset.GetByte(rset.GetOrdinal("category"));
                sbyte subCategory = (sbyte)rset.GetByte(rset.GetOrdinal("sub_category"));
                if (subCategory < 3)
                    continue;
                int objectId = rset.GetInt32(rset.GetOrdinal("object_id"));
                int itemId = rset.GetInt32(rset.GetOrdinal("item_id"));
                long itemCount = rset.GetInt64(rset.GetOrdinal("item_count"));
                long itemPrice = rset.GetInt64(rset.GetOrdinal("item_price"));
                int list = rset.GetInt32(rset.GetOrdinal("list"));
                int salesRanking = rset.GetInt32(rset.GetOrdinal("sales_ranking"));
                sbyte itemType = (sbyte)rset.GetByte(rset.GetOrdinal("item_type"));
                sbyte gift = (sbyte)rset.GetByte(rset.GetOrdinal("gift"));
                string titleDescription = rset.GetString(rset.GetOrdinal("title_description"));
                string description = rset.GetString(rset.GetOrdinal("description"));
                if (!items.ContainsKey(category))
                {
                    items[category] = new List<IGItem>();
                }
                items[category].Add(
                    new IGItem(objectId, itemId, itemCount, itemPrice, category, subCategory, list, salesRanking, itemType, gift, titleDescription,
                        description));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore inGameShop data for all from DB: " + e.Message);
        }
        return items;
    }

    public static bool DeleteIngameShopItem(int itemId, sbyte category, sbyte subCategory, int list)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = DELETE_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = itemId });
            stmt.Parameters.Add(new MySqlParameter { Value = (int)category });
            stmt.Parameters.Add(new MySqlParameter { Value = (int)subCategory });
            stmt.Parameters.Add(new MySqlParameter { Value = list });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error delete ingameshopItem: " + itemId);
            return false;
        }
        return true;
    }

    public static void SaveIngameShopItem(int objectId, int itemId, long itemCount, long itemPrice, sbyte category, sbyte subCategory, int list,
        int salesRanking, sbyte itemType, sbyte gift, string titleDescription, string description)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "INSERT INTO ingameshop(object_id, item_id, item_count, item_price, category, sub_category, list, sales_ranking, item_type, gift, title_description, description)"
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
            stmt.Parameters.Add(new MySqlParameter { Value = objectId });
            stmt.Parameters.Add(new MySqlParameter { Value = itemId });
            stmt.Parameters.Add(new MySqlParameter { Value = itemCount });
            stmt.Parameters.Add(new MySqlParameter { Value = itemPrice });
            stmt.Parameters.Add(new MySqlParameter { Value = category });
            stmt.Parameters.Add(new MySqlParameter { Value = subCategory });
            stmt.Parameters.Add(new MySqlParameter { Value = list });
            stmt.Parameters.Add(new MySqlParameter { Value = salesRanking });
            stmt.Parameters.Add(new MySqlParameter { Value = itemType });
            stmt.Parameters.Add(new MySqlParameter { Value = gift });
            stmt.Parameters.Add(new MySqlParameter { Value = titleDescription });
            stmt.Parameters.Add(new MySqlParameter { Value = description });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error saving Item: " + objectId);
        }
    }

    public static bool IncreaseSales(int objectVal, int current)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_SALES_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = current });
            stmt.Parameters.Add(new MySqlParameter { Value = objectVal });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error increaseSales Item: " + objectVal);
            return false;
        }
        return true;
    }
}
