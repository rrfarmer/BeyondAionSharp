using MySqlConnector;

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

	/// <summary>
	/// MySqlConnector always speaks utf8mb4. Connector/J's UTF-8 spellings are normalized to that
	/// explicit connection-string value so the JDBC option is not silently discarded.
	/// </summary>
	public string CharacterSet { get; init; } = "utf8mb4";

	/// <summary>
	/// Connector/J serverTimezone/connectionTimeZone value. A non-empty value is applied to each
	/// physical MySQL session by <see cref="Aion.Commons.Database.DatabaseFactory"/>; SERVER or an empty value
	/// leaves the server session unchanged.
	/// </summary>
	public string? ConnectionTimeZone { get; init; }

	/// <summary>Explicit TLS mode translated from Connector/J sslMode or its legacy SSL flags.</summary>
	public MySqlSslMode? SslMode { get; init; }

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
		var parsed = ParseJdbcMysqlUrl(jdbcUrl);
		return new DatabaseOptions
		{
			Server = parsed.Server,
			Port = parsed.Port,
			Database = parsed.Database,
			UserId = loader.Get("database.user", "root"),
			Password = loader.Get("database.password", "root"),
			MaxPoolSize = loader.GetInt("database.connectionpool.connections.max", 5),
			ConnectionTimeout = loader.GetInt("database.connectionpool.timeout", 5000),
			CharacterSet = parsed.CharacterSet,
			ConnectionTimeZone = parsed.ConnectionTimeZone,
			SslMode = parsed.SslMode,
		};
	}

	public static JdbcMySqlUrl ParseJdbcMysqlUrl(string jdbcUrl)
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

		var query = ParseJdbcOptions(uri.Query, jdbcUrl);
		var characterSet = ParseCharacterSet(query);
		var connectionTimeZone = ParseConnectionTimeZone(query);
		var sslMode = ParseSslMode(query);

		return new JdbcMySqlUrl(
			uri.Host,
			uri.IsDefaultPort ? 3306 : uri.Port,
			database,
			characterSet,
			connectionTimeZone,
			sslMode);
	}

	private static Dictionary<string, string> ParseJdbcOptions(string rawQuery, string jdbcUrl)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrEmpty(rawQuery))
			return result;

		foreach (var pair in rawQuery.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
		{
			var separator = pair.IndexOf('=');
			var key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
			var value = Uri.UnescapeDataString(separator < 0 ? string.Empty : pair[(separator + 1)..]);
			if (string.IsNullOrWhiteSpace(key))
				throw new FormatException($"Database URL '{jdbcUrl}' contains an empty JDBC option name.");
			if (!IsSupportedJdbcOption(key))
				throw new FormatException($"Unsupported JDBC database option '{key}' in '{jdbcUrl}'.");
			if (!result.TryAdd(key, value))
				throw new FormatException($"JDBC database option '{key}' is specified more than once in '{jdbcUrl}'.");
		}

		return result;
	}

	private static bool IsSupportedJdbcOption(string key)
	{
		return key.Equals("serverTimezone", StringComparison.OrdinalIgnoreCase)
			|| key.Equals("connectionTimeZone", StringComparison.OrdinalIgnoreCase)
			|| key.Equals("characterEncoding", StringComparison.OrdinalIgnoreCase)
			|| key.Equals("sslMode", StringComparison.OrdinalIgnoreCase)
			|| key.Equals("useSSL", StringComparison.OrdinalIgnoreCase)
			|| key.Equals("requireSSL", StringComparison.OrdinalIgnoreCase)
			|| key.Equals("verifyServerCertificate", StringComparison.OrdinalIgnoreCase);
	}

	private static string ParseCharacterSet(IReadOnlyDictionary<string, string> query)
	{
		if (!query.TryGetValue("characterEncoding", out var encoding))
			return "utf8mb4";

		return encoding.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant() switch
		{
			"UTF8" or "UTF8MB4" => "utf8mb4",
			_ => throw new FormatException(
				$"Unsupported JDBC characterEncoding '{encoding}'. MySqlConnector 2.5 uses utf8mb4; only UTF-8/UTF8/utf8mb4 are equivalent."),
		};
	}

	private static string? ParseConnectionTimeZone(IReadOnlyDictionary<string, string> query)
	{
		var hasServerTimeZone = query.TryGetValue("serverTimezone", out var serverTimeZone);
		var hasConnectionTimeZone = query.TryGetValue("connectionTimeZone", out var connectionTimeZone);
		if (hasServerTimeZone && hasConnectionTimeZone)
			throw new FormatException("Specify only one of JDBC serverTimezone or connectionTimeZone; both describe the same connection setting.");

		var value = hasConnectionTimeZone ? connectionTimeZone : serverTimeZone;
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	private static MySqlSslMode? ParseSslMode(IReadOnlyDictionary<string, string> query)
	{
		var hasSslMode = query.TryGetValue("sslMode", out var sslMode);
		var hasLegacySsl = query.ContainsKey("useSSL") || query.ContainsKey("requireSSL") || query.ContainsKey("verifyServerCertificate");
		if (hasSslMode && hasLegacySsl)
			throw new FormatException("Do not combine JDBC sslMode with legacy useSSL/requireSSL/verifyServerCertificate options.");

		if (hasSslMode)
		{
			return sslMode!.Trim().Replace('-', '_').ToUpperInvariant() switch
			{
				"DISABLED" => MySqlSslMode.Disabled,
				"PREFERRED" => MySqlSslMode.Preferred,
				"REQUIRED" => MySqlSslMode.Required,
				"VERIFY_CA" => MySqlSslMode.VerifyCA,
				"VERIFY_IDENTITY" => MySqlSslMode.VerifyFull,
				_ => throw new FormatException($"Unsupported JDBC sslMode '{sslMode}'."),
			};
		}

		var useSsl = ReadOptionalBoolean(query, "useSSL");
		var requireSsl = ReadOptionalBoolean(query, "requireSSL");
		var verifyServerCertificate = ReadOptionalBoolean(query, "verifyServerCertificate");
		if (useSsl == false && (requireSsl == true || verifyServerCertificate == true))
			throw new FormatException("JDBC useSSL=false conflicts with requireSSL=true or verifyServerCertificate=true.");

		if (verifyServerCertificate == true)
			return MySqlSslMode.VerifyCA;
		if (requireSsl == true)
			return MySqlSslMode.Required;
		if (useSsl == true)
			return MySqlSslMode.Preferred;
		if (useSsl == false)
			return MySqlSslMode.Disabled;
		return null;
	}

	private static bool? ReadOptionalBoolean(IReadOnlyDictionary<string, string> query, string key)
	{
		if (!query.TryGetValue(key, out var value))
			return null;
		if (bool.TryParse(value, out var parsed))
			return parsed;
		throw new FormatException($"JDBC option '{key}' must be true or false, but was '{value}'.");
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

public sealed record JdbcMySqlUrl(
	string Server,
	int Port,
	string Database,
	string CharacterSet,
	string? ConnectionTimeZone,
	MySqlSslMode? SslMode)
{
	public void Deconstruct(out string server, out int port, out string database)
	{
		server = Server;
		port = Port;
		database = Database;
	}
}
