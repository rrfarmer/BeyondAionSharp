using Aion.Commons.Configuration;

namespace Aion.Commons.Configs;

/// <summary>
/// Java parity: commons/configs/DatabaseConfig (SoulKeeper). This class holds all configuration of database.
/// <para>
/// [Property] keys/defaults are copied verbatim from Java. Both the login-server and chat-server bootstraps merge
/// their config/network/database.properties (and the per-instance my{ls,cs}.properties override) into a
/// <see cref="JavaProperties"/> and call <see cref="ConfigurableProcessor.Process"/> over this holder, so operator
/// overrides (e.g. the checked-in myls.properties pointing database.url at port 3307) bind for real — identical
/// precedence to Java's PropertiesUtils cascade.
/// </para>
/// </summary>
public static class DatabaseConfig
{
    [Property(key: "database.url")]
    public static string? DATABASE_URL;

    [Property(key: "database.user")]
    public static string? DATABASE_USER;

    [Property(key: "database.password")]
    public static string? DATABASE_PASSWORD;

    /// <summary>
    /// Maximum amount of connections kept in connection pool
    /// </summary>
    [Property(key: "database.connectionpool.connections.max", defaultValue: "5")]
    public static int DATABASE_CONNECTIONS_MAX;

    /// <summary>
    /// Maximum wait time when getting a DB connection, before throwing a timeout error
    /// </summary>
    [Property(key: "database.connectionpool.timeout", defaultValue: "5000")]
    public static int DATABASE_TIMEOUT;
}
