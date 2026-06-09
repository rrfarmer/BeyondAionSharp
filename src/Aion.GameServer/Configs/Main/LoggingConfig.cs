namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/LoggingConfig. SCREAMING_SNAKE field names + @Property defaults.</summary>
public static class LoggingConfig
{
    /// <summary>Key: gameserver.log.audit</summary>
    public static bool LOG_AUDIT = true;

    /// <summary>Key: gameserver.log.craft</summary>
    public static bool LOG_CRAFT = true;

    /// <summary>Key: gameserver.log.gmaudit</summary>
    public static bool LOG_GMAUDIT = true;

    /// <summary>Key: gameserver.log.chats.general</summary>
    public static bool LOG_GENERAL_CHATS = true;

    /// <summary>Key: gameserver.log.chats.private</summary>
    public static bool LOG_PRIVATE_CHATS = false;

    /// <summary>Key: gameserver.log.ingameshop</summary>
    public static bool LOG_INGAMESHOP = false;

    /// <summary>Key: gameserver.log.ingameshop.sql</summary>
    public static bool LOG_INGAMESHOP_SQL = false;

    /// <summary>Key: gameserver.log.item</summary>
    public static bool LOG_ITEM = true;

    /// <summary>Key: gameserver.log.kill</summary>
    public static bool LOG_KILL = false;

    /// <summary>Key: gameserver.log.pl</summary>
    public static bool LOG_PL = false;

    /// <summary>Key: gameserver.log.mail</summary>
    public static bool LOG_MAIL = false;

    /// <summary>Key: gameserver.log.player.exchange</summary>
    public static bool LOG_PLAYER_EXCHANGE = false;

    /// <summary>Key: gameserver.log.broker.exchange</summary>
    public static bool LOG_BROKER_EXCHANGE = false;

    /// <summary>Key: gameserver.log.siege</summary>
    public static bool LOG_SIEGE = false;

    /// <summary>Key: gameserver.log.sysmail</summary>
    public static bool LOG_SYSMAIL = false;

    /// <summary>Key: gameserver.log.auction</summary>
    public static bool LOG_HOUSE_AUCTION = true;

    /// <summary>Key: gameserver.log.tampering</summary>
    public static bool LOG_TAMPERING = false;
}
