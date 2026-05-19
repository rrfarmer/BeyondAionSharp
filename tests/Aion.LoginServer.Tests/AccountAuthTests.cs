using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Services;
using Aion.LoginServer.Utils;

namespace Aion.LoginServer.Tests;

public class AccountAuthTests
{
	[Fact]
	public void EncodePassword_MatchesJavaSha1Base64()
	{
		Assert.Equal("W6ph5Mm5Pz8GgiULbPgzG37mj9g=", AccountUtils.EncodePassword("password"));
	}

	[Fact]
	public async Task LoginAsync_ValidPassword_ReturnsSuccessWithoutMutatingLoginFields()
	{
		var account = TestAccount("player", "secret");
		var accountRepo = new FakeAccountRepository(account);
		var timeRepo = new FakeAccountTimeRepository();
		var service = CreateService(new LoginServerOptions(), accountRepo, timeRepo, new FakeBannedIpService());

		var result = await service.LoginAsync("player", "secret", "127.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_ALL_OK, result.Response);
		Assert.Same(account, result.Account);
		Assert.Null(accountRepo.LastIp);
		Assert.False(timeRepo.Updated);
	}

	[Fact]
	public async Task CompleteSuccessfulLoginAsync_UpdatesLoginFields()
	{
		var account = TestAccount("player", "secret");
		var accountRepo = new FakeAccountRepository(account);
		var timeRepo = new FakeAccountTimeRepository();
		var service = CreateService(new LoginServerOptions(), accountRepo, timeRepo, new FakeBannedIpService());

		await service.CompleteSuccessfulLoginAsync(account, "127.0.0.1");

		Assert.Equal("127.0.0.1", accountRepo.LastIp);
		Assert.True(timeRepo.Updated);
	}

	[Fact]
	public async Task LoginAsync_InvalidPassword_ReturnsIncorrectPassword()
	{
		var service = CreateService(new LoginServerOptions(), new FakeAccountRepository(TestAccount("player", "secret")), new FakeAccountTimeRepository(), new FakeBannedIpService());

		var result = await service.LoginAsync("player", "wrong", "127.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD, result.Response);
	}

	[Fact]
	public async Task LoginAsync_AutoCreatesMissingAccountWhenEnabled()
	{
		var accountRepo = new FakeAccountRepository(null);
		var service = CreateService(new LoginServerOptions { AutoCreateAccounts = true }, accountRepo, new FakeAccountTimeRepository(), new FakeBannedIpService());

		var result = await service.LoginAsync("newbie", "secret", "127.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_ALL_OK, result.Response);
		Assert.NotNull(result.Account);
		Assert.Equal("newbie", result.Account.Name);
		Assert.Equal(AccountUtils.EncodePassword("secret"), result.Account.PasswordHash);
	}

	[Fact]
	public async Task LoginAsync_BannedIp_ReturnsBlockedIp()
	{
		var bannedIpService = new FakeBannedIpService(new BannedIp { Mask = "127.0.0.1" });
		var service = CreateService(new LoginServerOptions(), new FakeAccountRepository(TestAccount("player", "secret")), new FakeAccountTimeRepository(), bannedIpService);

		var result = await service.LoginAsync("player", "secret", "127.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP, result.Response);
	}

	[Fact]
	public async Task LoginAsync_ExternalAuthSuccess_UsesExternalAccountIdAndSkipsPasswordHash()
	{
		var accountRepo = new FakeAccountRepository(null);
		var externalAuth = new FakeExternalAuthClient(new ExternalAuthResponse("external-account", 0));
		var service = CreateService(
			new LoginServerOptions { ExternalAuthUrl = "http://auth.example/login", AutoCreateAccounts = true },
			accountRepo,
			new FakeAccountTimeRepository(),
			new FakeBannedIpService(),
			externalAuth);

		var result = await service.LoginAsync("player", "secret", "10.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_ALL_OK, result.Response);
		Assert.NotNull(result.Account);
		Assert.Equal("external-account", result.Account.Name);
		Assert.Equal(string.Empty, result.Account.PasswordHash);
		Assert.True(accountRepo.InsertUseExternalAuth);
		Assert.Equal(("player", "secret", "http://auth.example/login"), externalAuth.LastRequest);
	}

	[Fact]
	public async Task LoginAsync_ExternalAuthResponseFailure_ReturnsMappedResponse()
	{
		var externalAuth = new FakeExternalAuthClient(new ExternalAuthResponse("external-account", (int)AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD));
		var service = CreateService(
			new LoginServerOptions { ExternalAuthUrl = "http://auth.example/login" },
			new FakeAccountRepository(null),
			new FakeAccountTimeRepository(),
			new FakeBannedIpService(),
			externalAuth);

		var result = await service.LoginAsync("player", "secret", "127.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD, result.Response);
	}

	[Fact]
	public async Task LoginAsync_ExternalAuthUnavailable_ReturnsAccountCacheServerDown()
	{
		var service = CreateService(
			new LoginServerOptions { ExternalAuthUrl = "http://auth.example/login" },
			new FakeAccountRepository(null),
			new FakeAccountTimeRepository(),
			new FakeBannedIpService(),
			new FakeExternalAuthClient(null));

		var result = await service.LoginAsync("player", "secret", "127.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_ACCOUNTCACHESERVER_DOWN, result.Response);
	}

	[Fact]
	public async Task LoginAsync_BruteForceBanOccursAfterJavaThreshold()
	{
		var bannedIpService = new FakeBannedIpService();
		var service = CreateService(
			new LoginServerOptions { LoginTryBeforeBan = 2, WrongLoginBanMinutes = 15 },
			new FakeAccountRepository(TestAccount("player", "secret")),
			new FakeAccountTimeRepository(),
			bannedIpService);

		var first = await service.LoginAsync("player", "wrong", "10.0.0.1");
		var second = await service.LoginAsync("player", "wrong", "10.0.0.1");
		var third = await service.LoginAsync("player", "wrong", "10.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD, first.Response);
		Assert.False(first.CloseAfterResponse);
		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD, second.Response);
		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP, third.Response);
		Assert.True(third.CloseAfterResponse);
		Assert.Equal("10.0.0.1", bannedIpService.InsertedMask);
		Assert.NotNull(bannedIpService.InsertedExpireTime);
	}

	private static LoginAuthService CreateService(
		LoginServerOptions options,
		IAccountRepository accountRepository,
		IAccountTimeRepository accountTimeRepository,
		IBannedIpService bannedIpService,
		IExternalAuthClient? externalAuthClient = null,
		IBruteForceProtector? bruteForceProtector = null)
	{
		return new LoginAuthService(
			options,
			accountRepository,
			accountTimeRepository,
			bannedIpService,
			externalAuthClient ?? new FakeExternalAuthClient(null),
			bruteForceProtector ?? new BruteForceProtector());
	}

	private static Account TestAccount(string name, string password)
	{
		return new Account
		{
			Id = 1,
			Name = name,
			PasswordHash = AccountUtils.EncodePassword(password),
			Activated = 1,
			AccountTime = new AccountTime { LastLoginTime = DateTime.UtcNow },
		};
	}

	private sealed class FakeAccountRepository : IAccountRepository
	{
		private Account? _account;

		public FakeAccountRepository(Account? account)
		{
			_account = account;
		}

		public string? LastIp { get; private set; }

		public bool InsertUseExternalAuth { get; private set; }

		public Task<Account?> GetAccountByNameAsync(string name, bool useExternalAuth, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_account?.Name == name ? _account : null);
		}

		public Task<Account?> GetAccountByIdAsync(int id, bool useExternalAuth, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_account?.Id == id ? _account : null);
		}

		public Task<bool> InsertAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default)
		{
			account.Id = 42;
			InsertUseExternalAuth = useExternalAuth;
			_account = account;
			return Task.FromResult(true);
		}

		public Task UpdateLastIpAsync(int accountId, string ip, CancellationToken cancellationToken = default)
		{
			LastIp = ip;
			return Task.CompletedTask;
		}

		public Task<bool> UpdateLastMacAsync(int accountId, string mac, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<bool> UpdateLastHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<bool> UpdateAllowedHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<string> GetLastIpAsync(int accountId, CancellationToken cancellationToken = default) => Task.FromResult(_account?.LastIp ?? string.Empty);

		public Task<bool> UpdateAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default)
		{
			_account = account;
			return Task.FromResult(true);
		}

		public Task UpdateLastServerAsync(int accountId, sbyte lastServer, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task UpdateMembershipAsync(int accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class FakeAccountTimeRepository : IAccountTimeRepository
	{
		public bool Updated { get; private set; }

		public Task<AccountTime?> GetAccountTimeAsync(int accountId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<AccountTime?>(new AccountTime());
		}

		public Task UpdateAccountTimeAsync(int accountId, AccountTime accountTime, CancellationToken cancellationToken = default)
		{
			Updated = true;
			return Task.CompletedTask;
		}
	}

	private sealed class FakeBannedIpService : IBannedIpService
	{
		private readonly List<BannedIp> _bans;

		public FakeBannedIpService(params BannedIp[] bans)
		{
			_bans = bans.ToList();
		}

		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public IReadOnlyCollection<BannedIp> GetEntries() => _bans;

		public bool IsBanned(string ip)
		{
			var now = DateTime.UtcNow;
			return _bans.Any(ban => ban.IsActive(now) && NetworkMask.Matches(ban.Mask, ip));
		}

		public string? InsertedMask { get; private set; }

		public DateTime? InsertedExpireTime { get; private set; }

		public Task<bool> BanAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default)
		{
			InsertedMask = mask;
			InsertedExpireTime = expireTime;
			_bans.Add(new BannedIp { Mask = mask, TimeEnd = expireTime });
			return Task.FromResult(true);
		}

		public Task<bool> UnbanAsync(string mask, CancellationToken cancellationToken = default) => Task.FromResult(_bans.RemoveAll(ban => ban.Mask == mask) > 0);
	}

	private sealed class FakeExternalAuthClient : IExternalAuthClient
	{
		private readonly ExternalAuthResponse? _response;

		public FakeExternalAuthClient(ExternalAuthResponse? response)
		{
			_response = response;
		}

		public (string User, string Password, string Url)? LastRequest { get; private set; }

		public Task<ExternalAuthResponse?> AuthenticateAsync(string user, string password, string url, CancellationToken cancellationToken = default)
		{
			LastRequest = (user, password, url);
			return Task.FromResult(_response);
		}
	}
}
