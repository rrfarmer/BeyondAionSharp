using System.Collections.Concurrent;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Aion.ServerPackets;

namespace Aion.LoginServer.Services;

public interface ILoginClientSession
{
	Account Account { get; }

	SessionKey SessionKey { get; }

	bool JoinedGameServer { get; }

	Task SendPacketAsync(AionServerPacket packet);

	Task CloseWithPacketAsync(AionServerPacket packet);
}

public enum LoginSessionRegisterResult
{
	Registered,
	AlreadyLoggedIn
}

public interface ILoginSessionRegistry
{
	Task<LoginSessionRegisterResult> RegisterLoginSessionAsync(ILoginClientSession session);

	void RegisterReconnectedSession(ILoginClientSession session);

	void RemoveLoginSession(Account account, ILoginClientSession session);

	ILoginClientSession? GetLoginSession(int accountId);

	Task<bool> KickLoginSessionAsync(int accountId, AionAuthResponse response);

	ILoginClientSession? ConsumeLoginSession(SessionKey sessionKey);

	void AddReconnectingAccount(ReconnectingAccount account);

	bool TryConsumeReconnectingAccount(int accountId, int reconnectKey, out ReconnectingAccount? account);

	void BeginGameServerCharacterCountLoad(int accountId, IReadOnlyDictionary<byte, int> initialCounts);

	void AddGameServerCharacterCount(int accountId, byte gameServerId, int characterCount);

	bool HasAllGameServerCharacterCounts(int accountId, int gameServerCount);

	IReadOnlyDictionary<byte, int> GetGameServerCharacterCounts(int accountId);
}

public sealed class LoginSessionRegistry : ILoginSessionRegistry
{
	private readonly ConcurrentDictionary<int, ILoginClientSession> _accountsOnLoginServer = new();
	private readonly ConcurrentDictionary<int, ReconnectingAccount> _reconnectingAccounts = new();
	private readonly ConcurrentDictionary<int, ConcurrentDictionary<byte, int>> _accountsGameServerCharacterCounts = new();

	public async Task<LoginSessionRegisterResult> RegisterLoginSessionAsync(ILoginClientSession session)
	{
		if (_accountsOnLoginServer.TryRemove(session.Account.Id, out var existingSession))
		{
			await existingSession.CloseWithPacketAsync(new SmAccountKick(AionAuthResponse.STR_L2AUTH_S_KICKED_DOUBLE_LOGIN));
			return LoginSessionRegisterResult.AlreadyLoggedIn;
		}

		_accountsOnLoginServer[session.Account.Id] = session;
		return LoginSessionRegisterResult.Registered;
	}

	public void RegisterReconnectedSession(ILoginClientSession session)
	{
		_accountsOnLoginServer[session.Account.Id] = session;
	}

	public void RemoveLoginSession(Account account, ILoginClientSession session)
	{
		_accountsOnLoginServer.TryGetValue(account.Id, out var currentSession);
		if (ReferenceEquals(currentSession, session))
			_accountsOnLoginServer.TryRemove(account.Id, out _);
	}

	public ILoginClientSession? GetLoginSession(int accountId)
	{
		_accountsOnLoginServer.TryGetValue(accountId, out var session);
		return session;
	}

	public async Task<bool> KickLoginSessionAsync(int accountId, AionAuthResponse response)
	{
		if (!_accountsOnLoginServer.TryRemove(accountId, out var session))
			return false;

		await session.CloseWithPacketAsync(new SmAccountKick(response));
		return true;
	}

	public ILoginClientSession? ConsumeLoginSession(SessionKey sessionKey)
	{
		if (!_accountsOnLoginServer.TryGetValue(sessionKey.AccountId, out var session))
			return null;

		if (!session.SessionKey.CheckSessionKey(sessionKey))
			return null;

		var removed = ((ICollection<KeyValuePair<int, ILoginClientSession>>)_accountsOnLoginServer)
			.Remove(new KeyValuePair<int, ILoginClientSession>(sessionKey.AccountId, session));
		return removed ? session : null;
	}

	public void AddReconnectingAccount(ReconnectingAccount account)
	{
		_reconnectingAccounts[account.Account.Id] = account;
	}

	public bool TryConsumeReconnectingAccount(int accountId, int reconnectKey, out ReconnectingAccount? account)
	{
		account = null;
		if (!_reconnectingAccounts.TryRemove(accountId, out var reconnectingAccount))
			return false;

		if (reconnectingAccount.ReconnectionKey != reconnectKey)
			return false;

		account = reconnectingAccount;
		return true;
	}

	public void BeginGameServerCharacterCountLoad(int accountId, IReadOnlyDictionary<byte, int> initialCounts)
	{
		_accountsGameServerCharacterCounts[accountId] = new ConcurrentDictionary<byte, int>(initialCounts);
	}

	public void AddGameServerCharacterCount(int accountId, byte gameServerId, int characterCount)
	{
		_accountsGameServerCharacterCounts
			.GetOrAdd(accountId, _ => new ConcurrentDictionary<byte, int>())
			[gameServerId] = characterCount;
	}

	public bool HasAllGameServerCharacterCounts(int accountId, int gameServerCount)
	{
		return _accountsGameServerCharacterCounts.TryGetValue(accountId, out var counts) && counts.Count == gameServerCount;
	}

	public IReadOnlyDictionary<byte, int> GetGameServerCharacterCounts(int accountId)
	{
		return _accountsGameServerCharacterCounts.TryGetValue(accountId, out var counts)
			? new Dictionary<byte, int>(counts)
			: new Dictionary<byte, int>();
	}
}
