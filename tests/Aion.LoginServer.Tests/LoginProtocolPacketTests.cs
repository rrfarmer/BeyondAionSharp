using Aion.Commons.Network;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Aion.ClientPackets;
using Aion.LoginServer.Network.Aion.ServerPackets;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ClientPackets;
using Aion.LoginServer.Network.GameServer.ServerPackets;

namespace Aion.LoginServer.Tests;

public class LoginProtocolPacketTests
{
	[Fact]
	public void FrameCodec_LengthPrefixIncludesSizeField()
	{
		var frame = PacketFrameCodec.CreateFrame(new byte[] { 0x03, 0xAA, 0xBB });

		Assert.Equal(new byte[] { 0x05, 0x00, 0x03, 0xAA, 0xBB }, frame);
		var payload = PacketFrameCodec.CreatePayloadBuffer(frame);
		Assert.Equal(0x03, payload.ReadC());
		Assert.Equal(0xAA, payload.ReadC());
		Assert.Equal(0xBB, payload.ReadC());
	}

	[Fact]
	public void AionFactory_ConnectedStateReadsAuthGameGuard()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x07);
		payload.WriteD(0x01020304);
		payload.WriteD(0);
		payload.WriteD(0);
		payload.WriteD(0);
		payload.WriteD(0);
		payload.WriteB(new byte[0x0B]);

		var packet = AionClientPacketFactory.Create(new PacketBuffer(payload.ToArray()), LoginClientState.Connected);

		var auth = Assert.IsType<CmAuthGameGuard>(packet);
		Assert.Equal(0x01020304, auth.SessionId);
	}

	[Fact]
	public void AionFactory_RejectsLoginBeforeGameGuard()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x00);
		payload.WriteB(new byte[128 + 55]);

		var packet = AionClientPacketFactory.Create(new PacketBuffer(payload.ToArray()), LoginClientState.Connected);

		Assert.Null(packet);
	}

	[Fact]
	public void SmLoginOk_WritesJavaPayloadShape()
	{
		var sessionKey = new SessionKey(1001, 0x11223344, 0x55667788, 0x01020304);
		var payload = new SmLoginOk(sessionKey).SerializePayload();

		Assert.Equal(68, payload.Length);
		Assert.Equal(0x03, payload[0]);
		Assert.Equal(new byte[] { 0xE9, 0x03, 0x00, 0x00 }, payload[1..5]);
		Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, payload[5..9]);
		Assert.Equal(new byte[] { 0xEA, 0x03, 0x00, 0x00 }, payload[17..21]);
		Assert.All(payload[49..], value => Assert.Equal(0, value));
	}

	[Fact]
	public void SmPlayOk_WritesPlayKeysServerIdAndPadding()
	{
		var sessionKey = new SessionKey(1, 2, 0x01020304, 0x11223344);
		var payload = new SmPlayOk(sessionKey, 7).SerializePayload();

		Assert.Equal(24, payload.Length);
		Assert.Equal(0x07, payload[0]);
		Assert.Equal(new byte[] { 0x04, 0x03, 0x02, 0x01 }, payload[1..5]);
		Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, payload[5..9]);
		Assert.Equal(7, payload[9]);
		Assert.All(payload[10..], value => Assert.Equal(0, value));
	}

	[Fact]
	public void SmServerList_WritesRegisteredServersAndCharacterCounts()
	{
		var server = new GameServerInfo(1, "127.0.0.1", "secret");
		server.MarkOnline(new byte[] { 127, 0, 0, 1 }, 7777, 0, 100);
		var payload = new SmServerList(new[] { server }, new Dictionary<byte, int> { [1] = 2 }, lastServer: 1).SerializePayload();

		Assert.Equal(41, payload.Length);
		Assert.Equal(0x04, payload[0]);
		Assert.Equal(1, payload[1]);
		Assert.Equal(1, payload[2]);
		Assert.Equal(1, payload[3]);
		Assert.Equal(new byte[] { 127, 0, 0, 1 }, payload[4..8]);
		Assert.Equal(new byte[] { 0x61, 0x1E }, payload[8..10]);
		Assert.Equal(1, payload[18]); // online
		Assert.Equal(new byte[] { 0x02, 0x00 }, payload[24..26]); // maxIdWithChars + 1
		Assert.Equal(1, payload[26]); // auto-connect enabled
		Assert.Equal(2, payload[27]); // character count on server 1
	}

	[Fact]
	public void GsFactory_ConnectedStateReadsGameServerAuth()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0);
		payload.WriteC(1);
		payload.WriteS("secret");
		payload.WriteC(4);
		payload.WriteB(new byte[] { 127, 0, 0, 1 });
		payload.WriteH(7777);
		payload.WriteC(3);
		payload.WriteD(200);

		var packet = GsClientPacketFactory.Create(new PacketBuffer(payload.ToArray()), GameServerConnectionState.Connected);

		var auth = Assert.IsType<CmGameServerAuth>(packet);
		Assert.Equal(1, auth.GameServerId);
		Assert.Equal("secret", auth.Password);
		Assert.Equal(new byte[] { 127, 0, 0, 1 }, auth.Ip);
		Assert.Equal(7777, auth.Port);
		Assert.Equal(3, auth.MinAccessLevel);
		Assert.Equal(200, auth.MaxPlayers);
	}

	[Fact]
	public void SmGameServerAuthResponse_WritesAuthedResponseWithServerCount()
	{
		var frame = new SmGameServerAuthResponse(GsAuthResponse.AUTHED, 3).SerializeFrame();

		Assert.Equal(new byte[] { 0x05, 0x00, 0x00, 0x00, 0x03 }, frame);
	}
}
