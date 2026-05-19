using System.Buffers.Binary;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Crypto;

namespace Aion.LoginServer.Tests;

public class LoginRsaCredentialTests
{
	[Fact]
	public void LoginRsaKeyPair_GeneratesScrambledModulusAndDecryptsRawBlock()
	{
		using var keyPair = LoginRsaKeyPair.Generate();
		var plain = new byte[128];
		plain[94] = (byte)'a';
		plain[95] = (byte)'c';
		plain[96] = (byte)'c';
		plain[108] = (byte)'p';
		plain[109] = (byte)'w';
		BinaryPrimitives.WriteInt32LittleEndian(plain.AsSpan(124, 4), -1);

		var encrypted = LoginRsaKeyPair.RawEncryptForTesting(plain, keyPair.PublicParameters);
		var decrypted = keyPair.DecryptNoPadding(encrypted);

		Assert.Equal(128, keyPair.EncryptedModulus.Length);
		Assert.Equal(plain, decrypted);
	}

	[Fact]
	public void CredentialDecryptor_ReadsNormalLoginLayout()
	{
		using var keyPair = LoginRsaKeyPair.Generate();
		var plain = new byte[128];
		WriteAscii(plain, 94, "account");
		WriteAscii(plain, 108, "password");
		BinaryPrimitives.WriteInt32LittleEndian(plain.AsSpan(124, 4), 123456);
		var encrypted = LoginRsaKeyPair.RawEncryptForTesting(plain, keyPair.PublicParameters);

		var credentials = LoginCredentialDecryptor.Decrypt(encrypted, keyPair);

		Assert.NotNull(credentials);
		Assert.Equal("account", credentials.Username);
		Assert.Equal("password", credentials.Password);
		Assert.Equal(123456, credentials.OneTimePassword);
	}

	private static void WriteAscii(byte[] buffer, int offset, string value)
	{
		for (var i = 0; i < value.Length; i++)
			buffer[offset + i] = (byte)value[i];
	}
}
