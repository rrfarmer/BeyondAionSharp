namespace Aion.GameServer.Configs.Network;

/// <summary>
/// Java parity: configs/network/NetworkConfig.
/// Fields keep Java names + @Property default values (loaded from properties when the config framework is wired).
/// </summary>
public static class NetworkConfig
{
    // Address fields (Java InetSocketAddress) — held as their default "host:port" strings until the socket
    // config layer parses them; not referenced by gameplay, kept for parity.
    public static string CLIENT_CONNECT_ADDRESS = "0.0.0.0:7777";
    public static string CLIENT_SOCKET_ADDRESS = "0.0.0.0:7777";
    public static string LOGIN_ADDRESS = "localhost:9014";
    public static string CHAT_ADDRESS = "localhost:9021";
    public static string CHAT_PASSWORD = "";

    public static int GAMESERVER_ID = 1;
    public static string LOGIN_PASSWORD = "";
    public static int MIN_ACCESS_LEVEL = 0;
    public static int MAX_ONLINE_PLAYERS = 100;
    public static int NIO_READ_WRITE_THREADS = 1;
    public static bool NIO_READ_WRITE_THREADS_UNSAFE_ALLOW = false;
    public static int PACKET_PROCESSOR_MIN_THREADS = 4;
    public static int PACKET_PROCESSOR_MAX_THREADS = 4;
    public static int PACKET_PROCESSOR_THREAD_KILL_THRESHOLD = 3;
    public static int PACKET_PROCESSOR_THREAD_SPAWN_THRESHOLD = 50;
    public static bool LOG_UNKNOWN_PACKETS = false;
    public static bool LOG_IGNORED_PACKETS = false;
    public static bool ENABLE_FLOOD_CONNECTIONS = false;
    public static int Flood_Tick = 1000;
    public static int Flood_SWARN = 10;
    public static int Flood_SReject = 20;
    public static int Flood_STick = 10;
    public static int Flood_LWARN = 30;
    public static int Flood_LReject = 60;
    public static int Flood_LTick = 60;
}
