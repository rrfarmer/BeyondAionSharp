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
			_connectionString = new MySqlConnectionStringBuilder
			{
				Server = server,
				UserID = userId,
				Password = password,
				Database = database,
				Port = (uint)port,
				MaximumPoolSize = (uint)maxPoolSize,
				ConnectionTimeout = (uint)(connectionTimeout / 1000),
				Pooling = true,
				AllowUserVariables = true,
			}.ConnectionString;

			var builder = new MySqlDataSourceBuilder(_connectionString);
			_dataSource = builder.Build();
		}

		public static void Initialize(DatabaseOptions options)
		{
			Initialize(
				options.Server,
				options.UserId,
				options.Password,
				options.Database,
				options.Port,
				options.MaxPoolSize,
				options.ConnectionTimeout);
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
