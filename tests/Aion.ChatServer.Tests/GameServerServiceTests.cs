using Aion.ChatServer.Configuration;
using Aion.ChatServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.ChatServer.Tests;

public class GameServerServiceTests
{
	[Fact]
	public void RegisterGameServer_TracksSingleOnlineServer()
	{
		var service = new GameServerService(new ChatServerOptions { GameServerPassword = "secret" }, NullLogger<GameServerService>.Instance);

		Assert.Equal(GsAuthResponse.Authed, service.RegisterGameServer(1, "secret"));
		Assert.True(service.IsOnline);
		Assert.Equal((byte)1, service.GameServerId);
		Assert.Equal(GsAuthResponse.AlreadyRegistered, service.RegisterGameServer(2, "secret"));

		service.SetOffline();

		Assert.False(service.IsOnline);
		Assert.Null(service.GameServerId);
	}
}
