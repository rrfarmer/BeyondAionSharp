using Aion.LoginServer.Network.Crypto;

namespace Aion.LoginServer.Network.Aion;

public static class LoginCredentialDecryptor
{
	public static LoginCredentials? Decrypt(byte[] encryptedLoginData, LoginRsaKeyPair keyPair)
	{
		if (encryptedLoginData.Length == 0 || encryptedLoginData.Length % 128 != 0)
			return null;

		try
		{
			var decrypted = new byte[encryptedLoginData.Length];
			for (var offset = 0; offset < encryptedLoginData.Length; offset += 128)
			{
				var block = keyPair.DecryptNoPadding(encryptedLoginData.AsSpan(offset, 128));
				block.CopyTo(decrypted.AsSpan(offset, 128));
			}

			return LoginCredentials.FromDecryptedBlocks(decrypted, encryptedLoginData.Length > 128);
		}
		catch (Exception)
		{
			return null;
		}
	}
}
