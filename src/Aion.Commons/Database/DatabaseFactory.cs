using System;
using System.Threading.Tasks;
using Aion.Commons.Configuration;
using MySqlConnector;

namespace Aion.Commons.Database
{
	/// <summary>
	/// Database connection factory for parity testing and operation.
	/// Matches Java DatabaseFactory interface.
	/// </summary>
	public class DatabaseFactory
	{
		private static MySqlDataSource? _dataSource;
		private static string? _connectionString;

		/// <summary>
		/// Initialize the connection pool.
		/// </summary>
		public static void Initialize(
			string server = "localhost",
			string userId = "root",
			string password = "root",
			string database = "aion_ls",
			int port = 3306,
			int maxPoolSize = 5,
			int connectionTimeout = 5000
		)
		{
			Initialize(
				new DatabaseOptions
				{
					Server = server,
					UserId = userId,
					Password = password,
					Database = database,
					Port = port,
					MaxPoolSize = maxPoolSize,
					ConnectionTimeout = connectionTimeout,
				});
		}

		public static void Initialize(DatabaseOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			_connectionString = BuildConnectionString(options);

			var builder = new MySqlDataSourceBuilder(_connectionString);
			var sessionTimeZone = TranslateConnectionTimeZone(options.ConnectionTimeZone);
			if (sessionTimeZone != null)
			{
				builder.UseConnectionOpenedCallback(
					(context, cancellationToken) => SetSessionTimeZoneAsync(context.Connection, sessionTimeZone, cancellationToken));
			}

			var previous = _dataSource;
			_dataSource = builder.Build();
			previous?.Dispose();
		}

		/// <summary>
		/// Builds the MySqlConnector string after translating supported Connector/J URL options. JDBC
		/// option names are never copied into this string verbatim.
		/// </summary>
		public static string BuildConnectionString(DatabaseOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			var sessionTimeZone = TranslateConnectionTimeZone(options.ConnectionTimeZone);
			var builder = new MySqlConnectionStringBuilder
			{
				Server = options.Server,
				UserID = options.UserId,
				Password = options.Password,
				Database = options.Database,
				Port = checked((uint)options.Port),
				MaximumPoolSize = checked((uint)options.MaxPoolSize),
				// Hikari's value is milliseconds, while MySqlConnector only accepts whole seconds.
				// Round upward so the C# connection cannot time out before the Java deadline; zero
				// retains both providers' no-timeout sentinel semantics.
				ConnectionTimeout = TranslateConnectionTimeoutSeconds(options.ConnectionTimeout),
				Pooling = true,
				AllowUserVariables = true,
				CharacterSet = options.CharacterSet,
				DateTimeKind = string.Equals(sessionTimeZone, "+00:00", StringComparison.Ordinal)
					? MySqlDateTimeKind.Utc
					: MySqlDateTimeKind.Unspecified,
			};
			if (options.SslMode.HasValue)
				builder.SslMode = options.SslMode.Value;
			return builder.ConnectionString;
		}

		internal static uint TranslateConnectionTimeoutSeconds(int connectionTimeoutMilliseconds)
		{
			if (connectionTimeoutMilliseconds < 0)
				throw new ArgumentOutOfRangeException(
					nameof(connectionTimeoutMilliseconds),
					connectionTimeoutMilliseconds,
					"Database connection timeout must be zero or a positive millisecond value.");
			if (connectionTimeoutMilliseconds == 0)
				return 0;

			return checked((uint)(((long)connectionTimeoutMilliseconds + 999L) / 1000L));
		}

		/// <summary>
		/// Translates Connector/J connectionTimeZone/serverTimezone values to MySQL session values.
		/// SERVER (and an omitted/empty JDBC option) intentionally leaves the server session unchanged.
		/// </summary>
		public static string? TranslateConnectionTimeZone(string? connectionTimeZone)
		{
			if (string.IsNullOrWhiteSpace(connectionTimeZone)
				|| connectionTimeZone.Equals("SERVER", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			if (connectionTimeZone.Equals("UTC", StringComparison.OrdinalIgnoreCase)
				|| connectionTimeZone.Equals("Z", StringComparison.OrdinalIgnoreCase)
				|| connectionTimeZone.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase)
				|| connectionTimeZone.Equals("GMT", StringComparison.OrdinalIgnoreCase))
			{
				return "+00:00";
			}

			if (connectionTimeZone.Equals("LOCAL", StringComparison.OrdinalIgnoreCase))
			{
				var localId = TimeZoneInfo.Local.Id;
				if (TimeZoneInfo.Local.Equals(TimeZoneInfo.Utc))
					return "+00:00";
				return TimeZoneInfo.TryConvertWindowsIdToIanaId(localId, out var ianaId) ? ianaId : localId;
			}

			return connectionTimeZone;
		}

		private static async ValueTask SetSessionTimeZoneAsync(
			MySqlConnection connection,
			string sessionTimeZone,
			CancellationToken cancellationToken)
		{
			await using var command = connection.CreateCommand();
			command.CommandText = "SET time_zone = ?";
			command.Parameters.Add(new MySqlParameter { Value = sessionTimeZone });
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		/// <summary>
		/// Get a connection from the pool.
		/// </summary>
		public static MySqlConnection GetConnection()
		{
			if (_dataSource == null)
				throw new InvalidOperationException("DatabaseFactory not initialized. Call Initialize first.");

			return _dataSource.CreateConnection();
		}

		/// <summary>
		/// Get a connection asynchronously.
		/// </summary>
		public static async Task<MySqlConnection> GetConnectionAsync()
		{
			if (_dataSource == null)
				throw new InvalidOperationException("DatabaseFactory not initialized. Call Initialize first.");

			var connection = _dataSource.CreateConnection();
			await connection.OpenAsync();
			return connection;
		}

		/// <summary>
		/// Test the connection pool.
		/// </summary>
		public static async Task<bool> TestConnectionAsync()
		{
			try
			{
				using (var conn = await GetConnectionAsync())
				{
					using (var cmd = conn.CreateCommand())
					{
						cmd.CommandText = "SELECT 1";
						var result = await cmd.ExecuteScalarAsync();
						return result != null;
					}
				}
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Close all connections in the pool.
		/// </summary>
		public static void Dispose()
		{
			_dataSource?.Dispose();
			_dataSource = null;
		}
	}
}
