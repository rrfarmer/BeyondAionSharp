using Aion.Commons.Database;
using Aion.LoginServer.Data;
using Aion.LoginServer.Utils;

namespace Aion.LoginServer.Tests;

public class LoginDatabaseIntegrationTests
{
	[Fact]
	public async Task AccountRepository_RoundTripsAgainstLoginSchema_WhenEnabled()
	{
		if (Environment.GetEnvironmentVariable("AION_LOGIN_DB_INTEGRATION") != "1")
			return;

		DatabaseFactory.Initialize(
			server: Environment.GetEnvironmentVariable("AION_LOGIN_DB_HOST") ?? "localhost",
			userId: Environment.GetEnvironmentVariable("AION_LOGIN_DB_USER") ?? "root",
			password: Environment.GetEnvironmentVariable("AION_LOGIN_DB_PASSWORD") ?? "aion",
			database: Environment.GetEnvironmentVariable("AION_LOGIN_DB_NAME") ?? "aion_ls",
			port: int.Parse(Environment.GetEnvironmentVariable("AION_LOGIN_DB_PORT") ?? "3307"));
		await InitializeSchemaAsync();

		var timeRepo = new AccountTimeRepository();
		var accountRepo = new AccountRepository(timeRepo);
		var inserted = new Model.Account
		{
			Name = "integration",
			PasswordHash = AccountUtils.EncodePassword("secret"),
			Activated = 1,
			LastServer = -1,
		};

		Assert.True(await accountRepo.InsertAccountAsync(inserted, useExternalAuth: false));
		var loaded = await accountRepo.GetAccountByNameAsync("integration", useExternalAuth: false);

		Assert.NotNull(loaded);
		Assert.Equal(inserted.Id, loaded.Id);
		Assert.Equal(inserted.PasswordHash, loaded.PasswordHash);
	}

	private static async Task InitializeSchemaAsync()
	{
		var sqlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "login-server", "sql", "aion_ls.sql"));
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

	private static IEnumerable<string> SplitSqlStatements(string sql)
	{
		var lines = sql.Split('\n')
			.Select(line => line.TrimEnd('\r'))
			.Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(line));
		return string.Join('\n', lines)
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(statement => !string.IsNullOrWhiteSpace(statement));
	}
}
