using System.Net;
using Aion.Commons.Configuration;

namespace Aion.ChatServer.Configuration;

public sealed class ChatServerOptions
{
	public IPEndPoint ClientEndPoint { get; init; } = new(IPAddress.Any, 10241);

	public IPEndPoint ClientConnectEndPoint { get; init; } = new(IPAddress.Any, 10241);

	public IPEndPoint GameServerEndPoint { get; init; } = new(IPAddress.Any, 9021);

	public string GameServerPassword { get; init; } = string.Empty;

	public int NioReadWriteThreads { get; init; } = 1;

	public bool LogChannelRequests { get; init; }

	public bool LogInvalidChannels { get; init; }

	public bool LogChat { get; init; }

	public bool LogChatToDatabase { get; init; }

	/// <summary>
	/// Non-Java C# extension (no corresponding Java key). Java's chat server has no keyword filter; this stays empty
	/// unless a host explicitly populates it, so it never alters Java-parity behavior.
	/// </summary>
	public IReadOnlyCollection<string> FilteredKeywords { get; init; } = [];

	/// <summary>
	/// Java parity: builds the options object from the faithful [Property] holder <see cref="Config"/>, which is bound
	/// by <see cref="ConfigurableProcessor"/> over the exact Java load order (config/main + config/network defaults,
	/// then mycs.properties overrides) — same key/default/precedence contract as Java's Config.load(), including the
	/// processor's ${...} placeholder resolution (connect_address = ${...socket_address}). The IPEndPoint fields parse
	/// the holder's verbatim "host:port" string (the socket-layer convention).
	/// </summary>
	public static ChatServerOptions LoadFromJavaConfig(string startDirectory)
	{
		Config.Load(startDirectory);

		return new ChatServerOptions
		{
			ClientEndPoint = ParseEndPoint(Config.CLIENT_SOCKET_ADDRESS),
			ClientConnectEndPoint = ParseEndPoint(Config.CLIENT_CONNECT_ADDRESS),
			GameServerEndPoint = ParseEndPoint(Config.GAMESERVER_SOCKET_ADDRESS),
			GameServerPassword = Config.GAMESERVER_PASSWORD,
			NioReadWriteThreads = Config.NIO_READ_WRITE_THREADS,
			LogChannelRequests = Config.LOG_CHANNEL_REQUEST,
			LogInvalidChannels = Config.LOG_CHANNEL_INVALID,
			LogChat = Config.LOG_CHAT,
			LogChatToDatabase = Config.LOG_CHAT_TO_DB,
			FilteredKeywords = [],
		};
	}

	/// <summary>
	/// Java parity: chat-server uses the commons DatabaseConfig (database.url/user/password/pool) loaded over the same
	/// cascade. Mirrors <see cref="DatabaseOptions.LoadFromJavaConfig"/> but rooted at chat-server/config so the
	/// highest-precedence mycs.properties override wins, exactly as Java's PropertiesUtils applies it.
	/// </summary>
	public static DatabaseOptions LoadDatabaseOptionsFromJavaConfig(string startDirectory)
	{
		var loader = new ConfigLoader();
		var repoRoot = FindRepoRoot(startDirectory);
		if (repoRoot != null)
		{
			var configRoot = Path.Combine(repoRoot, "chat-server", "config");
			loader.LoadCascading(
				Path.Combine(configRoot, "main"),
				Path.Combine(configRoot, "network"),
				Path.Combine(configRoot, "mycs.properties"));
		}

		var jdbcUrl = loader.Get("database.url", "jdbc:mysql://localhost:3306/aion_cs");
		var (server, port, database) = DatabaseOptions.ParseJdbcMysqlUrl(jdbcUrl);
		return new DatabaseOptions
		{
			Server = server,
			Port = port,
			Database = database,
			UserId = loader.Get("database.user", "root"),
			Password = loader.Get("database.password", string.Empty),
			MaxPoolSize = loader.GetInt("database.connectionpool.connections.max", 5),
			ConnectionTimeout = loader.GetInt("database.connectionpool.timeout", 5000),
		};
	}

	public static IPEndPoint ParseEndPoint(string value)
	{
		var separator = value.LastIndexOf(':');
		if (separator <= 0 || separator == value.Length - 1)
			throw new FormatException($"Invalid socket address '{value}'. Expected host:port.");

		var host = value[..separator].Trim();
		var port = int.Parse(value[(separator + 1)..].Trim());
		var address = host == "0.0.0.0" || host == "*"
			? IPAddress.Any
			: Dns.GetHostAddresses(host).First(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

		return new IPEndPoint(address, port);
	}

	private static string? FindRepoRoot(string startDirectory)
	{
		var directory = new DirectoryInfo(startDirectory);
		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "chat-server", "config")))
				return directory.FullName;
			directory = directory.Parent;
		}

		return null;
	}
}
