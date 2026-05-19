using System.Net;
using Aion.Commons.Database;
using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network;
using Aion.LoginServer.Services;
using Aion.LoginServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

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

	[Fact]
	public async Task AccountRepository_InsertMatchesJavaAccountDaoShape_WhenEnabled()
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

		var timeRepo = new TrackingAccountTimeRepository();
		var accountRepo = new AccountRepository(timeRepo);
		var inserted = new Account
		{
			Name = "insertshape",
			PasswordHash = AccountUtils.EncodePassword("secret"),
			Activated = 1,
			LastServer = -1,
			Toll = 999,
		};

		Assert.True(await accountRepo.InsertAccountAsync(inserted, useExternalAuth: false));

		Assert.Equal(0, timeRepo.UpdateCalls);
		Assert.Equal(0, await ExecuteScalarLongAsync($"SELECT toll FROM account_data WHERE id={inserted.Id}"));
		Assert.Equal(0, await ExecuteScalarLongAsync($"SELECT COUNT(*) FROM account_time WHERE account_id={inserted.Id}"));
		Assert.NotEqual(default, inserted.AccountTime.LastLoginTime);
	}

	[Fact]
	public async Task AuxiliaryRepositories_RoundTripAgainstLoginSchema_WhenEnabled()
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

		await ExecuteNonQueryAsync("INSERT INTO gameservers(id, mask, password) VALUES (1, '127.0.0.1', 'pass')");
		var gameServers = await new GameServersRepository().GetAllGameServersAsync();
		Assert.True(gameServers.TryGetValue(1, out var gameServer));
		Assert.Equal("pass", gameServer.Password);

		var bannedIpRepository = new BannedIpRepository();
		Assert.True(await bannedIpRepository.InsertAsync("10.0.0.*", DateTime.UtcNow.AddHours(1)));
		Assert.Contains(await bannedIpRepository.GetAllBansAsync(), ban => ban.Mask == "10.0.0.*");
		Assert.True(await bannedIpRepository.RemoveAsync("10.0.0.*"));
		Assert.DoesNotContain(await bannedIpRepository.GetAllBansAsync(), ban => ban.Mask == "10.0.0.*");

		var bannedMacRepository = new BannedMacRepository();
		var macEntry = new BannedMacEntry("aa-bb-cc-dd-ee-ff", DateTime.UtcNow.AddDays(1), "integration");
		Assert.True(await bannedMacRepository.UpdateAsync(macEntry));
		Assert.Equal("integration", (await bannedMacRepository.LoadAsync())[macEntry.Mac].Details);
		Assert.True(await bannedMacRepository.RemoveAsync(macEntry.Mac));
		Assert.False((await bannedMacRepository.LoadAsync()).ContainsKey(macEntry.Mac));

		var bannedHddRepository = new BannedHddRepository();
		Assert.True(await bannedHddRepository.UpdateAsync("hdd-integration", DateTime.UtcNow.AddDays(1)));
		Assert.True((await bannedHddRepository.LoadAsync()).ContainsKey("hdd-integration"));
		Assert.True(await bannedHddRepository.RemoveAsync("hdd-integration"));
		Assert.False((await bannedHddRepository.LoadAsync()).ContainsKey("hdd-integration"));

		await ExecuteNonQueryAsync("INSERT INTO account_data(id, name, password, toll) VALUES (100, 'premium', 'hash', 50)");
		await ExecuteNonQueryAsync("INSERT INTO account_rewards(accountId, points, rewarded) VALUES (100, 25, 0)");
		var premiumRepository = new PremiumRepository();
		Assert.Equal(75, await premiumRepository.GetPointsAsync(100));
		Assert.Equal(1, await ExecuteScalarLongAsync("SELECT rewarded FROM account_rewards WHERE accountId=100"));
		Assert.True(await premiumRepository.UpdatePointsAsync(100, 75, 20));
		Assert.Equal(55, await ExecuteScalarLongAsync("SELECT toll FROM account_data WHERE id=100"));

		var accountsLogRepository = new AccountsLogRepository();
		await accountsLogRepository.AddRecordAsync(100, 1, DateTime.UtcNow, "127.0.0.1", "aa-bb", "hdd");
		Assert.Equal(1L, await ExecuteScalarAsync("SELECT COUNT(*) FROM account_login_history WHERE account_id=100 AND gameserver_id=1"));

		await ExecuteNonQueryAsync(
			"INSERT INTO player_transfers(id, source_server, target_server, source_account_id, target_account_id, player_id, status) " +
			"VALUES (200, 1, 2, 100, 101, 5000, 0)");
		var playerTransferRepository = new PlayerTransferRepository();
		var task = Assert.Single(await playerTransferRepository.GetNewAsync());
		Assert.Equal(200, task.Id);
		Assert.Equal(5000, task.PlayerId);
		task.Status = PlayerTransferTask.StatusActive;
		task.Comment = "performing";
		Assert.True(await playerTransferRepository.UpdateAsync(task));
		Assert.Equal(PlayerTransferTask.StatusActive, await ExecuteScalarLongAsync("SELECT status FROM player_transfers WHERE id=200"));
		Assert.NotNull(await ExecuteScalarAsync("SELECT time_performed FROM player_transfers WHERE id=200"));
	}

	[Fact]
	public async Task LoginSocket_RoundTripsEncryptedHandshakeAgainstLoginSchema_WhenEnabled()
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

		var port = SocketServerSmokeTests.GetFreeLoopbackPort();
		var options = new LoginServerOptions
		{
			ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
			AutoCreateAccounts = false,
			BruteForceProtectionEnabled = false,
		};
		var accountTimeRepository = new AccountTimeRepository();
		var accountRepository = new AccountRepository(accountTimeRepository);
		var account = new Account
		{
			Name = "socketlogin",
			PasswordHash = AccountUtils.EncodePassword("secret"),
			Activated = 1,
			LastServer = -1,
		};
		Assert.True(await accountRepository.InsertAccountAsync(account, useExternalAuth: false));
		var bannedIpService = new BannedIpService(new BannedIpRepository());
		await bannedIpService.LoadAsync();

		using var keyGenerator = new SocketServerSmokeTests.FixedLoginKeyGenerator();
		var loginServer = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			options,
			keyGenerator,
			new LoginAuthService(
				options,
				accountRepository,
				accountTimeRepository,
				bannedIpService,
				new ThrowingExternalAuthClient(),
				new BruteForceProtector()),
			new LoginSessionRegistry(),
			new GameServerRegistry());
		var serverTask = loginServer.StartAsync();

		using var client = await SocketServerSmokeTests.ConnectWithRetryAsync(port);
		await SocketServerSmokeTests.CompleteLoginHandshakeAsync(client, keyGenerator, account.Id, account.Name, "secret");

		await loginServer.StopAsync(TimeSpan.FromSeconds(1));
		await SocketServerSmokeTests.AssertClientClosedAsync(client.GetStream());
		await SocketServerSmokeTests.AssertTaskCompletedAsync(serverTask);

		var loaded = await accountRepository.GetAccountByIdAsync(account.Id, useExternalAuth: false);
		Assert.NotNull(loaded);
		Assert.Equal("127.0.0.1", loaded.LastIp);
	}

	private static async Task InitializeSchemaAsync()
	{
		var sqlPath = FindLoginSchemaPath();
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

	private static string FindLoginSchemaPath()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, "login-server", "sql", "aion_ls.sql");
			if (File.Exists(candidate))
				return candidate;
			directory = directory.Parent;
		}

		throw new FileNotFoundException("Could not find login-server/sql/aion_ls.sql from test output directory.", "login-server/sql/aion_ls.sql");
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

	private static async Task ExecuteNonQueryAsync(string sql)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		await command.ExecuteNonQueryAsync();
	}

	private static async Task<object?> ExecuteScalarAsync(string sql)
	{
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		var value = await command.ExecuteScalarAsync();
		return value is null or DBNull ? null : value;
	}

	private static async Task<long> ExecuteScalarLongAsync(string sql)
	{
		var value = await ExecuteScalarAsync(sql);
		Assert.NotNull(value);
		return Convert.ToInt64(value);
	}

	private sealed class ThrowingExternalAuthClient : IExternalAuthClient
	{
		public Task<ExternalAuthResponse?> AuthenticateAsync(string user, string password, string url, CancellationToken cancellationToken = default)
		{
			throw new InvalidOperationException("External auth should not be reached by login database integration tests.");
		}
	}

	private sealed class TrackingAccountTimeRepository : IAccountTimeRepository
	{
		public int UpdateCalls { get; private set; }

		public Task<AccountTime?> GetAccountTimeAsync(int accountId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<AccountTime?>(new AccountTime());
		}

		public Task UpdateAccountTimeAsync(int accountId, AccountTime accountTime, CancellationToken cancellationToken = default)
		{
			UpdateCalls++;
			return Task.CompletedTask;
		}
	}
}
