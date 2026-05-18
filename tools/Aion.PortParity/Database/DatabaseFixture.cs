using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MySqlConnector;

namespace Aion.PortParity.Database
{
	/// <summary>
	/// Database fixture for initializing test databases from SQL schema files.
	/// Allows parallel parity testing against fresh schemas.
	/// </summary>
	public class DatabaseFixture : IAsyncDisposable
	{
		private readonly string _connectionString;
		private readonly string _databaseName;
		private MySqlConnection? _connection;

		public DatabaseFixture(string server, string userId, string password, string databaseName)
		{
			_databaseName = databaseName;
			_connectionString = new MySqlConnectionStringBuilder
			{
				Server = server,
				UserID = userId,
				Password = password,
				AllowUserVariables = true,
			}.ConnectionString;
		}

		/// <summary>
		/// Initialize the fixture: create database and run schema SQL file.
		/// </summary>
		public async Task InitializeAsync(string sqlSchemaPath)
		{
			if (!File.Exists(sqlSchemaPath))
				throw new FileNotFoundException($"Schema file not found: {sqlSchemaPath}");

			_connection = new MySqlConnection(_connectionString);
			await _connection.OpenAsync();

			try
			{
				// Drop existing database if it exists
				using (var cmd = _connection.CreateCommand())
				{
					cmd.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`";
					await cmd.ExecuteNonQueryAsync();
				}

				// Create fresh database
				using (var cmd = _connection.CreateCommand())
				{
					cmd.CommandText = $"CREATE DATABASE `{_databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
					await cmd.ExecuteNonQueryAsync();
				}

				// Execute schema file
				var schema = File.ReadAllText(sqlSchemaPath, Encoding.UTF8);
				await ExecuteSqlScript(_connection, schema);
			}
			catch
			{
				await _connection.CloseAsync();
				throw;
			}
		}

		/// <summary>
		/// Get a connection to the test database.
		/// </summary>
		public MySqlConnection GetConnection()
		{
			var connBuilder = new MySqlConnectionStringBuilder(_connectionString) { Database = _databaseName };
			return new MySqlConnection(connBuilder.ConnectionString);
		}

		/// <summary>
		/// Execute multiple SQL statements from a script.
		/// </summary>
		private static async Task ExecuteSqlScript(MySqlConnection connection, string script)
		{
			var statements = script.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

			foreach (var statement in statements)
			{
				var trimmed = statement.Trim();
				if (string.IsNullOrEmpty(trimmed))
					continue;

				using (var cmd = connection.CreateCommand())
				{
					cmd.CommandText = trimmed;
					cmd.CommandTimeout = 30;
					await cmd.ExecuteNonQueryAsync();
				}
			}
		}

		/// <summary>
		/// Clean up: drop the test database.
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			if (_connection != null)
			{
				try
				{
					using (var cmd = _connection.CreateCommand())
					{
						cmd.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`";
						await cmd.ExecuteNonQueryAsync();
					}
				}
				catch
				{ /* Ignore cleanup errors */
				}

				await _connection.CloseAsync();
				_connection.Dispose();
			}
		}
	}
}
