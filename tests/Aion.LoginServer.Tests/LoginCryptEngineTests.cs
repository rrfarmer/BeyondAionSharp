using Aion.LoginServer.Network.Crypto;

namespace Aion.LoginServer.Tests;

public class LoginCryptEngineTests
{
	private static readonly byte[] StaticKey =
	{
		0x6B, 0x60, 0xCB, 0x5B,
		0x82, 0xCE, 0x90, 0xB1,
		0xCC, 0x2B, 0x6C, 0x55,
		0x6C, 0x6C, 0x6C, 0x6C
	};

	[Fact]
	public void BlowfishCipher_RoundTripsFullBlocks()
	{
		var cipher = new BlowfishCipher(StaticKey);
		var original = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
		var data = (byte[])original.Clone();

		cipher.Cipher(data);
		Assert.NotEqual(original, data);

		cipher.Decipher(data);
		Assert.Equal(original, data);
	}

	[Fact]
	public void FirstEncrypt_UsesJavaPaddingAndUpdatesKeyAfterStaticKeyPacket()
	{
		var engine = new LoginCryptEngine(() => 0x01020304);
		engine.UpdateKey(new byte[] { 1, 3, 5, 7, 9, 11, 13, 15, 2, 4, 6, 8, 10, 12, 14, 16 });
		var packet = new byte[64];
		packet[0] = 0x00;
		packet[1] = 0x11;
		packet[2] = 0x22;

		var encryptedLength = engine.Encrypt(packet, 0, 3);

		// Java path: length + checksum(4) + first-packet XOR tail(4), then align by adding 8 - length % 8.
		Assert.Equal(16, encryptedLength);
		Assert.NotEqual(new byte[] { 0x00, 0x11, 0x22 }, packet[..3]);
	}

	[Fact]
	public void LaterEncrypt_CanBeDecryptedByEnginePrimedWithSameSessionKey()
	{
		var sessionKey = new byte[] { 1, 3, 5, 7, 9, 11, 13, 15, 2, 4, 6, 8, 10, 12, 14, 16 };
		var encryptEngine = PrimedEngine(sessionKey);
		var decryptEngine = PrimedEngine(sessionKey);
		var originalPayload = new byte[] { 0x03, 0xE9, 0x03, 0x00, 0x00, 0x44, 0x33, 0x22, 0x11 };
		var encrypted = new byte[64];
		originalPayload.CopyTo(encrypted, 0);

		var encryptedLength = encryptEngine.Encrypt(encrypted, 0, originalPayload.Length);
		var encryptedBytes = encrypted[..encryptedLength].ToArray();

		var decryptOk = decryptEngine.Decrypt(encryptedBytes, 0, encryptedLength);

		Assert.True(decryptOk);
		Assert.Equal(originalPayload, encryptedBytes[..originalPayload.Length]);
	}

	[Fact]
	public void Decrypt_ReturnsFalseForTamperedPacket()
	{
		var sessionKey = new byte[] { 1, 3, 5, 7, 9, 11, 13, 15, 2, 4, 6, 8, 10, 12, 14, 16 };
		var encryptEngine = PrimedEngine(sessionKey);
		var decryptEngine = PrimedEngine(sessionKey);
		var encrypted = new byte[64];
		new byte[] { 0x06, 0x08, 0x00, 0x00, 0x00 }.CopyTo(encrypted, 0);
		var encryptedLength = encryptEngine.Encrypt(encrypted, 0, 5);
		encrypted[3] ^= 0x7F;

		var decryptOk = decryptEngine.Decrypt(encrypted, 0, encryptedLength);

		Assert.False(decryptOk);
	}

	private static LoginCryptEngine PrimedEngine(byte[] sessionKey)
	{
		var engine = new LoginCryptEngine(() => 0x01020304);
		engine.UpdateKey(sessionKey);
		var firstPacket = new byte[64];
		firstPacket[0] = 0x00;
		engine.Encrypt(firstPacket, 0, 1);
		return engine;
	}
}
