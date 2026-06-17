using System.Collections.Generic;
using Aion.Commons.Configs;
using Aion.Commons.Configuration;

namespace Aion.ChatServer.Configuration;

/// <summary>
/// Java parity: chat-server configs/Config (ATracer) + main/LoggingConfig + network/NetworkConfig. The aggregate
/// [Property] holder for the chat server, bound at boot by <see cref="ConfigurableProcessor"/> over the exact Java
/// load order (config/main/* + config/network/* defaults, then config/mycs.properties overrides), processing
/// CommonsConfig + LoggingConfig + DatabaseConfig + NetworkConfig together — exactly as Java's Config.load().
/// <para>
/// Keys/defaults/precedence are copied VERBATIM from Java (hard contract). Java InetSocketAddress address fields are
/// held here as their "host:port" string (parsed by the socket layer when consumed), the same convention the
/// game-server and login-server NetworkConfig holders use; the StringTransformer binds the raw value verbatim and the
/// processor resolves any ${...} placeholder (e.g. connect_address = ${...socket_address}) before binding. The Java
/// NetworkConfig.CLIENT_CONNECT_ADDRESS local-IPv4 discovery (Config.load's isAnyLocalAddress branch +
/// NetworkUtils.findLocalIPv4) and the removePropertiesUsedInLogbackXml unknown-key filtering remain deferred infra
/// (network/logging-only; sanctioned).
/// </para>
/// </summary>
public static class Config
{
    // ---- main/LoggingConfig ----

    /// <summary>Log requests to new channels.</summary>
    [Property(key: "chatserver.log.channel.request", defaultValue: "false")]
    public static bool LOG_CHANNEL_REQUEST = false;

    /// <summary>Log requests to invalid channels.</summary>
    [Property(key: "chatserver.log.channel.invalid", defaultValue: "false")]
    public static bool LOG_CHANNEL_INVALID = false;

    /// <summary>Log Chat.</summary>
    [Property(key: "chatserver.log.chat", defaultValue: "false")]
    public static bool LOG_CHAT = false;

    /// <summary>Log Chat and Save to Database.</summary>
    [Property(key: "chatserver.log.chat_to_db", defaultValue: "false")]
    public static bool LOG_CHAT_TO_DB = false;

    // ---- network/NetworkConfig ----

    /// <summary>Address where Aion clients will attempt to connect to (host/domain name or IP), held as "host:port".</summary>
    [Property(key: "chatserver.network.client.connect_address", defaultValue: "0.0.0.0:10241")]
    public static string CLIENT_CONNECT_ADDRESS = "0.0.0.0:10241";

    /// <summary>Local address where CS will listen for Aion client connections (0.0.0.0 = bind any local IP).</summary>
    [Property(key: "chatserver.network.client.socket_address", defaultValue: "0.0.0.0:10241")]
    public static string CLIENT_SOCKET_ADDRESS = "0.0.0.0:10241";

    /// <summary>Local address where CS will listen for GS connections (0.0.0.0 = bind any local IP).</summary>
    [Property(key: "chatserver.network.gameserver.socket_address", defaultValue: "0.0.0.0:9021")]
    public static string GAMESERVER_SOCKET_ADDRESS = "0.0.0.0:9021";

    /// <summary>Password for GS authentication.</summary>
    [Property(key: "chatserver.network.gameserver.password", defaultValue: "")]
    public static string GAMESERVER_PASSWORD = "";

    /// <summary>
    /// Number of threads dedicated to be doing io read &amp; write. There is always 1 acceptor thread. If value is &lt; 1 -
    /// acceptor thread will also handle read &amp; write. If value is &gt; 0 - there will be given amount of read &amp; write
    /// threads + 1 acceptor thread.
    /// </summary>
    [Property(key: "chatserver.network.nio.threads", defaultValue: "1")]
    public static int NIO_READ_WRITE_THREADS = 1;

    /// <summary>
    /// Java parity: Config.loadProperties() — cascade config/main/* + config/network/* defaults, then mycs.properties.
    /// </summary>
    public static JavaProperties LoadProperties(string startDirectory)
    {
        var defaults = new JavaProperties();
        var repoRoot = FindRepoRoot(startDirectory);
        JavaProperties properties;
        if (repoRoot != null)
        {
            var configRoot = System.IO.Path.Combine(repoRoot, "chat-server", "config");
            defaults.LoadFromDirectory(System.IO.Path.Combine(configRoot, "main"), false);
            defaults.LoadFromDirectory(System.IO.Path.Combine(configRoot, "network"), false);
            properties = JavaProperties.Load(System.IO.Path.Combine(configRoot, "mycs.properties"), defaults);
        }
        else
        {
            properties = new JavaProperties(defaults);
        }

        return properties;
    }

    /// <summary>
    /// Java parity: Config.load() — process the loaded properties onto CommonsConfig + LoggingConfig + DatabaseConfig +
    /// NetworkConfig (here unified into this holder + CommonsConfig + DatabaseConfig). Returns the unused-key set (Java
    /// logs/filters these via removePropertiesUsedInLogbackXml; logging-only, deferred). Mirrors the Java load order.
    /// </summary>
    public static ISet<string> Load(string startDirectory)
    {
        JavaProperties properties = LoadProperties(startDirectory);
        return ConfigurableProcessor.Process(properties, typeof(CommonsConfig), typeof(Config), typeof(DatabaseConfig));
    }

    /// <summary>Java parity: Config.load(properties) test/host entrypoint — process explicit properties.</summary>
    public static ISet<string> LoadFrom(JavaProperties properties)
        => ConfigurableProcessor.Process(properties, typeof(CommonsConfig), typeof(Config), typeof(DatabaseConfig));

    private static string? FindRepoRoot(string startDirectory)
    {
        var directory = new System.IO.DirectoryInfo(startDirectory);
        while (directory != null)
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(directory.FullName, "chat-server", "config")))
                return directory.FullName;
            directory = directory.Parent;
        }

        return null;
    }
}
