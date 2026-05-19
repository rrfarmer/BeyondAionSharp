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

	private static readonly byte[] SessionKey =
	{
		1, 3, 5, 7, 9, 11, 13, 15,
		2, 4, 6, 8, 10, 12, 14, 16
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
	public void BlowfishCipher_MatchesJavaGoldenVector()
	{
		var cipher = new BlowfishCipher(StaticKey);
		var data = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

		cipher.Cipher(data);

		Assert.Equal(Hex("458EF8CB40966A791B9161DBC9042822"), data);
	}

	[Fact]
	public void FirstEncrypt_UsesJavaPaddingAndUpdatesKeyAfterStaticKeyPacket()
	{
		var engine = new LoginCryptEngine(() => 0x01020304);
		engine.UpdateKey(SessionKey);
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
	public void FirstEncrypt_MatchesJavaGoldenVector()
	{
		var engine = CreateSessionEngine();
		var packet = new byte[64];
		packet[0] = 0x00;
		packet[1] = 0x11;
		packet[2] = 0x22;

		var encryptedLength = engine.Encrypt(packet, 0, 3);

		Assert.Equal(16, encryptedLength);
		Assert.Equal(Hex("E0EC1DF408F551AA6F82C092934970B9"), packet[..encryptedLength]);
	}

	[Fact]
	public void LaterEncrypt_MatchesJavaGoldenVector()
	{
		var engine = CreateSessionEngine();
		var firstPacket = new byte[64];
		firstPacket[0] = 0x00;
		firstPacket[1] = 0x11;
		firstPacket[2] = 0x22;
		engine.Encrypt(firstPacket, 0, 3);
		var laterPacket = new byte[64];
		var laterPlain = new byte[] { 0x03, 0xE9, 0x03, 0x00, 0x00, 0x44, 0x33, 0x22, 0x11 };
		laterPlain.CopyTo(laterPacket, 0);

		var encryptedLength = engine.Encrypt(laterPacket, 0, laterPlain.Length);

		Assert.Equal(16, encryptedLength);
		Assert.Equal(Hex("9B406066E713C7631157BBF7D89CC550"), laterPacket[..encryptedLength]);
	}

	[Fact]
	public void LaterEncrypt_CanBeDecryptedByEnginePrimedWithSameSessionKey()
	{
		var encryptEngine = PrimedEngine(SessionKey);
		var decryptEngine = PrimedEngine(SessionKey);
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
		var encryptEngine = PrimedEngine(SessionKey);
		var decryptEngine = PrimedEngine(SessionKey);
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

	private static LoginCryptEngine CreateSessionEngine()
	{
		var engine = new LoginCryptEngine(() => 0x01020304);
		engine.UpdateKey(SessionKey);
		return engine;
	}

	private static byte[] Hex(string value)
	{
		return Convert.FromHexString(value);
	}
}
