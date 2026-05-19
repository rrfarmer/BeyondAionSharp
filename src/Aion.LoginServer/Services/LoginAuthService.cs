using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Utils;

namespace Aion.LoginServer.Services;

public sealed record LoginAuthResult(AionAuthResponse? Response, Account? Account, bool SendAccountBannedPacket = false)
{
	public static LoginAuthResult Failure(AionAuthResponse response) => new(response, null);

	public static LoginAuthResult Success(Account account) => new(AionAuthResponse.STR_L2AUTH_S_ALL_OK, account);

	public static LoginAuthResult AccountBanned() => new(null, null, SendAccountBannedPacket: true);
}

public interface ILoginAuthService
{
	Task<LoginAuthResult> LoginAsync(string username, string password, string remoteIp, CancellationToken cancellationToken = default);

	Task CompleteSuccessfulLoginAsync(Account account, string remoteIp, CancellationToken cancellationToken = default);

	Task UpdateOnLogoutAsync(Account account, CancellationToken cancellationToken = default);
}

public sealed class LoginAuthService : ILoginAuthService
{
	private readonly LoginServerOptions _options;
	private readonly IAccountRepository _accountRepository;
	private readonly IAccountTimeRepository _accountTimeRepository;
	private readonly IBannedIpRepository _bannedIpRepository;

	public LoginAuthService(
		LoginServerOptions options,
		IAccountRepository accountRepository,
		IAccountTimeRepository accountTimeRepository,
		IBannedIpRepository bannedIpRepository)
	{
		_options = options;
		_accountRepository = accountRepository;
		_accountTimeRepository = accountTimeRepository;
		_bannedIpRepository = bannedIpRepository;
	}

	public async Task<LoginAuthResult> LoginAsync(string username, string password, string remoteIp, CancellationToken cancellationToken = default)
	{
		if (await IsIpBannedAsync(remoteIp, cancellationToken))
			return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP);

		if (_options.UseExternalAuth)
			return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_ACCOUNTCACHESERVER_DOWN);

		var account = await _accountRepository.GetAccountByNameAsync(username, useExternalAuth: false, cancellationToken);
		if (account == null && _options.AutoCreateAccounts && !string.IsNullOrEmpty(username))
			account = await CreateAccountAsync(username, password, cancellationToken);

		if (account == null)
			return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_ACCOUNT_LOAD_FAIL);

		if (account.PasswordHash != AccountUtils.EncodePassword(password))
			return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD);

		if (account.Activated != 1)
			return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_AGREE_GAME);

		if (IsAccountExpired(account))
			return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_TIME_EXHAUSTED);

		if (IsAccountPenaltyActive(account))
			return LoginAuthResult.AccountBanned();

		if (!string.IsNullOrWhiteSpace(account.IpForce) && !NetworkMask.Matches(account.IpForce, remoteIp))
			return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP);

		return LoginAuthResult.Success(account);
	}

	public async Task CompleteSuccessfulLoginAsync(Account account, string remoteIp, CancellationToken cancellationToken = default)
	{
		UpdateOnLogin(account);
		await _accountTimeRepository.UpdateAccountTimeAsync(account.Id, account.AccountTime, cancellationToken);
		await _accountRepository.UpdateLastIpAsync(account.Id, remoteIp, cancellationToken);
		await _accountRepository.UpdateMembershipAsync(account.Id, cancellationToken);
	}

	public async Task UpdateOnLogoutAsync(Account account, CancellationToken cancellationToken = default)
	{
		var accountTime = account.AccountTime;
		accountTime.LastLoginTime = DateTime.UtcNow;
		accountTime.SessionDuration = (long)(DateTime.UtcNow - accountTime.LastLoginTime).TotalMilliseconds;
		accountTime.AccumulatedOnlineTime += accountTime.SessionDuration;
		await _accountTimeRepository.UpdateAccountTimeAsync(account.Id, accountTime, cancellationToken);
		account.AccountTime = accountTime;
	}

	private async Task<bool> IsIpBannedAsync(string remoteIp, CancellationToken cancellationToken)
	{
		await _bannedIpRepository.CleanExpiredBansAsync(cancellationToken);
		var bans = await _bannedIpRepository.GetAllBansAsync(cancellationToken);
		var now = DateTime.UtcNow;
		return bans.Any(ban => ban.IsActive(now) && NetworkMask.Matches(ban.Mask, remoteIp));
	}

	private async Task<Account?> CreateAccountAsync(string username, string password, CancellationToken cancellationToken)
	{
		var account = new Account
		{
			Name = username,
			PasswordHash = AccountUtils.EncodePassword(password),
			AccessLevel = 0,
			Membership = 0,
			Activated = 1,
			LastServer = -1,
			LastMac = "xx-xx-xx-xx-xx-xx",
			Toll = 0,
		};
		return await _accountRepository.InsertAccountAsync(account, useExternalAuth: false, cancellationToken) ? account : null;
	}

	private static void UpdateOnLogin(Account account)
	{
		var accountTime = account.AccountTime;
		var now = DateTime.UtcNow;
		var lastLoginDay = GetDays(accountTime.LastLoginTime);
		var currentDay = GetDays(now);

		if (lastLoginDay < currentDay)
		{
			accountTime.AccumulatedOnlineTime = 0;
			accountTime.AccumulatedRestTime = 0;
		}
		else
		{
			var restTime = (long)(now - accountTime.LastLoginTime).TotalMilliseconds - accountTime.SessionDuration;
			accountTime.AccumulatedRestTime += restTime;
		}

		accountTime.LastLoginTime = now;
		account.AccountTime = accountTime;
	}

	private static bool IsAccountExpired(Account account)
	{
		return account.AccountTime.ExpirationTime != null && account.AccountTime.ExpirationTime.Value < DateTime.UtcNow;
	}

	private static bool IsAccountPenaltyActive(Account account)
	{
		return account.AccountTime.PenaltyEnd != null
			&& (account.AccountTime.PenaltyEnd.Value == DateTime.UnixEpoch.AddMilliseconds(1000) || account.AccountTime.PenaltyEnd.Value >= DateTime.UtcNow);
	}

	private static int GetDays(DateTime value)
	{
		return (int)(new DateTimeOffset(value).ToUnixTimeMilliseconds() / 1000 / 3600 / 24);
	}
}
