using Aion.LoginServer.Model;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ServerPackets;
using Aion.LoginServer.Services;

namespace Aion.LoginServer.Tests;

public class GameServerRegistryTests
{
	[Fact]
	public void RegisterGameServer_AuthenticatesKnownServerAndMarksOnline()
	{
		var registry = new GameServerRegistry();
		registry.RegisterKnownServer(new GameServerInfo(1, "127.0.0.1", "secret"));

		var response = registry.RegisterGameServer(new GameServerAuthRequest(1, "secret", new byte[] { 127, 0, 0, 1 }, 7777, 0, 100), "127.0.0.1:50123");

		var server = registry.GetGameServer(1);
		Assert.Equal(GsAuthResponse.AUTHED, response);
		Assert.NotNull(server);
		Assert.True(server.IsOnline);
		Assert.Equal(7777, server.Port);
	}

	[Fact]
	public void RegisterGameServer_RejectsWrongPasswordOrIpMask()
	{
		var registry = new GameServerRegistry();
		registry.RegisterKnownServer(new GameServerInfo(1, "10.0.0.*", "secret"));

		var wrongPassword = registry.RegisterGameServer(new GameServerAuthRequest(1, "wrong", new byte[] { 127, 0, 0, 1 }, 7777, 0, 100), "10.0.0.5");
		var wrongIp = registry.RegisterGameServer(new GameServerAuthRequest(1, "secret", new byte[] { 127, 0, 0, 1 }, 7777, 0, 100), "127.0.0.1");

		Assert.Equal(GsAuthResponse.NOT_AUTHED, wrongPassword);
		Assert.Equal(GsAuthResponse.NOT_AUTHED, wrongIp);
		Assert.False(registry.GetGameServer(1)!.IsOnline);
	}

	[Fact]
	public async Task KickAccountFromGameServer_SendsJavaRequestKickPacket()
	{
		var registry = new GameServerRegistry();
		var session = new FakeGameServerSession();
		var server = new GameServerInfo(1, "127.0.0.1", "secret");
		registry.RegisterKnownServer(server);
		registry.RegisterGameServer(new GameServerAuthRequest(1, "secret", new byte[] { 127, 0, 0, 1 }, 7777, 0, 100), "127.0.0.1", session);
		server.AddAccount(new Account { Id = 7 });

		var kicked = await registry.KickAccountFromGameServerAsync(7, notifyDoubleLogin: true);

		Assert.True(kicked);
		Assert.NotNull(session.LastPacket);
		Assert.Equal(new byte[] { 0x02, 0x07, 0x00, 0x00, 0x00, 0x01 }, session.LastPacket!.SerializePayload());
	}

	[Fact]
	public async Task CharacterCountRequest_SeedsOfflineServersAndRequestsOnlineServers()
	{
		var registry = new GameServerRegistry();
		var session = new FakeGameServerSession();
		registry.RegisterKnownServer(new GameServerInfo(1, "127.0.0.1", "secret"));
		registry.RegisterKnownServer(new GameServerInfo(2, "127.0.0.1", "secret"));
		registry.RegisterGameServer(new GameServerAuthRequest(1, "secret", new byte[] { 127, 0, 0, 1 }, 7777, 0, 100), "127.0.0.1", session);

		var offlineCounts = registry.GetOfflineGameServerCharacterCounts();
		await registry.RequestOnlineGameServerCharacterCountsAsync(55);

		Assert.Equal(0, offlineCounts[2]);
		Assert.NotNull(session.LastPacket);
		Assert.Equal(new byte[] { 0x08, 0x37, 0x00, 0x00, 0x00 }, session.LastPacket!.SerializePayload());
	}

	private sealed class FakeGameServerSession : IGameServerSession
	{
		public GsServerPacket? LastPacket { get; private set; }

		public Task SendPacketAsync(GsServerPacket packet)
		{
			LastPacket = packet;
			return Task.CompletedTask;
		}
	}
}
