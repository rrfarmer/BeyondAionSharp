using System.Security.Cryptography;
using Aion.LoginServer.Model;

namespace Aion.LoginServer.Network.Aion;

public sealed record SessionKey(int AccountId, int LoginOk, int PlayOk1, int PlayOk2)
{
	public SessionKey(Account account)
		: this(account.Id, RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue), RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue), RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue))
	{
	}

	public bool CheckLogin(int accountId, int loginOk) => AccountId == accountId && LoginOk == loginOk;

	public bool CheckSessionKey(SessionKey key) => this == key;
}
