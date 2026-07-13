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
	public void AionFactory_ReadsJavaClientPacketShapes()
	{
		using var loginPayload = new PacketBuffer();
		loginPayload.WriteC(0x00);
		loginPayload.WriteB(Enumerable.Range(0, 128).Select(i => (byte)i).ToArray());
		loginPayload.WriteD(0x11223344);
		loginPayload.WriteB(new byte[16]);
		loginPayload.WriteB(new byte[] { 0x20, 0, 0, 0, 0, 0, 1 });
		loginPayload.WriteB(new byte[] { 0x9D, 0xDA, 0x47, 0xA7, 0x21, 0xC0, 0xA6, 0xA5, 0x4B, 0xB7, 0x5E, 0xE3, 0xCE, 0xC9, 0x26, 0xAA });
		loginPayload.WriteD(0);
		loginPayload.WriteD(unchecked((int)0x88776655));
		loginPayload.WriteD(0);

		var login = Assert.IsType<CmLogin>(AionClientPacketFactory.Create(new PacketBuffer(loginPayload.ToArray()), LoginClientState.AuthedGameGuard));
		Assert.Equal(Enumerable.Range(0, 128).Select(i => (byte)i), login.EncryptedLoginData);
		Assert.Equal(0x11223344, login.SessionId);

		using var serverListPayload = new PacketBuffer();
		serverListPayload.WriteC(0x05);
		serverListPayload.WriteD(1001);
		serverListPayload.WriteD(0x11223344);
		serverListPayload.WriteC(7);
		serverListPayload.WriteB(new byte[] { 1, 2, 3, 4, 5, 6 });
		serverListPayload.WriteD(0x01020304);
		serverListPayload.WriteD(60222);

		var serverList = Assert.IsType<CmServerList>(AionClientPacketFactory.Create(new PacketBuffer(serverListPayload.ToArray()), LoginClientState.AuthedLogin));
		Assert.Equal(1001, serverList.AccountId);
		Assert.Equal(0x11223344, serverList.LoginOk);

		using var playPayload = new PacketBuffer();
		playPayload.WriteC(0x02);
		playPayload.WriteD(1001);
		playPayload.WriteD(0x11223344);
		playPayload.WriteC(3);
		playPayload.WriteB(new byte[] { 6, 5, 4, 3, 2, 1 });
		playPayload.WriteQ(0x0102030405060708);

		var play = Assert.IsType<CmPlay>(AionClientPacketFactory.Create(new PacketBuffer(playPayload.ToArray()), LoginClientState.AuthedLogin));
		Assert.Equal(1001, play.AccountId);
		Assert.Equal(0x11223344, play.LoginOk);
		Assert.Equal(3, play.ServerId);
	}

	[Fact]
	public void AionFactory_ConnectedStateReadsUpdateSession()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x08);
		payload.WriteD(1001);
		payload.WriteD(0x11223344);
		payload.WriteD(0x55667788);
		payload.WriteC(68);
		payload.WriteB(new byte[] { 1, 2, 3, 4, 5, 6 });
		payload.WriteC(4);
		payload.WriteC(68);
		payload.WriteH(0x7788);

		var update = Assert.IsType<CmUpdateSession>(AionClientPacketFactory.Create(new PacketBuffer(payload.ToArray()), LoginClientState.Connected));

		Assert.Equal(1001, update.AccountId);
		Assert.Equal(0x11223344, update.LoginOk);
		Assert.Equal(0x55667788, update.ReconnectKey);
	}

	[Fact]
	public void SmAuthGameGuard_MatchesJavaGeneratedPayloadVector()
	{
		var payload = new SmAuthGameGuard(0x11223344).SerializePayload();

		Assert.Equal(
			Convert.FromHexString("0B44332211000000000000000000000000000000000050CD00000000000000000B4463EF11000000"),
			payload);
	}

	[Fact]
	public void AionFailureAndControlPackets_MatchJavaGeneratedPayloadVectors()
	{
		Assert.Equal(Convert.FromHexString("0103000000"), new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD).SerializePayload());
		Assert.Equal(Convert.FromHexString("0608000000"), new SmPlayFail(AionAuthResponse.STR_L2AUTH_S_SERVER_DOWN).SerializePayload());
		Assert.Equal(Convert.FromHexString("0816000000"), new SmAccountKick(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP).SerializePayload());
		Assert.Equal(Convert.FromHexString("02"), new SmAccountBanned().SerializePayload());
		Assert.Equal(Convert.FromHexString("09"), new SmAccountBanned2().SerializePayload());
	}

	[Fact]
	public void SmLoginOk_MatchesJavaGeneratedPayloadVector()
	{
		var sessionKey = new SessionKey(1001, 0x11223344, 0x01020304, 0x55667788);
		var payload = new SmLoginOk(sessionKey).SerializePayload();

		Assert.Equal(
			Convert.FromHexString(
				"03E9030000443322110000000000000000EA030000000000000000000000000000" +
				"0000000000000000000000000000000000000000000000000000000000000000000000"),
			payload);
	}

	[Fact]
	public void SmPlayOk_MatchesJavaGeneratedPayloadVector()
	{
		var sessionKey = new SessionKey(1001, 0x11223344, 0x01020304, 0x55667788);
		var payload = new SmPlayOk(sessionKey, 7).SerializePayload();

		Assert.Equal(Convert.FromHexString("070403020188776655070000000000000000000000000000"), payload);
	}

	[Fact]
	public void SmUpdateSession_MatchesJavaGeneratedPayloadVector()
	{
		var sessionKey = new SessionKey(1001, 0x11223344, 0x01020304, 0x55667788);
		var payload = new SmUpdateSession(sessionKey).SerializePayload();

		Assert.Equal(Convert.FromHexString("0CE90300004433221100"), payload);
	}

	[Fact]
	public void SmServerList_MatchesJavaGeneratedPayloadVector()
	{
		var server = new GameServerInfo(1, "127.0.0.1", "secret");
		server.MarkOnline(new byte[] { 127, 0, 0, 1 }, 7777, 0, 100);
		var payload = new SmServerList(new[] { server }, new Dictionary<byte, int> { [1] = 2 }, lastServer: 1).SerializePayload();

		Assert.Equal(
			Convert.FromHexString("040101017F000001611E00000000000064000101000000000200010200000000000000000000000000"),
			payload);
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
	public void SmGameServerAuthResponse_MatchesJavaGeneratedPayloadVectors()
	{
		var authedPayload = new SmGameServerAuthResponse(GsAuthResponse.AUTHED, 1).SerializePayload();
		var failedPayload = new SmGameServerAuthResponse(GsAuthResponse.NOT_AUTHED, 1).SerializePayload();

		Assert.Equal(Convert.FromHexString("000001"), authedPayload);
		Assert.Equal(Convert.FromHexString("0001"), failedPayload);
	}

	[Fact]
	public void SmAccountAuthAndReconnectResponses_MatchJavaGeneratedPayloadVectors()
	{
		var okPayload = new SmAccountAuthResponse(
			1001,
			ok: true,
			"player",
			1_700_000_000_000,
			1_111,
			2_222,
			accessLevel: 3,
			membership: 2,
			allowedHddSerial: "disk-1").SerializePayload();
		var failedPayload = new SmAccountAuthResponse(1001, ok: false).SerializePayload();
		var reconnectPayload = new SmAccountReconnectKey(1001, 0x11223344).SerializePayload();

		Assert.Equal(
			Convert.FromHexString(
				"01E90300000170006C00610079006500720000000068E5CF8B010000" +
				"5704000000000000AE0800000000000003026400690073006B002D0031000000"),
			okPayload);
		Assert.Equal(Convert.FromHexString("01E903000000"), failedPayload);
		Assert.Equal(Convert.FromHexString("03E903000044332211"), reconnectPayload);
	}

	[Fact]
	public void GsFactory_AuthedStateReadsCharacterCountResponse()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(8);
		payload.WriteD(123);
		payload.WriteC(4);

		var packet = GsClientPacketFactory.Create(new PacketBuffer(payload.ToArray()), GameServerConnectionState.Authed);

		var character = Assert.IsType<CmGameServerCharacter>(packet);
		Assert.Equal(123, character.AccountId);
		Assert.Equal(4, character.CharacterCount);
	}

	[Fact]
	public void GsFactory_AuthedStateReadsAccountListWithJavaIntCount()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(4);
		payload.WriteD(2);
		payload.WriteD(10);
		payload.WriteD(11);

		var packet = GsClientPacketFactory.Create(new PacketBuffer(payload.ToArray()), GameServerConnectionState.Authed);

		var list = Assert.IsType<CmAccountList>(packet);
		Assert.Equal(new[] { 10, 11 }, list.AccountIds);
	}

	[Fact]
	public void GsFactory_AuthedStateReadsAccountSessionAndSimpleAccountPackets()
	{
		using var authPayload = new PacketBuffer();
		authPayload.WriteC(1);
		authPayload.WriteD(1001);
		authPayload.WriteD(0x11223344);
		authPayload.WriteD(0x01020304);
		authPayload.WriteD(unchecked((int)0x88776655));

		var auth = Assert.IsType<CmAccountAuth>(GsClientPacketFactory.Create(new PacketBuffer(authPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1001, auth.SessionKey.AccountId);
		Assert.Equal(0x11223344, auth.SessionKey.LoginOk);
		Assert.Equal(0x01020304, auth.SessionKey.PlayOk1);
		Assert.Equal(unchecked((int)0x88776655), auth.SessionKey.PlayOk2);

		using var reconnectPayload = new PacketBuffer();
		reconnectPayload.WriteC(2);
		reconnectPayload.WriteD(1001);
		var reconnect = Assert.IsType<CmAccountReconnectKey>(GsClientPacketFactory.Create(new PacketBuffer(reconnectPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1001, reconnect.AccountId);

		using var disconnectedPayload = new PacketBuffer();
		disconnectedPayload.WriteC(3);
		disconnectedPayload.WriteD(1001);
		var disconnected = Assert.IsType<CmAccountDisconnected>(GsClientPacketFactory.Create(new PacketBuffer(disconnectedPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1001, disconnected.AccountId);

	}

	[Fact]
	public void SmGameServerCharacterResponse_MatchesJavaGeneratedPayloadVector()
	{
		var payload = new SmGameServerCharacterResponse(123).SerializePayload();

		Assert.Equal(Convert.FromHexString("087B000000"), payload);
	}

	[Fact]
	public void SmRequestKickAccount_MatchesJavaGeneratedPayloadVector()
	{
		var payload = new SmRequestKickAccount(123, notifyDoubleLogin: true).SerializePayload();

		Assert.Equal(Convert.FromHexString("027B00000001"), payload);
	}

	[Fact]
	public void SmPing_MatchesJavaGeneratedPayloadVector()
	{
		var payload = new SmPing().SerializePayload();

		Assert.Equal(Convert.FromHexString("0B"), payload);
	}

	[Fact]
	public void GameServerPingTracker_MatchesJavaUnansweredPingThreshold()
	{
		var tracker = new GameServerPingTracker();

		Assert.False(tracker.ShouldCloseOnPingTick());
		Assert.False(tracker.ShouldCloseOnPingTick());
		Assert.False(tracker.ShouldCloseOnPingTick());
		Assert.True(tracker.ShouldCloseOnPingTick());

		tracker.OnReceivePong();

		Assert.False(tracker.ShouldCloseOnPingTick());
	}

	[Fact]
	public void GsFactory_AuthedStateReadsAccountConnectionInfo()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(7);
		payload.WriteD(77);
		payload.WriteQ(1_700_000_000_000);
		payload.WriteS("127.0.0.1");
		payload.WriteS("aa-bb");
		payload.WriteS("disk");

		var packet = GsClientPacketFactory.Create(new PacketBuffer(payload.ToArray()), GameServerConnectionState.Authed);

		var info = Assert.IsType<CmAccountConnectionInfo>(packet);
		Assert.Equal(77, info.AccountId);
		Assert.Equal(1_700_000_000_000, info.Time);
		Assert.Equal("127.0.0.1", info.Ip);
		Assert.Equal("aa-bb", info.Mac);
		Assert.Equal("disk", info.HddSerial);
	}

	[Fact]
	public void GsFactory_AuthedStateReadsCompactedBanAndHddControls()
	{
		using var macPayload = new PacketBuffer();
		macPayload.WriteC(9);
		macPayload.WriteC(1);
		macPayload.WriteS("aa-bb");
		macPayload.WriteS("reason");
		macPayload.WriteQ(1_700_000_000_000);

		var mac = Assert.IsType<CmMacBanControl>(GsClientPacketFactory.Create(new PacketBuffer(macPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1, mac.Type);
		Assert.Equal("aa-bb", mac.Address);
		Assert.Equal("reason", mac.Details);
		Assert.Equal(1_700_000_000_000, mac.Time);

		using var hddPayload = new PacketBuffer();
		hddPayload.WriteC(10);
		hddPayload.WriteC(1);
		hddPayload.WriteS("disk");
		hddPayload.WriteQ(1_700_000_000_000);

		var hdd = Assert.IsType<CmHddBanControl>(GsClientPacketFactory.Create(new PacketBuffer(hddPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1, hdd.Type);
		Assert.Equal("disk", hdd.Address);
		Assert.Equal(1_700_000_000_000, hdd.Time);

		using var allowedHddPayload = new PacketBuffer();
		allowedHddPayload.WriteC(11);
		allowedHddPayload.WriteD(1001);
		allowedHddPayload.WriteS("allowed-disk");

		var allowedHdd = Assert.IsType<CmChangeAllowedHddSerial>(GsClientPacketFactory.Create(new PacketBuffer(allowedHddPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1001, allowedHdd.AccountId);
		Assert.Equal("allowed-disk", allowedHdd.HddSerial);

		using var pongPayload = new PacketBuffer();
		pongPayload.WriteC(12);
		Assert.IsType<CmGameServerPong>(GsClientPacketFactory.Create(new PacketBuffer(pongPayload.ToArray()), GameServerConnectionState.Authed));
	}

	[Fact]
	public void GsFactory_UnknownAuthedOpcodeMatchesJavaByReturningNoPacket()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0xFE);
		payload.WriteD(0x11223344);
		var buffer = new PacketBuffer(payload.ToArray());

		var packet = GsClientPacketFactory.Create(buffer, GameServerConnectionState.Authed);

		Assert.Null(packet);
		Assert.Equal(4, buffer.Remaining);
	}

	[Fact]
	public void BanListPackets_MatchJavaGeneratedPayloadVectors()
	{
		var banTime = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000).UtcDateTime;
		var macPayload = new SmMacBanList(new[] { new BannedMacEntry("aa-bb", banTime, "reason") }).SerializePayload();

		var hddPayload = new SmHddBanList(new Dictionary<string, DateTime> { ["disk"] = banTime }).SerializePayload();

		Assert.Equal(Convert.FromHexString("0901000000610061002D006200620000000068E5CF8B01000072006500610073006F006E000000"), macPayload);
		Assert.Equal(Convert.FromHexString("0A010000006400690073006B0000000068E5CF8B010000"), hddPayload);
	}

	[Fact]
	public void GsFactory_AuthedStateReadsBanAndLoginServerControl()
	{
		using var banPayload = new PacketBuffer();
		banPayload.WriteC(6);
		banPayload.WriteC(3);
		banPayload.WriteD(99);
		banPayload.WriteS("127.0.0.1");
		banPayload.WriteD(15);
		banPayload.WriteD(12345);

		var ban = Assert.IsType<CmBan>(GsClientPacketFactory.Create(new PacketBuffer(banPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(3, ban.Type);
		Assert.Equal(99, ban.AccountId);
		Assert.Equal("127.0.0.1", ban.Ip);
		Assert.Equal(15, ban.Time);
		Assert.Equal(12345, ban.AdminObjectId);

		using var controlPayload = new PacketBuffer();
		controlPayload.WriteC(5);
		controlPayload.WriteC(1);
		controlPayload.WriteC(7);
		controlPayload.WriteD(99);
		controlPayload.WriteD(12345);

		var control = Assert.IsType<CmLoginServerControl>(GsClientPacketFactory.Create(new PacketBuffer(controlPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1, control.Type);
		Assert.Equal(7, control.Param);
		Assert.Equal(99, control.AccountId);
		Assert.Equal(12345, control.AdminId);
	}

	[Fact]
	public void GsFactory_AuthedStateReadsPlayerTransferControlVariants()
	{
		using var requestPayload = new PacketBuffer();
		requestPayload.WriteC(13);
		requestPayload.WriteC(1);
		requestPayload.WriteD(5);
		requestPayload.WriteS("Character");
		requestPayload.WriteB(new byte[] { 1, 2, 3 });

		var request = Assert.IsType<CmPlayerTransferControl>(GsClientPacketFactory.Create(new PacketBuffer(requestPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1, request.ActionId);
		Assert.Equal(5, request.TaskId);
		Assert.Equal("Character", request.Name);
		Assert.Equal(new byte[] { 1, 2, 3 }, request.Db);

		using var errorPayload = new PacketBuffer();
		errorPayload.WriteC(13);
		errorPayload.WriteC(2);
		errorPayload.WriteD(5);
		errorPayload.WriteS("nope");

		var error = Assert.IsType<CmPlayerTransferControl>(GsClientPacketFactory.Create(new PacketBuffer(errorPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(2, error.ActionId);
		Assert.Equal(5, error.TaskId);
		Assert.Equal("nope", error.Reason);

		using var okPayload = new PacketBuffer();
		okPayload.WriteC(13);
		okPayload.WriteC(3);
		okPayload.WriteD(5);
		var ok = Assert.IsType<CmPlayerTransferControl>(GsClientPacketFactory.Create(new PacketBuffer(okPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(3, ok.ActionId);
		Assert.Equal(5, ok.TaskId);

		using var stopPayload = new PacketBuffer();
		stopPayload.WriteC(13);
		stopPayload.WriteC(4);
		stopPayload.WriteD(5);
		stopPayload.WriteS("stopped");
		var stop = Assert.IsType<CmPlayerTransferControl>(GsClientPacketFactory.Create(new PacketBuffer(stopPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(4, stop.ActionId);
		Assert.Equal(5, stop.TaskId);
		Assert.Equal("stopped", stop.Reason);
	}

	[Fact]
	public void SmBanAndLoginServerControlResponses_MatchJavaGeneratedPayloadVectors()
	{
		var banPayload = new SmBanResponse(3, 99, "127.0.0.1", 15, 12345, true).SerializePayload();
		Assert.Equal(Convert.FromHexString("0503630000003100320037002E0030002E0030002E00310000000F0000003930000001"), banPayload);

		var controlPayload = new SmLoginServerControlResponse(1, 7, 99, 12345, true).SerializePayload();
		Assert.Equal(Convert.FromHexString("040107630000003930000001"), controlPayload);
	}

	[Fact]
	public void SmPlayerTransferResponses_MatchJavaGeneratedPayloadVectors()
	{
		var performPayload = new SmPlayerTransferResponse(
			PlayerTransferResultStatus.PerformAction,
			new PlayerTransferTask
			{
				SourceServerId = 1,
				TargetServerId = 2,
				SourceAccountId = 10,
				TargetAccountId = 20,
				PlayerId = 30,
				Id = 40,
			}).SerializePayload();
		Assert.Equal(Convert.FromHexString("0C1700000001020A000000140000001E00000028000000"), performPayload);

		var sendInfoPayload = new SmPlayerTransferResponse(
			PlayerTransferResultStatus.SendInfo,
			new PlayerTransferRequest
			{
				TargetAccountId = 20,
				TaskId = 40,
				Name = "Character",
				TargetAccount = new Account { Name = "target" },
				Db = new byte[] { 1, 2, 3 },
			}).SerializePayload();
		var okPayload = new SmPlayerTransferResponse(PlayerTransferResultStatus.Ok, 40).SerializePayload();
		var errorPayload = new SmPlayerTransferResponse(PlayerTransferResultStatus.Error, 40, "nope").SerializePayload();

		Assert.Equal(
			Convert.FromHexString(
				"0C1400000014000000280000004300680061007200610063007400650072000000" +
				"740061007200670065007400000003000000010203"),
			sendInfoPayload);
		Assert.Equal(Convert.FromHexString("0C1500000028000000"), okPayload);
		Assert.Equal(Convert.FromHexString("0C16000000280000006E006F00700065000000"), errorPayload);
	}
}
