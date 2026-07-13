using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Utils;

namespace Aion.LoginServer.Services;

public sealed record LoginAuthResult(AionAuthResponse? Response, Account? Account, bool SendAccountBannedPacket = false, bool CloseAfterResponse = false)
{
	public static LoginAuthResult Failure(AionAuthResponse response) => new(response, null);

	public static LoginAuthResult FailureAndClose(AionAuthResponse response) => new(response, null, CloseAfterResponse: true);

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
	private readonly IBannedIpService _bannedIpService;
	private readonly IExternalAuthClient _externalAuthClient;
	private readonly IBruteForceProtector _bruteForceProtector;

	public LoginAuthService(
		LoginServerOptions options,
		IAccountRepository accountRepository,
		IAccountTimeRepository accountTimeRepository,
		IBannedIpService bannedIpService,
		IExternalAuthClient externalAuthClient,
		IBruteForceProtector bruteForceProtector)
	{
		_options = options;
		_accountRepository = accountRepository;
		_accountTimeRepository = accountTimeRepository;
		_bannedIpService = bannedIpService;
		_externalAuthClient = externalAuthClient;
		_bruteForceProtector = bruteForceProtector;
	}

	public async Task<LoginAuthResult> LoginAsync(string username, string password, string remoteIp, CancellationToken cancellationToken = default)
	{
		if (_bannedIpService.IsBanned(remoteIp))
			return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP);

		var accountName = username;
		if (_options.UseExternalAuth)
		{
			var externalAuth = await _externalAuthClient.AuthenticateAsync(username, password, _options.ExternalAuthUrl, cancellationToken);
			if (externalAuth == null)
				return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_ACCOUNTCACHESERVER_DOWN);

			var externalResponse = GetByIdOrDefault(externalAuth.AionAuthResponseId, AionAuthResponse.STR_L2AUTH_UNKNOWN4);
			if (externalResponse != AionAuthResponse.STR_L2AUTH_S_ALL_OK)
				return await ApplyBruteForceProtectionAsync(externalResponse, remoteIp, cancellationToken);

			accountName = externalAuth.AccountId;
		}

		var account = await _accountRepository.GetAccountByNameAsync(accountName, _options.UseExternalAuth, cancellationToken);
		if (account == null && _options.AutoCreateAccounts && !string.IsNullOrEmpty(accountName))
			account = await CreateAccountAsync(accountName, password, _options.UseExternalAuth, cancellationToken);

		if (account == null)
			return LoginAuthResult.Failure(AionAuthResponse.STR_L2AUTH_S_ACCOUNT_LOAD_FAIL);

		if (!_options.UseExternalAuth && account.PasswordHash != AccountUtils.EncodePassword(password))
			return await ApplyBruteForceProtectionAsync(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD, remoteIp, cancellationToken);

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

	private async Task<LoginAuthResult> ApplyBruteForceProtectionAsync(AionAuthResponse response, string remoteIp, CancellationToken cancellationToken)
	{
		if (_options.BruteForceProtectionEnabled
			&& response is AionAuthResponse.STR_L2AUTH_S_INVALID_ACCOUT or AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD
			&& remoteIp != "127.0.0.1"
			&& _bruteForceProtector.AddFailedConnect(remoteIp, _options.LoginTryBeforeBan, _options.WrongLoginBanMinutes))
		{
			await _bannedIpService.BanAsync(remoteIp, DateTime.UtcNow.AddMinutes(_options.WrongLoginBanMinutes), cancellationToken);
			return LoginAuthResult.FailureAndClose(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP);
		}

		return LoginAuthResult.Failure(response);
	}

	private async Task<Account?> CreateAccountAsync(string username, string password, bool useExternalAuth, CancellationToken cancellationToken)
	{
		var account = new Account
		{
			Name = username,
			PasswordHash = useExternalAuth ? string.Empty : AccountUtils.EncodePassword(password),
			AccessLevel = 0,
			Membership = 0,
			Activated = 1,
			LastServer = 0,
			LastMac = "xx-xx-xx-xx-xx-xx",
		};
		return await _accountRepository.InsertAccountAsync(account, useExternalAuth, cancellationToken) ? account : null;
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

	private static AionAuthResponse GetByIdOrDefault(int id, AionAuthResponse fallback)
	{
		return Enum.IsDefined(typeof(AionAuthResponse), id) ? (AionAuthResponse)id : fallback;
	}
}
