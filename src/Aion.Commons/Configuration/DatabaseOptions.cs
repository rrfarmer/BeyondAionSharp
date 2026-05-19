namespace Aion.Commons.Configuration;

public sealed class DatabaseOptions
{
	public string Server { get; init; } = "localhost";

	public int Port { get; init; } = 3306;

	public string Database { get; init; } = "aion_ls";

	public string UserId { get; init; } = "root";

	public string Password { get; init; } = "root";

	public int MaxPoolSize { get; init; } = 5;

	public int ConnectionTimeout { get; init; } = 5000;

	public static DatabaseOptions LoadFromJavaConfig(string startDirectory)
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

		var jdbcUrl = loader.Get("database.url", "jdbc:mysql://localhost:3306/aion_ls");
		var (server, port, database) = ParseJdbcMysqlUrl(jdbcUrl);
		return new DatabaseOptions
		{
			Server = server,
			Port = port,
			Database = database,
			UserId = loader.Get("database.user", "root"),
			Password = loader.Get("database.password", "root"),
			MaxPoolSize = loader.GetInt("database.connectionpool.connections.max", 5),
			ConnectionTimeout = loader.GetInt("database.connectionpool.timeout", 5000),
		};
	}

	public static (string Server, int Port, string Database) ParseJdbcMysqlUrl(string jdbcUrl)
	{
		const string prefix = "jdbc:";
		var uriText = jdbcUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
			? jdbcUrl[prefix.Length..]
			: jdbcUrl;

		var uri = new Uri(uriText, UriKind.Absolute);
		if (!string.Equals(uri.Scheme, "mysql", StringComparison.OrdinalIgnoreCase))
			throw new FormatException($"Unsupported database URL scheme '{uri.Scheme}'. Expected mysql.");

		var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
		if (string.IsNullOrWhiteSpace(database))
			throw new FormatException($"Database URL '{jdbcUrl}' does not contain a database name.");

		return (uri.Host, uri.IsDefaultPort ? 3306 : uri.Port, database);
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
