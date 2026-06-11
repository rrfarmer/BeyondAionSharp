using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerSettingsDAO (@author ATracer, Neon). JDBC DAO over player_settings. MIXED: loadSettings via DatabaseFactory;
/// saveSettings via the commons DB callback helper. The five near-identical anonymous IUStH bodies (each REPLACE with playerId/type/value)
/// are consolidated into one parameterized nested SettingHandler(playerId, type, value) - same behavior, one class instead of five.
/// switch-arrow on settings_type (0/1/2/-1/-2) -> C# switch; resultSet.getBytes->GetFieldValue&lt;byte[]&gt;; getInt->GetInt32;
/// setBytes/setInt->Parameters.Add. Persistable.PersistentState->IPersistable.PersistentState. SQL verbatim. (Note: case 2 sets
/// HouseBuddies per the code, despite the class TODO comment saying "2 - display".)
/// </summary>
public class PlayerSettingsDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerSettingsDAO));

    public static PlayerSettings LoadSettings(int playerId)
    {
        PlayerSettings playerSettings = new PlayerSettings();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT * FROM player_settings WHERE player_id = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader resultSet = stmt.ExecuteReader();
            while (resultSet.Read())
            {
                int type = resultSet.GetInt32(resultSet.GetOrdinal("settings_type"));
                int settingsOrd = resultSet.GetOrdinal("settings");
                switch (type)
                {
                    case 0:
                        playerSettings.SetUiSettings(resultSet.GetFieldValue<byte[]>(settingsOrd));
                        break;
                    case 1:
                        playerSettings.SetShortcuts(resultSet.GetFieldValue<byte[]>(settingsOrd));
                        break;
                    case 2:
                        playerSettings.SetHouseBuddies(resultSet.GetFieldValue<byte[]>(settingsOrd));
                        break;
                    case -1:
                        playerSettings.SetDisplay(resultSet.GetInt32(settingsOrd));
                        break;
                    case -2:
                        playerSettings.SetDeny(resultSet.GetInt32(settingsOrd));
                        break;
                }
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore PlayerSettings data for player " + playerId + " from DB: " + e.Message);
        }
        playerSettings.SetPersistentState(IPersistable.PersistentState.UPDATED);
        return playerSettings;
    }

    public static void SaveSettings(Player player)
    {
        int playerId = player.GetObjectId();

        PlayerSettings playerSettings = player.GetPlayerSettings();
        if (playerSettings.GetPersistentState() == IPersistable.PersistentState.UPDATED)
            return;

        byte[] uiSettings = playerSettings.GetUiSettings();
        byte[] shortcuts = playerSettings.GetShortcuts();
        byte[] houseBuddies = playerSettings.GetHouseBuddies();
        int display = playerSettings.GetDisplay();
        int deny = playerSettings.GetDeny();

        if (uiSettings != null)
        {
            DB.InsertUpdate("REPLACE INTO player_settings values (?, ?, ?)", new SettingHandler(playerId, 0, uiSettings));
        }

        if (shortcuts != null)
        {
            DB.InsertUpdate("REPLACE INTO player_settings values (?, ?, ?)", new SettingHandler(playerId, 1, shortcuts));
        }

        if (houseBuddies != null)
        {
            DB.InsertUpdate("REPLACE INTO player_settings values (?, ?, ?)", new SettingHandler(playerId, 2, houseBuddies));
        }

        DB.InsertUpdate("REPLACE INTO player_settings values (?, ?, ?)", new SettingHandler(playerId, -1, display));

        DB.InsertUpdate("REPLACE INTO player_settings values (?, ?, ?)", new SettingHandler(playerId, -2, deny));
    }

    private sealed class SettingHandler : IUStH
    {
        private readonly int playerId;
        private readonly int type;
        private readonly object value;

        internal SettingHandler(int playerId, int type, object value)
        {
            this.playerId = playerId;
            this.type = type;
            this.value = value;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            stmt.Parameters.Add(new MySqlParameter { Value = type });
            stmt.Parameters.Add(new MySqlParameter { Value = value });
            stmt.ExecuteNonQuery();
        }
    }
}
