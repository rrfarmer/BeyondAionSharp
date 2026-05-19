using Aion.Commons.Network;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Aion.ServerPackets;
using Aion.LoginServer.Network.Crypto;

namespace Aion.LoginServer.Tests;

public class EncryptedLoginFrameTests
{
	[Fact]
	public void SerializeEncryptedFrame_WritesEncryptedLengthPrefixedServerPacket()
	{
		var sessionKey = new byte[] { 1, 3, 5, 7, 9, 11, 13, 15, 2, 4, 6, 8, 10, 12, 14, 16 };
		var serverEngine = PrimedEngine(sessionKey);
		var verifier = PrimedEngine(sessionKey);

		var frame = new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR).SerializeEncryptedFrame(serverEngine);
		var payload = frame[2..];

		Assert.Equal(frame.Length, frame[0] | frame[1] << 8);
		Assert.True(verifier.Decrypt(payload, 0, payload.Length));

		using var decrypted = new PacketBuffer(payload);
		Assert.Equal(0x01, decrypted.ReadC());
		Assert.Equal((int)AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR, decrypted.ReadD());
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
