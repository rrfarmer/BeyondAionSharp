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
		var service = new LoginAuthService(new LoginServerOptions(), accountRepo, timeRepo, new FakeBannedIpRepository());

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
		var service = new LoginAuthService(new LoginServerOptions(), accountRepo, timeRepo, new FakeBannedIpRepository());

		await service.CompleteSuccessfulLoginAsync(account, "127.0.0.1");

		Assert.Equal("127.0.0.1", accountRepo.LastIp);
		Assert.True(timeRepo.Updated);
	}

	[Fact]
	public async Task LoginAsync_InvalidPassword_ReturnsIncorrectPassword()
	{
		var service = new LoginAuthService(new LoginServerOptions(), new FakeAccountRepository(TestAccount("player", "secret")), new FakeAccountTimeRepository(), new FakeBannedIpRepository());

		var result = await service.LoginAsync("player", "wrong", "127.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD, result.Response);
	}

	[Fact]
	public async Task LoginAsync_AutoCreatesMissingAccountWhenEnabled()
	{
		var accountRepo = new FakeAccountRepository(null);
		var service = new LoginAuthService(new LoginServerOptions { AutoCreateAccounts = true }, accountRepo, new FakeAccountTimeRepository(), new FakeBannedIpRepository());

		var result = await service.LoginAsync("newbie", "secret", "127.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_ALL_OK, result.Response);
		Assert.NotNull(result.Account);
		Assert.Equal("newbie", result.Account.Name);
		Assert.Equal(AccountUtils.EncodePassword("secret"), result.Account.PasswordHash);
	}

	[Fact]
	public async Task LoginAsync_BannedIp_ReturnsBlockedIp()
	{
		var bannedRepo = new FakeBannedIpRepository(new BannedIp { Mask = "127.0.0.1" });
		var service = new LoginAuthService(new LoginServerOptions(), new FakeAccountRepository(TestAccount("player", "secret")), new FakeAccountTimeRepository(), bannedRepo);

		var result = await service.LoginAsync("player", "secret", "127.0.0.1");

		Assert.Equal(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP, result.Response);
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

	private sealed class FakeBannedIpRepository : IBannedIpRepository
	{
		private readonly IReadOnlyCollection<BannedIp> _bans;

		public FakeBannedIpRepository(params BannedIp[] bans)
		{
			_bans = bans;
		}

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task<IReadOnlyCollection<BannedIp>> GetAllBansAsync(CancellationToken cancellationToken = default) => Task.FromResult(_bans);

		public Task<bool> InsertAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<bool> RemoveAsync(string mask, CancellationToken cancellationToken = default) => Task.FromResult(true);
	}
}
