using Aion.LoginServer.Network.Crypto;
using Aion.LoginServer.Network.Aion.ServerPackets;

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

	private static readonly byte[] RsaKey = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();

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
	public void SmInitEncryptedFrame_MatchesJavaGoldenVector()
	{
		var engine = CreateSessionEngine();

		var frame = new SmInit(RsaKey, SessionKey, 0x11223344).SerializeEncryptedFrame(engine);

		Assert.Equal(210, frame.Length);
		Assert.Equal(
			Hex(
				"D20071247EBD9E5575028AF9A6FB3D2193B3A98D3D89D2753883D251C088F131" +
				"29AD6C5A586271774A46F072927ECB8F55BBDCE63D4276A1132E68A39C1CA134" +
				"5E63E0B09256A8B14F281F751464E765791F133B43B9D258379842E2EB92130" +
				"9276B33705A6D41C362DC0A6305D2371839F14CCE4986B6F2B97C7858149AB" +
				"59148BBA270D7A39761431AD7ABBEC7756AA5531C23CEB8F7481226FBA0F0B" +
				"2DA25F4A8706E0D428AFBE00E4B8365CC3F9F7BD24B6F379089F57639D1A01" +
				"3FB3411988D805FEEC353FABCEDF8A971F3B9067375"),
			frame);
	}

	[Fact]
	public void LaterEncrypt_CanBeDecryptedByEnginePrimedWithSameSessionKey()
	{
		var encryptEngine = PrimedEngine(SessionKey);
		var originalPayload = new byte[] { 0x03, 0xE9, 0x03, 0x00, 0x00, 0x44, 0x33, 0x22, 0x11 };
		var encrypted = new byte[64];
		originalPayload.CopyTo(encrypted, 0);

		var encryptedLength = encryptEngine.Encrypt(encrypted, 0, originalPayload.Length);
		var encryptedBytes = encrypted[..encryptedLength].ToArray();

		var decryptOk = DecryptServerPayload(encryptedBytes, SessionKey);

		Assert.True(decryptOk);
		Assert.Equal(originalPayload, encryptedBytes[..originalPayload.Length]);
	}

	[Fact]
	public void Decrypt_AcceptsCapturedClientGameGuardChecksumShape()
	{
		var decryptedClientPayload = new byte[]
		{
			0x07, 0x5A, 0x1E, 0x45,
			0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00,
			0x00, 0x3F, 0x81, 0x80,
			0x07, 0x65, 0x9F, 0xC5,
			0x80, 0x3E, 0x81, 0x80,
		};
		var encrypted = decryptedClientPayload.ToArray();
		new BlowfishCipher(SessionKey).Cipher(encrypted);
		var decryptEngine = PrimedEngine(SessionKey);

		var decryptOk = decryptEngine.Decrypt(encrypted, 0, encrypted.Length);

		Assert.True(decryptOk);
		Assert.Equal(decryptedClientPayload, encrypted);
	}

	[Fact]
	public void Decrypt_ReturnsFalseForTamperedPacket()
	{
		var decryptEngine = PrimedEngine(SessionKey);
		var encrypted = EncryptClientPayload(SessionKey, new byte[] { 0x06, 0x08, 0x00, 0x00, 0x00 });
		var encryptedLength = encrypted.Length;
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

	private static byte[] EncryptClientPayload(byte[] sessionKey, byte[] rawPayload)
	{
		var payload = CreateClientChecksummedPayload(rawPayload);
		new BlowfishCipher(sessionKey).Cipher(payload);
		return payload;
	}

	private static byte[] CreateClientChecksummedPayload(byte[] rawPayload)
	{
		var length = rawPayload.Length + 8;
		if ((length & 7) != 0)
			length += 8 - (length & 7);

		var payload = new byte[length];
		rawPayload.CopyTo(payload, 0);
		var checksumOffset = length - 8;
		var xor = 0;
		for (var offset = 0; offset < checksumOffset; offset += 4)
			xor ^= BitConverter.ToInt32(payload, offset);
		BitConverter.GetBytes(xor).CopyTo(payload, checksumOffset);
		return payload;
	}

	private static bool DecryptServerPayload(byte[] encryptedPayload, byte[] sessionKey)
	{
		new BlowfishCipher(sessionKey).Decipher(encryptedPayload);
		return VerifyServerChecksum(encryptedPayload);
	}

	private static bool VerifyServerChecksum(byte[] payload)
	{
		var xor = 0;
		var checksumOffset = payload.Length - 4;
		for (var offset = 0; offset < checksumOffset; offset += 4)
			xor ^= BitConverter.ToInt32(payload, offset);
		return xor == BitConverter.ToInt32(payload, checksumOffset);
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
