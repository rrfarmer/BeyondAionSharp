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
	public void SmAuthGameGuard_MatchesJavaGeneratedPayloadVector()
	{
		var payload = new SmAuthGameGuard(0x11223344).SerializePayload();

		Assert.Equal(
			Convert.FromHexString("0B44332211000000000000000000000000000000000050CD00000000000000000B4463EF11000000"),
			payload);
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
	public void SmGameServerAuthResponse_WritesAuthedResponseWithServerCount()
	{
		var frame = new SmGameServerAuthResponse(GsAuthResponse.AUTHED, 3).SerializeFrame();

		Assert.Equal(new byte[] { 0x05, 0x00, 0x00, 0x00, 0x03 }, frame);
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
	public void SmPing_WritesJavaPayloadShape()
	{
		var payload = new SmPing().SerializePayload();

		Assert.Equal(new byte[] { 0x0B }, payload);
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
	public void GsFactory_AuthedStateReadsPremiumAndBanControls()
	{
		using var premiumPayload = new PacketBuffer();
		premiumPayload.WriteC(11);
		premiumPayload.WriteD(1);
		premiumPayload.WriteD(200);
		premiumPayload.WriteQ(500);
		premiumPayload.WriteC(3);

		var premium = Assert.IsType<CmPremiumControl>(GsClientPacketFactory.Create(new PacketBuffer(premiumPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1, premium.AccountId);
		Assert.Equal(200, premium.RequestId);
		Assert.Equal(500, premium.RequiredCost);
		Assert.Equal(3, premium.ServerId);

		using var macPayload = new PacketBuffer();
		macPayload.WriteC(10);
		macPayload.WriteC(1);
		macPayload.WriteS("aa-bb");
		macPayload.WriteS("reason");
		macPayload.WriteQ(1_700_000_000_000);

		var mac = Assert.IsType<CmMacBanControl>(GsClientPacketFactory.Create(new PacketBuffer(macPayload.ToArray()), GameServerConnectionState.Authed));
		Assert.Equal(1, mac.Type);
		Assert.Equal("aa-bb", mac.Address);
		Assert.Equal("reason", mac.Details);
	}

	[Fact]
	public void SmPremiumResponse_WritesJavaPayloadShape()
	{
		var payload = new SmPremiumResponse(200, 3, 1500).SerializePayload();

		Assert.Equal(17, payload.Length);
		Assert.Equal(10, payload[0]);
		Assert.Equal(new byte[] { 0xC8, 0x00, 0x00, 0x00 }, payload[1..5]);
		Assert.Equal(new byte[] { 0x03, 0x00, 0x00, 0x00 }, payload[5..9]);
		Assert.Equal(new byte[] { 0xDC, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, payload[9..17]);
	}

	[Fact]
	public void BanListPackets_WriteJavaPayloadShape()
	{
		var banTime = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000).UtcDateTime;
		var macPayload = new SmMacBanList(new[] { new BannedMacEntry("aa-bb", banTime, "reason") }).SerializePayload();
		using var macBuffer = new PacketBuffer(macPayload);
		Assert.Equal(9, macBuffer.ReadC());
		Assert.Equal(1, macBuffer.ReadD());
		Assert.Equal("aa-bb", macBuffer.ReadS());
		Assert.Equal(1_700_000_000_000, macBuffer.ReadQ());
		Assert.Equal("reason", macBuffer.ReadS());

		var hddPayload = new SmHddBanList(new Dictionary<string, DateTime> { ["disk"] = banTime }).SerializePayload();
		using var hddBuffer = new PacketBuffer(hddPayload);
		Assert.Equal(13, hddBuffer.ReadC());
		Assert.Equal(1, hddBuffer.ReadD());
		Assert.Equal("disk", hddBuffer.ReadS());
		Assert.Equal(1_700_000_000_000, hddBuffer.ReadQ());
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
	}

	[Fact]
	public void SmBanAndLoginServerControlResponses_WriteJavaPayloadShape()
	{
		var banPayload = new SmBanResponse(3, 99, "127.0.0.1", 15, 12345, true).SerializePayload();
		using var banBuffer = new PacketBuffer(banPayload);
		Assert.Equal(5, banBuffer.ReadC());
		Assert.Equal(3, banBuffer.ReadC());
		Assert.Equal(99, banBuffer.ReadD());
		Assert.Equal("127.0.0.1", banBuffer.ReadS());
		Assert.Equal(15, banBuffer.ReadD());
		Assert.Equal(12345, banBuffer.ReadD());
		Assert.Equal(1, banBuffer.ReadC());

		var controlPayload = new SmLoginServerControlResponse(1, 7, 99, 12345, true).SerializePayload();
		Assert.Equal(new byte[] { 0x04, 0x01, 0x07, 0x63, 0x00, 0x00, 0x00, 0x39, 0x30, 0x00, 0x00, 0x01 }, controlPayload);
	}

	[Fact]
	public void SmPlayerTransferResponses_WriteJavaPayloadShapes()
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
		Assert.Equal(new byte[] { 0x0C, 0x17, 0x00, 0x00, 0x00, 0x01, 0x02, 0x0A, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x1E, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00 }, performPayload);

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
		using var sendInfo = new PacketBuffer(sendInfoPayload);
		Assert.Equal(12, sendInfo.ReadC());
		Assert.Equal(20, sendInfo.ReadD());
		Assert.Equal(20, sendInfo.ReadD());
		Assert.Equal(40, sendInfo.ReadD());
		Assert.Equal("Character", sendInfo.ReadS());
		Assert.Equal("target", sendInfo.ReadS());
		Assert.Equal(3, sendInfo.ReadD());
		Assert.Equal(new byte[] { 1, 2, 3 }, sendInfo.ReadB(3));
	}
}
