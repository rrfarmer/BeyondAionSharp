using System.Collections.Concurrent;

namespace Aion.LoginServer.Model;

public sealed class GameServerInfo
{
	private readonly ConcurrentDictionary<int, Account> _accountsOnGameServer = new();

	public GameServerInfo(byte id, string ipMask, string password)
	{
		Id = id;
		IpMask = ipMask;
		Password = password;
	}

	public byte Id { get; }

	public string IpMask { get; }

	public string Password { get; }

	public byte[] Ip { get; private set; } = { 0, 0, 0, 0 };

	public ushort Port { get; private set; }

	public byte MinAccessLevel { get; private set; }

	public int MaxPlayers { get; private set; }

	public bool IsOnline { get; private set; }

	public int CurrentPlayers => _accountsOnGameServer.Count;

	public bool IsFull => CurrentPlayers >= MaxPlayers;

	public void MarkOnline(byte[] ip, ushort port, byte minAccessLevel, int maxPlayers)
	{
		Ip = ip;
		Port = port;
		MinAccessLevel = minAccessLevel;
		MaxPlayers = maxPlayers;
		IsOnline = true;
	}

	public void MarkOffline()
	{
		IsOnline = false;
		_accountsOnGameServer.Clear();
	}

	public bool IsAccountOnGameServer(int accountId) => _accountsOnGameServer.ContainsKey(accountId);

	public void AddAccount(Account account) => _accountsOnGameServer[account.Id] = account;

	public Account? RemoveAccount(int accountId)
	{
		_accountsOnGameServer.TryRemove(accountId, out var account);
		return account;
	}

	public Account? GetAccount(int accountId)
	{
		_accountsOnGameServer.TryGetValue(accountId, out var account);
		return account;
	}
}
