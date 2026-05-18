using System.Net;
using Aion.Commons.Configuration;

namespace Aion.LoginServer.Configuration;

public sealed class LoginServerOptions
{
	public IPEndPoint ClientEndPoint { get; init; } = new(IPAddress.Any, 2106);

	public IPEndPoint GameServerEndPoint { get; init; } = new(IPAddress.Any, 9014);

	public int MaxClientConnections { get; init; } = 10000;

	public int MaxGameServerConnections { get; init; } = 100;

	public bool AutoCreateAccounts { get; init; } = true;

	public bool BruteForceProtectionEnabled { get; init; } = true;

	public static LoginServerOptions LoadFromJavaConfig(string startDirectory)
	{
		var loader = new ConfigLoader();
		var repoRoot = FindRepoRoot(startDirectory);
		if (repoRoot != null)
		{
			var configRoot = Path.Combine(repoRoot, "login-server", "config");
			loader.LoadCascading(
				Path.Combine(configRoot, "main"),
				Path.Combine(configRoot, "network"),
				Path.Combine(configRoot, "myls.properties"));
		}

		return new LoginServerOptions
		{
			ClientEndPoint = ParseEndPoint(loader.Get("loginserver.network.client.socket_address", "0.0.0.0:2106")),
			GameServerEndPoint = ParseEndPoint(loader.Get("loginserver.network.gameserver.socket_address", "0.0.0.0:9014")),
			AutoCreateAccounts = loader.GetBool("loginserver.accounts.autocreate", true),
			BruteForceProtectionEnabled = loader.GetBool("loginserver.server.bruteforceprotector", true),
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
			if (Directory.Exists(Path.Combine(directory.FullName, "login-server", "config")))
				return directory.FullName;
			directory = directory.Parent;
		}

		return null;
	}
}
