using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/PunishmentConfig (synchro2). SCREAMING_SNAKE field names + [Property] keys/defaults bound at boot.</summary>
public static class PunishmentConfig
{
    /// <summary>Key: gameserver.punishment.enable</summary>
    [Property(key: "gameserver.punishment.enable", defaultValue: "false")]
    public static bool PUNISHMENT_ENABLE = false;

    /// <summary>Key: gameserver.punishment.type</summary>
    [Property(key: "gameserver.punishment.type", defaultValue: "1")]
    public static int PUNISHMENT_TYPE = 1;

    /// <summary>Key: gameserver.punishment.time</summary>
    [Property(key: "gameserver.punishment.time", defaultValue: "1440")]
    public static int PUNISHMENT_TIME = 1440;
}
