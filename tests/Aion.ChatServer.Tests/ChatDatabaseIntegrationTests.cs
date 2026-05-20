using Aion.ChatServer.Data.Repositories;
using Aion.Commons.Database;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.ChatServer.Tests;

public class ChatDatabaseIntegrationTests
{
	[Fact]
	public async Task ChatLogRepository_InsertsAgainstJavaChatSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_CHAT_DB_INTEGRATION") != "1")
			return;

		DatabaseFactory.Initialize(
			server: Environment.GetEnvironmentVariable("AION_CHAT_DB_HOST") ?? "localhost",
			userId: Environment.GetEnvironmentVariable("AION_CHAT_DB_USER") ?? "root",
			password: Environment.GetEnvironmentVariable("AION_CHAT_DB_PASSWORD") ?? "aion",
			database: Environment.GetEnvironmentVariable("AION_CHAT_DB_NAME") ?? "aion_cs",
			port: int.Parse(Environment.GetEnvironmentVariable("AION_CHAT_DB_PORT") ?? "3307"));
		await InitializeSchemaAsync();

		var repository = new ChatLogRepository(NullLogger<ChatLogRepository>.Instance);
		await repository.InsertChatLogAsync("Daeva", "hello integration", "REGION (E)");

		Assert.Equal("Daeva", await ExecuteScalarStringAsync("SELECT sender FROM chatlog WHERE message = 'hello integration'"));
		Assert.Equal("REGION (E)", await ExecuteScalarStringAsync("SELECT type FROM chatlog WHERE message = 'hello integration'"));
	}

	private static async Task InitializeSchemaAsync()
	{
		var sqlPath = FindChatSchemaPath();
		var sql = await File.ReadAllTextAsync(sqlPath);
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		foreach (var statement in SplitSqlStatements(sql))
		{
			await using var command = connection.CreateCommand();
			command.CommandText = statement;
			await command.ExecuteNonQueryAsync();
		}
	}

	private static string FindChatSchemaPath()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, "chat-server", "sql", "aion_cs.sql");
			if (File.Exists(candidate))
				return candidate;
			directory = directory.Parent;
		}

		throw new FileNotFoundException("Could not find chat-server/sql/aion_cs.sql from test output directory.", "chat-server/sql/aion_cs.sql");
	}

	private static IEnumerable<string> SplitSqlStatements(string sql)
	{
		var lines = sql.Split('\n')
			.Select(line => line.TrimEnd('\r'))
			.Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(line));
		return string.Join('\n', lines)
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(statement => !string.IsNullOrWhiteSpace(statement));
	}

	private static async Task<string?> ExecuteScalarStringAsync(string sql)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		var value = await command.ExecuteScalarAsync();
		return value is null or DBNull ? null : Convert.ToString(value);
	}
}
