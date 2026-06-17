using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/HTMLConfig (lord_rex). SCREAMING_SNAKE field names + [Property] keys/defaults bound at boot.</summary>
public static class HTMLConfig
{
    /// <summary>Enable HTML Welcome Message. Key: gameserver.html.welcome.enable</summary>
    [Property(key: "gameserver.html.welcome.enable", defaultValue: "false")]
    public static bool ENABLE_HTML_WELCOME = false;

    /// <summary>Enable HTML Guide Message. Key: gameserver.html.guides.enable</summary>
    [Property(key: "gameserver.html.guides.enable", defaultValue: "false")]
    public static bool ENABLE_GUIDES = false;

    /// <summary>Html files directory. Key: gameserver.html.root</summary>
    [Property(key: "gameserver.html.root", defaultValue: "./data/static_data/HTML/")]
    public static string HTML_ROOT = "./data/static_data/HTML/";

    /// <summary>Html cache directory. Key: gameserver.html.cache.file</summary>
    [Property(key: "gameserver.html.cache.file", defaultValue: "./cache/html.cache")]
    public static string HTML_CACHE_FILE = "./cache/html.cache";

    /// <summary>Encoding. Key: gameserver.html.encoding</summary>
    [Property(key: "gameserver.html.encoding", defaultValue: "UTF-8")]
    public static string HTML_ENCODING = "UTF-8";
}
