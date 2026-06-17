using System.Net;
using Aion.Commons.Configuration;

namespace Aion.LoginServer.Configuration;

public sealed class LoginServerOptions
{
	public IPEndPoint ClientEndPoint { get; init; } = new(IPAddress.Any, 2106);

	public IPEndPoint GameServerEndPoint { get; init; } = new(IPAddress.Any, 9014);

	public int MaxClientConnections { get; init; } = 10000;

	public int MaxGameServerConnections { get; init; } = 100;

	public int NioReadWriteThreads { get; init; }

	public bool AutoCreateAccounts { get; init; } = true;

	public int LoginTryBeforeBan { get; init; } = 5;

	public int WrongLoginBanMinutes { get; init; } = 15;

	public bool BruteForceProtectionEnabled { get; init; } = true;

	public bool LogGameServerLogins { get; init; }

	public string ExternalAuthUrl { get; init; } = string.Empty;

	public bool UseExternalAuth => !string.IsNullOrWhiteSpace(ExternalAuthUrl);

	/// <summary>
	/// Java parity: builds the options object from the faithful [Property] holder <see cref="Config"/>, which is bound
	/// by <see cref="ConfigurableProcessor"/> over the exact Java load order (config/main + config/network defaults,
	/// then myls.properties overrides) — same key/default/precedence contract as Java's Config.load(). The IPEndPoint
	/// fields parse the holder's verbatim "host:port" string (the socket-layer convention). MaxClient/GameServer
	/// connections are not Java config keys and keep their C# defaults.
	/// </summary>
	public static LoginServerOptions LoadFromJavaConfig(string startDirectory)
	{
		Config.Load(startDirectory);

		return new LoginServerOptions
		{
			ClientEndPoint = ParseEndPoint(Config.CLIENT_SOCKET_ADDRESS),
			GameServerEndPoint = ParseEndPoint(Config.GAMESERVER_SOCKET_ADDRESS),
			AutoCreateAccounts = Config.ACCOUNT_AUTO_CREATION,
			LoginTryBeforeBan = Config.LOGIN_TRY_BEFORE_BAN,
			WrongLoginBanMinutes = Config.WRONG_LOGIN_BAN_TIME,
			NioReadWriteThreads = Config.NIO_READ_WRITE_THREADS,
			BruteForceProtectionEnabled = Config.ENABLE_BRUTEFORCE_PROTECTION,
			LogGameServerLogins = Config.LOG_LOGINS,
			ExternalAuthUrl = Config.EXTERNAL_AUTH_URL,
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
}
