using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/LoggingConfig. SCREAMING_SNAKE field names + [Property] keys/defaults bound at boot.</summary>
public static class LoggingConfig
{
    /// <summary>Key: gameserver.log.audit</summary>
    [Property(key: "gameserver.log.audit", defaultValue: "true")]
    public static bool LOG_AUDIT = true;

    /// <summary>Key: gameserver.log.craft</summary>
    [Property(key: "gameserver.log.craft", defaultValue: "true")]
    public static bool LOG_CRAFT = true;

    /// <summary>Key: gameserver.log.gmaudit</summary>
    [Property(key: "gameserver.log.gmaudit", defaultValue: "true")]
    public static bool LOG_GMAUDIT = true;

    /// <summary>Key: gameserver.log.chats.general</summary>
    [Property(key: "gameserver.log.chats.general", defaultValue: "true")]
    public static bool LOG_GENERAL_CHATS = true;

    /// <summary>Key: gameserver.log.chats.private</summary>
    [Property(key: "gameserver.log.chats.private", defaultValue: "false")]
    public static bool LOG_PRIVATE_CHATS = false;

    /// <summary>Key: gameserver.log.item</summary>
    [Property(key: "gameserver.log.item", defaultValue: "true")]
    public static bool LOG_ITEM = true;

    /// <summary>Key: gameserver.log.kill</summary>
    [Property(key: "gameserver.log.kill", defaultValue: "false")]
    public static bool LOG_KILL = false;

    /// <summary>Key: gameserver.log.pl</summary>
    [Property(key: "gameserver.log.pl", defaultValue: "false")]
    public static bool LOG_PL = false;

    /// <summary>Key: gameserver.log.mail</summary>
    [Property(key: "gameserver.log.mail", defaultValue: "false")]
    public static bool LOG_MAIL = false;

    /// <summary>Key: gameserver.log.player.exchange</summary>
    [Property(key: "gameserver.log.player.exchange", defaultValue: "false")]
    public static bool LOG_PLAYER_EXCHANGE = false;

    /// <summary>Key: gameserver.log.broker.exchange</summary>
    [Property(key: "gameserver.log.broker.exchange", defaultValue: "false")]
    public static bool LOG_BROKER_EXCHANGE = false;

    /// <summary>Key: gameserver.log.siege</summary>
    [Property(key: "gameserver.log.siege", defaultValue: "false")]
    public static bool LOG_SIEGE = false;

    /// <summary>Key: gameserver.log.sysmail</summary>
    [Property(key: "gameserver.log.sysmail", defaultValue: "false")]
    public static bool LOG_SYSMAIL = false;

    /// <summary>Key: gameserver.log.auction</summary>
    [Property(key: "gameserver.log.auction", defaultValue: "true")]
    public static bool LOG_HOUSE_AUCTION = true;

    /// <summary>Key: gameserver.log.tampering</summary>
    [Property(key: "gameserver.log.tampering", defaultValue: "false")]
    public static bool LOG_TAMPERING = false;
}
