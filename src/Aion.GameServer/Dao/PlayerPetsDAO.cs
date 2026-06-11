using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerPetsDAO (@author Xitanium, Kamui, Rolandas, M@xx, xTz). JDBC DAO over player_pets. DatabaseFactory-style;
/// positional '?' -> ordered MySqlParameter; executeQuery -> ExecuteReader; getInt/getLong/getString->GetX(GetOrdinal); execute->ExecuteNonQuery;
/// SQL verbatim. getUsedIDs scrollable ResultSet -> forward-only List&lt;int&gt;+ToArray (scroll flags dropped). PetCommonData despawn/birthday
/// use DateTime?: getTimestamp->IsDBNull?null:GetDateTime; setTimestamp(null)->DBNull.Value; the null despawn fallback new Timestamp(now)
/// -> DateTime.Now. PetHungryLevel.fromId->FromId; doping ids CSV split + Integer.parseInt->int.Parse. Pet types fully-qualified to match
/// PetCommonData's declared return types (Services.ToyPet / Model.Templates.Pet). FeedProgress/DopingBag null-guards preserved.
/// </summary>
public class PlayerPetsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerPetsDAO));

    public static void SaveFeedStatus(int petObjectId, int hungryLevel, int feedProgress, long reuseTime)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "UPDATE player_pets SET hungry_level = ?, feed_progress = ?, reuse_time = ? WHERE id = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = hungryLevel });
            stmt.Parameters.Add(new MySqlParameter { Value = feedProgress });
            stmt.Parameters.Add(new MySqlParameter { Value = reuseTime });
            stmt.Parameters.Add(new MySqlParameter { Value = petObjectId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error updating feed status for pet #" + petObjectId);
        }
    }

    public static void SaveDopingBag(int petObjectId, Aion.GameServer.Model.Templates.Pet.PetDopingBag bag)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "UPDATE player_pets SET dopings = ? WHERE id = ?";
            string itemIds = bag.GetFoodItem() + "," + bag.GetDrinkItem();
            foreach (int itemId in bag.GetScrollsUsed())
                itemIds += "," + itemId;
            stmt.Parameters.Add(new MySqlParameter { Value = itemIds });
            stmt.Parameters.Add(new MySqlParameter { Value = petObjectId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error update doping for pet #" + petObjectId);
        }
    }

    public static void SetTime(int petObjectId, long time)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "UPDATE player_pets SET reuse_time = ? WHERE id = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = time });
            stmt.Parameters.Add(new MySqlParameter { Value = petObjectId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error update pet #" + petObjectId);
        }
    }

    public static void InsertPlayerPet(Player player, PetCommonData petCommonData)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "INSERT INTO player_pets(id, player_id, template_id, decoration, name, despawn_time, expire_time) VALUES(?, ?, ?, ?, ?, ?, ?)";
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetTemplateId() });
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetDecoration() });
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetName() });
            stmt.Parameters.Add(new MySqlParameter { Value = (object)petCommonData.GetDespawnTime() ?? DBNull.Value });
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetExpireTime() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error inserting new pet #" + petCommonData.GetObjectId() + ", name: " + petCommonData.GetName());
        }
    }

    public static void RemovePlayerPet(int petObjectId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "DELETE FROM player_pets WHERE id = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = petObjectId });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error removing pet #" + petObjectId);
        }
    }

    public static List<PetCommonData> GetPlayerPets(Player player)
    {
        List<PetCommonData> pets = new List<PetCommonData>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT * FROM player_pets WHERE player_id = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            using MySqlDataReader rs = stmt.ExecuteReader();
            while (rs.Read())
            {
                PetCommonData petCommonData = new PetCommonData(rs.GetInt32(rs.GetOrdinal("id")), rs.GetInt32(rs.GetOrdinal("template_id")), player.GetObjectId(),
                    rs.GetInt32(rs.GetOrdinal("expire_time")));
                petCommonData.SetName(rs.GetString(rs.GetOrdinal("name")));
                petCommonData.SetDecoration(rs.GetInt32(rs.GetOrdinal("decoration")));
                if (petCommonData.GetFeedProgress() != null)
                {
                    petCommonData.GetFeedProgress().SetHungryLevel(Aion.GameServer.Services.ToyPet.PetHungryLevel.FromId(rs.GetInt32(rs.GetOrdinal("hungry_level"))));
                    petCommonData.GetFeedProgress().SetData(rs.GetInt32(rs.GetOrdinal("feed_progress")));
                    petCommonData.SetRefeedTime(rs.GetInt64(rs.GetOrdinal("reuse_time")));
                }
                if (petCommonData.GetDopingBag() != null)
                {
                    int dopingsOrd = rs.GetOrdinal("dopings");
                    string dopings = rs.IsDBNull(dopingsOrd) ? null : rs.GetString(dopingsOrd);
                    if (dopings != null)
                    {
                        string[] ids = dopings.Split(',');
                        for (int i = 0; i < ids.Length; i++)
                            petCommonData.GetDopingBag().SetItem(int.Parse(ids[i]), i);
                    }
                }
                int bdOrd = rs.GetOrdinal("birthday");
                petCommonData.SetBirthday(rs.IsDBNull(bdOrd) ? (DateTime?)null : rs.GetDateTime(bdOrd));
                petCommonData.SetStartMoodTime(rs.GetInt64(rs.GetOrdinal("mood_started")));
                petCommonData.SetShuggleCounter(rs.GetInt32(rs.GetOrdinal("counter")));
                petCommonData.SetMoodCdStarted(rs.GetInt64(rs.GetOrdinal("mood_cd_started")));
                petCommonData.SetGiftCdStarted(rs.GetInt64(rs.GetOrdinal("gift_cd_started")));
                int dtOrd = rs.GetOrdinal("despawn_time");
                DateTime? ts = rs.IsDBNull(dtOrd) ? (DateTime?)null : rs.GetDateTime(dtOrd);
                if (ts == null)
                    ts = DateTime.Now;
                petCommonData.SetDespawnTime(ts);
                pets.Add(petCommonData);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Error loading pets for " + player);
        }
        return pets;
    }

    public static void UpdatePetName(PetCommonData petCommonData)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "UPDATE player_pets SET name = ? WHERE id = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetName() });
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error update pet #" + petCommonData.GetObjectId());
        }
    }

    public static bool SavePetMoodData(PetCommonData petCommonData)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "UPDATE player_pets SET mood_started = ?, counter = ?, mood_cd_started = ?, gift_cd_started = ?, despawn_time = ? WHERE id = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetMoodStartTime() });
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetShuggleCounter() });
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetMoodCdStarted() });
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetGiftCdStarted() });
            stmt.Parameters.Add(new MySqlParameter { Value = (object)petCommonData.GetDespawnTime() ?? DBNull.Value });
            stmt.Parameters.Add(new MySqlParameter { Value = petCommonData.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error updating mood for pet #" + petCommonData.GetObjectId());
            return false;
        }
        return true;
    }

    public static int[] GetUsedIDs()
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT id FROM player_pets";
            using MySqlDataReader rs = stmt.ExecuteReader();
            List<int> ids = new List<int>();
            while (rs.Read())
                ids.Add(rs.GetInt32(rs.GetOrdinal("id")));
            return ids.ToArray();
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't get list of IDs from pets table");
            return null;
        }
    }
}
