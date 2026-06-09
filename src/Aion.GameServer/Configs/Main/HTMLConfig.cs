namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/HTMLConfig (lord_rex). SCREAMING_SNAKE field names + @Property defaults.</summary>
public static class HTMLConfig
{
    /// <summary>Enable HTML Welcome Message. Key: gameserver.html.welcome.enable</summary>
    public static bool ENABLE_HTML_WELCOME = false;

    /// <summary>Enable HTML Guide Message. Key: gameserver.html.guides.enable</summary>
    public static bool ENABLE_GUIDES = false;

    /// <summary>Html files directory. Key: gameserver.html.root</summary>
    public static string HTML_ROOT = "./data/static_data/HTML/";

    /// <summary>Html cache directory. Key: gameserver.html.cache.file</summary>
    public static string HTML_CACHE_FILE = "./cache/html.cache";

    /// <summary>Encoding. Key: gameserver.html.encoding</summary>
    public static string HTML_ENCODING = "UTF-8";
}
