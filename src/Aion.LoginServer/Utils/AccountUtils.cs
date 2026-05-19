using System.Security.Cryptography;
using System.Text;

namespace Aion.LoginServer.Utils;

public static class AccountUtils
{
	public static string EncodePassword(string password)
	{
		var bytes = Encoding.UTF8.GetBytes(password);
		var hash = SHA1.HashData(bytes);
		return Convert.ToBase64String(hash);
	}
}
