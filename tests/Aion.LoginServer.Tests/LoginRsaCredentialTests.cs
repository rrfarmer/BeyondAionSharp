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
	public void ScrambleModulus_MatchesJavaGoldenVector()
	{
		var modulus = Enumerable.Range(0, 128).Select(i => (byte)(0x80 + i)).ToArray();

		var scrambled = LoginRsaKeyPair.ScrambleModulus(modulus);

		Assert.Equal(
			Convert.FromHexString(
				"0D0F0D134040404040404040404D4F4D53404040404040404040404040404040" +
				"4040404040404040404040404040404040404040404040404040404040404040" +
				"CDCECFD08485868788898A8B8CCDCECFD09192939495969798999A9B9C9D9E" +
				"9FA0A1A2A3A4A5A6A7A8A9AAABACADAEAFB0B1B2B3B4B5B6B7B8B9BABBBCBDBEBF"),
			scrambled);
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

	[Fact]
	public void CredentialDecryptor_ReadsLoginExLayoutAcrossTwoBlocks()
	{
		using var keyPair = LoginRsaKeyPair.Generate();
		var username = "abcdefghijklmnopabcdefghijklmnopabcdefghijklmnopabcdefghijklmnop";
		var password = "12345678901234561234567890123456";
		var compacted = new byte[100];
		WriteAscii(compacted, 0, username);
		WriteAscii(compacted, 64, password);
		BinaryPrimitives.WriteInt32LittleEndian(compacted.AsSpan(96, 4), 654321);
		var plain = new byte[256];
		compacted.AsSpan(0, 50).CopyTo(plain.AsSpan(78, 50));
		compacted.AsSpan(50, 50).CopyTo(plain.AsSpan(206, 50));
		var encrypted = new byte[256];
		LoginRsaKeyPair.RawEncryptForTesting(plain.AsSpan(0, 128), keyPair.PublicParameters).CopyTo(encrypted, 0);
		LoginRsaKeyPair.RawEncryptForTesting(plain.AsSpan(128, 128), keyPair.PublicParameters).CopyTo(encrypted, 128);

		var credentials = LoginCredentialDecryptor.Decrypt(encrypted, keyPair);

		Assert.NotNull(credentials);
		Assert.Equal(username, credentials.Username);
		Assert.Equal(password, credentials.Password);
		Assert.Equal(654321, credentials.OneTimePassword);
	}

	private static void WriteAscii(byte[] buffer, int offset, string value)
	{
		for (var i = 0; i < value.Length; i++)
			buffer[offset + i] = (byte)value[i];
	}
}
