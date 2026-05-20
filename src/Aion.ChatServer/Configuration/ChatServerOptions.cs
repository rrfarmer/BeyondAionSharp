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

	public IReadOnlyCollection<string> FilteredKeywords { get; init; } = [];

	public static ChatServerOptions LoadFromJavaConfig(string startDirectory)
	{
		var loader = LoadProperties(startDirectory);
		var clientSocket = GetWithEnvironment(loader, "chatserver.network.client.socket_address", "0.0.0.0:10241");
		var clientConnect = ResolvePropertyReference(
			GetWithEnvironment(loader, "chatserver.network.client.connect_address", clientSocket),
			loader,
			clientSocket);

		return new ChatServerOptions
		{
			ClientEndPoint = ParseEndPoint(clientSocket),
			ClientConnectEndPoint = ParseEndPoint(clientConnect),
			GameServerEndPoint = ParseEndPoint(GetWithEnvironment(loader, "chatserver.network.gameserver.socket_address", "0.0.0.0:9021")),
			GameServerPassword = GetWithEnvironment(loader, "chatserver.network.gameserver.password", string.Empty),
			NioReadWriteThreads = GetIntWithEnvironment(loader, "chatserver.network.nio.threads", 1),
			LogChannelRequests = GetBoolWithEnvironment(loader, "chatserver.log.channel.request", false),
			LogInvalidChannels = GetBoolWithEnvironment(loader, "chatserver.log.channel.invalid", false),
			LogChat = GetBoolWithEnvironment(loader, "chatserver.log.chat", false),
			LogChatToDatabase = GetBoolWithEnvironment(loader, "chatserver.log.chat_to_db", false),
			FilteredKeywords = GetListWithEnvironment(loader, "chatserver.chat.filter.keywords"),
		};
	}

	public static DatabaseOptions LoadDatabaseOptionsFromJavaConfig(string startDirectory)
	{
		var loader = LoadProperties(startDirectory);
		var jdbcUrl = GetWithEnvironment(loader, "database.url", "jdbc:mysql://localhost:3306/aion_cs");
		var (server, port, database) = DatabaseOptions.ParseJdbcMysqlUrl(jdbcUrl);
		return new DatabaseOptions
		{
			Server = server,
			Port = port,
			Database = database,
			UserId = GetWithEnvironment(loader, "database.user", "root"),
			Password = GetWithEnvironment(loader, "database.password", string.Empty),
			MaxPoolSize = GetIntWithEnvironment(loader, "database.connectionpool.connections.max", 5),
			ConnectionTimeout = GetIntWithEnvironment(loader, "database.connectionpool.timeout", 5000),
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

	private static ConfigLoader LoadProperties(string startDirectory)
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

		return loader;
	}

	private static string GetWithEnvironment(ConfigLoader loader, string key, string defaultValue)
	{
		return Environment.GetEnvironmentVariable(key)
			?? Environment.GetEnvironmentVariable(key.ToUpperInvariant().Replace('.', '_'))
			?? loader.Get(key, defaultValue);
	}

	private static int GetIntWithEnvironment(ConfigLoader loader, string key, int defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue.ToString());
		return int.TryParse(value, out var parsed) ? parsed : defaultValue;
	}

	private static bool GetBoolWithEnvironment(ConfigLoader loader, string key, bool defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue.ToString());
		return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
	}

	private static IReadOnlyCollection<string> GetListWithEnvironment(ConfigLoader loader, string key)
	{
		var value = GetWithEnvironment(loader, key, string.Empty);
		return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
	}

	private static string ResolvePropertyReference(string value, ConfigLoader loader, string defaultValue)
	{
		if (!value.StartsWith("${", StringComparison.Ordinal) || !value.EndsWith('}'))
			return value;

		var key = value[2..^1];
		return GetWithEnvironment(loader, key, defaultValue);
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
