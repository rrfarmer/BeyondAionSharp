using System.Collections.Generic;
using Aion.Commons.Configs;
using Aion.Commons.Configuration;

namespace Aion.LoginServer.Configuration;

/// <summary>
/// Java parity: login-server configs/Config (-Nemesiss-, SoulKeeper, Neon). The aggregate [Property] holder for the
/// login server, bound at boot by <see cref="ConfigurableProcessor"/> over the Java load order
/// (config/main/* + config/network/* defaults, then config/myls.properties overrides), processing
/// Config + <see cref="CommonsConfig"/> + <see cref="DatabaseConfig"/> together — exactly as Java's Config.load().
/// <para>
/// Keys/defaults/precedence are copied VERBATIM from Java (hard contract). Java InetSocketAddress address fields are
/// held here as their "host:port" string (parsed by the socket layer when consumed), the same convention the
/// game-server NetworkConfig uses; the StringTransformer binds the raw value verbatim. The Java
/// removePropertiesUsedInLogbackXml unknown-key filtering remains deferred infra (logging-only).
/// </para>
/// </summary>
public static class Config
{
    /// <summary>Local address where LS will listen for Aion client connections (0.0.0.0 = bind any local IP).</summary>
    [Property(key: "loginserver.network.client.socket_address", defaultValue: "0.0.0.0:2106")]
    public static string CLIENT_SOCKET_ADDRESS = "0.0.0.0:2106";

    /// <summary>Local address where LS will listen for GS connections (0.0.0.0 = bind any local IP).</summary>
    [Property(key: "loginserver.network.gameserver.socket_address", defaultValue: "0.0.0.0:9014")]
    public static string GAMESERVER_SOCKET_ADDRESS = "0.0.0.0:9014";

    /// <summary>Number of login tries before ban.</summary>
    [Property(key: "loginserver.network.client.logintrybeforeban", defaultValue: "5")]
    public static int LOGIN_TRY_BEFORE_BAN = 5;

    /// <summary>Ban time in minutes.</summary>
    [Property(key: "loginserver.network.client.bantimeforbruteforcing", defaultValue: "15")]
    public static int WRONG_LOGIN_BAN_TIME = 15;

    /// <summary>
    /// Number of threads dedicated to be doing io read &amp; write. There is always 1 acceptor thread. If value is &lt; 1 -
    /// acceptor thread will also handle read &amp; write. If value is &gt; 0 - there will be given amount of read &amp; write
    /// threads + 1 acceptor thread.
    /// </summary>
    [Property(key: "loginserver.network.nio.threads", defaultValue: "0")]
    public static int NIO_READ_WRITE_THREADS = 0;

    /// <summary>Should server automatically create accounts for users or not?</summary>
    [Property(key: "loginserver.accounts.autocreate", defaultValue: "true")]
    public static bool ACCOUNT_AUTO_CREATION = true;

    /// <summary>URL for external authentication, that is used to receive an JSON encoded string, holding the auth status.</summary>
    [Property(key: "loginserver.accounts.external_auth.url", defaultValue: "")]
    public static string EXTERNAL_AUTH_URL = "";

    /// <summary>Enable\disable brute-force protector from 1 IP on account login.</summary>
    [Property(key: "loginserver.server.bruteforceprotector", defaultValue: "true")]
    public static bool ENABLE_BRUTEFORCE_PROTECTION = true;

    /// <summary>Log successful gameserver logins including connection data to DB.</summary>
    [Property(key: "loginserver.log.logins", defaultValue: "false")]
    public static bool LOG_LOGINS = false;

    /// <summary>Java parity: Config.useExternalAuth().</summary>
    public static bool UseExternalAuth() => !string.IsNullOrWhiteSpace(EXTERNAL_AUTH_URL);

    /// <summary>Java parity: loadProperties() — cascade config/main/* + config/network/* then myls.properties.</summary>
    public static JavaProperties LoadProperties(string startDirectory)
    {
        var defaults = new JavaProperties();
        var repoRoot = FindRepoRoot(startDirectory);
        JavaProperties properties;
        if (repoRoot != null)
        {
            var configRoot = System.IO.Path.Combine(repoRoot, "login-server", "config");
            defaults.LoadFromDirectory(System.IO.Path.Combine(configRoot, "main"), false);
            defaults.LoadFromDirectory(System.IO.Path.Combine(configRoot, "network"), false);
            properties = JavaProperties.Load(System.IO.Path.Combine(configRoot, "myls.properties"), defaults);
        }
        else
        {
            properties = new JavaProperties(defaults);
        }

        return properties;
    }

    /// <summary>
    /// Java parity: Config.load() — process the loaded properties onto Config + CommonsConfig + DatabaseConfig.
    /// Returns the unused-key set (Java logs/filters these; logging-only, deferred). Mirrors the Java load order.
    /// </summary>
    public static ISet<string> Load(string startDirectory)
    {
        JavaProperties properties = LoadProperties(startDirectory);
        return ConfigurableProcessor.Process(properties, typeof(Config), typeof(CommonsConfig), typeof(DatabaseConfig));
    }

    /// <summary>Java parity: Config.load(properties) test/host entrypoint — process explicit properties.</summary>
    public static ISet<string> LoadFrom(JavaProperties properties)
        => ConfigurableProcessor.Process(properties, typeof(Config), typeof(CommonsConfig), typeof(DatabaseConfig));

    private static string? FindRepoRoot(string startDirectory)
    {
        var directory = new System.IO.DirectoryInfo(startDirectory);
        while (directory != null)
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(directory.FullName, "login-server", "config")))
                return directory.FullName;
            directory = directory.Parent;
        }

        return null;
    }
}
