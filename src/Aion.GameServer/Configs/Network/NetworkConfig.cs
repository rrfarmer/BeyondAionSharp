using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Network;

/// <summary>
/// Java parity: configs/network/NetworkConfig. [Property] keys/defaults bound at boot by
/// <see cref="ConfigurableProcessor"/> from config/network/network.properties (operator overrides honored).
/// <para>
/// Java InetSocketAddress address fields are held here as their "host:port" string (parsed by the socket layer
/// when consumed); the StringTransformer binds the raw value verbatim. The Java key/default contract is preserved
/// exactly. The InetSocketAddressTransformer + the Config.load() local-IP discovery for CLIENT_CONNECT_ADDRESS
/// remain deferred infra (see Full-Parity-Backlog §C).
/// </para>
/// </summary>
public static class NetworkConfig
{
    /// <summary>Address where Aion clients will attempt to connect (host/IP). Key: gameserver.network.client.connect_address</summary>
    [Property(key: "gameserver.network.client.connect_address", defaultValue: "0.0.0.0:7777")]
    public static string CLIENT_CONNECT_ADDRESS = "0.0.0.0:7777";

    /// <summary>Local address where GS listens for Aion client connections. Key: gameserver.network.client.socket_address</summary>
    [Property(key: "gameserver.network.client.socket_address", defaultValue: "0.0.0.0:7777")]
    public static string CLIENT_SOCKET_ADDRESS = "0.0.0.0:7777";

    /// <summary>Address of the login server. Key: gameserver.network.login.address</summary>
    [Property(key: "gameserver.network.login.address", defaultValue: "localhost:9014")]
    public static string LOGIN_ADDRESS = "localhost:9014";

    /// <summary>Address of the chat server. Key: gameserver.network.chat.address</summary>
    [Property(key: "gameserver.network.chat.address", defaultValue: "localhost:9021")]
    public static string CHAT_ADDRESS = "localhost:9021";

    /// <summary>Password for this GameServer ID for authentication at ChatServer. Key: gameserver.network.chat.password</summary>
    [Property(key: "gameserver.network.chat.password", defaultValue: "")]
    public static string CHAT_PASSWORD = "";

    /// <summary>GameServer id that this GameServer requests at LoginServer. Key: gameserver.network.login.gsid</summary>
    [Property(key: "gameserver.network.login.gsid", defaultValue: "1")]
    public static int GAMESERVER_ID = 1;

    /// <summary>Password for this GameServer ID for authentication at LoginServer. Key: gameserver.network.login.password</summary>
    [Property(key: "gameserver.network.login.password", defaultValue: "")]
    public static string LOGIN_PASSWORD = "";

    /// <summary>Minimum required access level for accounts trying to connect (maintenance mode). Key: gameserver.network.login.min_accesslevel</summary>
    [Property(key: "gameserver.network.login.min_accesslevel", defaultValue: "0")]
    public static int MIN_ACCESS_LEVEL = 0;

    /// <summary>Max allowed online players. Key: gameserver.network.login.max_players</summary>
    [Property(key: "gameserver.network.login.max_players", defaultValue: "100")]
    public static int MAX_ONLINE_PLAYERS = 100;

    /// <summary>Number of threads dedicated to io read &amp; write. Key: gameserver.network.nio.threads</summary>
    [Property(key: "gameserver.network.nio.threads", defaultValue: "1")]
    public static int NIO_READ_WRITE_THREADS = 1;

    /// <summary>Allows values &gt; 1 for nio.threads (dev/debug only; the game server is not thread-safe). Key: gameserver.network.nio.threads.unsafe.allow</summary>
    [Property(key: "gameserver.network.nio.threads.unsafe.allow", defaultValue: "false")]
    public static bool NIO_READ_WRITE_THREADS_UNSAFE_ALLOW;

    /// <summary>Minimum threads used to execute aion client packets. Key: gameserver.network.packet.processor.threads.min</summary>
    [Property(key: "gameserver.network.packet.processor.threads.min", defaultValue: "4")]
    public static int PACKET_PROCESSOR_MIN_THREADS = 4;

    /// <summary>Maximum threads used to execute aion client packets. Key: gameserver.network.packet.processor.threads.max</summary>
    [Property(key: "gameserver.network.packet.processor.threads.max", defaultValue: "4")]
    public static int PACKET_PROCESSOR_MAX_THREADS = 4;

    /// <summary>Threshold deciding when extra threads are not needed. Key: gameserver.network.packet.processor.threshold.kill</summary>
    [Property(key: "gameserver.network.packet.processor.threshold.kill", defaultValue: "3")]
    public static int PACKET_PROCESSOR_THREAD_KILL_THRESHOLD = 3;

    /// <summary>Threshold deciding when extra threads should be spawned. Key: gameserver.network.packet.processor.threshold.spawn</summary>
    [Property(key: "gameserver.network.packet.processor.threshold.spawn", defaultValue: "50")]
    public static int PACKET_PROCESSOR_THREAD_SPAWN_THRESHOLD = 50;

    /// <summary>If aion client packets unknown by the server should be logged. Key: gameserver.network.logging.unknown_packets</summary>
    [Property(key: "gameserver.network.logging.unknown_packets", defaultValue: "false")]
    public static bool LOG_UNKNOWN_PACKETS;

    /// <summary>If ignored aion client packets (invalid connection state) should be logged. Key: gameserver.network.logging.ignored_packets</summary>
    [Property(key: "gameserver.network.logging.ignored_packets", defaultValue: "false")]
    public static bool LOG_IGNORED_PACKETS;

    /// <summary>Key: gameserver.network.flood.connections</summary>
    [Property(key: "gameserver.network.flood.connections", defaultValue: "false")]
    public static bool ENABLE_FLOOD_CONNECTIONS;

    /// <summary>Key: gameserver.network.flood.tick</summary>
    [Property(key: "gameserver.network.flood.tick", defaultValue: "1000")]
    public static int Flood_Tick = 1000;

    /// <summary>Key: gameserver.network.flood.short.warn</summary>
    [Property(key: "gameserver.network.flood.short.warn", defaultValue: "10")]
    public static int Flood_SWARN = 10;

    /// <summary>Key: gameserver.network.flood.short.reject</summary>
    [Property(key: "gameserver.network.flood.short.reject", defaultValue: "20")]
    public static int Flood_SReject = 20;

    /// <summary>Key: gameserver.network.flood.short.tick</summary>
    [Property(key: "gameserver.network.flood.short.tick", defaultValue: "10")]
    public static int Flood_STick = 10;

    /// <summary>Key: gameserver.network.flood.long.warn</summary>
    [Property(key: "gameserver.network.flood.long.warn", defaultValue: "30")]
    public static int Flood_LWARN = 30;

    /// <summary>Key: gameserver.network.flood.long.reject</summary>
    [Property(key: "gameserver.network.flood.long.reject", defaultValue: "60")]
    public static int Flood_LReject = 60;

    /// <summary>Key: gameserver.network.flood.long.tick</summary>
    [Property(key: "gameserver.network.flood.long.tick", defaultValue: "60")]
    public static int Flood_LTick = 60;
}
