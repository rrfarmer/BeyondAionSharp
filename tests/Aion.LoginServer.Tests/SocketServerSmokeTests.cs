using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Crypto;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ServerPackets;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.LoginServer.Tests;

public sealed class SocketServerSmokeTests
{
	private static readonly byte[] InitialLoginKey =
	{
		0x6B, 0x60, 0xCB, 0x5B,
		0x82, 0xCE, 0x90, 0xB1,
		0xCC, 0x2B, 0x6C, 0x55,
		0x6C, 0x6C, 0x6C, 0x6C
	};

	[Fact]
	public async Task LoginClientSocketServer_CompletesGameGuardAndLoginHandshakeThenClosesActiveConnectionOnStop()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		await AssertActiveConnectionsAsync(server.GetActiveConnections, 1);
		await CompleteLoginHandshakeAsync(client, keyGenerator, authService.Account.Id);
		Assert.Equal(1, authService.LoginAttempts);
		Assert.Equal(1, authService.CompletedLogins);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(client.GetStream());
		Assert.Equal(0, server.GetActiveConnections());
		Assert.Equal(1, authService.Logouts);
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_RejectsLoginWhenSessionIdDoesNotMatchInit()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		await AssertActiveConnectionsAsync(server.GetActiveConnections, 1);
		var stream = client.GetStream();
		var frame = await ReadFrameAsync(stream);
		Assert.Equal(210, frame.Length);
		var initPayload = DecryptFirstServerPayload(frame[2..]);
		var sessionId = BinaryPrimitives.ReadInt32LittleEndian(initPayload.AsSpan(1, 4));

		var clientEngine = CreatePrimedClientEngine(keyGenerator.BlowfishKey);
		await stream.WriteAsync(CreateEncryptedAuthGameGuardFrame(clientEngine, sessionId));
		var authGameGuardPayload = await ReadEncryptedLoginPayloadAsync(stream, clientEngine);
		Assert.Equal(0x0B, authGameGuardPayload[0]);
		Assert.Equal(sessionId, BinaryPrimitives.ReadInt32LittleEndian(authGameGuardPayload.AsSpan(1, 4)));

		await stream.WriteAsync(CreateEncryptedLoginFrame(clientEngine, keyGenerator.PublicParameters, sessionId ^ 0x01020304, "player", "secret"));
		var loginFailPayload = await ReadEncryptedLoginPayloadAsync(stream, clientEngine);
		Assert.Equal(0x01, loginFailPayload[0]);
		Assert.Equal((int)AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR, BinaryPrimitives.ReadInt32LittleEndian(loginFailPayload.AsSpan(1, 4)));
		Assert.Equal(0, authService.LoginAttempts);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(stream);
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_IgnoresShortLoginPacketAndKeepsConnectionOpen()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var stream = client.GetStream();
		var frame = await ReadFrameAsync(stream);
		var initPayload = DecryptFirstServerPayload(frame[2..]);
		var sessionId = BinaryPrimitives.ReadInt32LittleEndian(initPayload.AsSpan(1, 4));

		var clientEngine = CreatePrimedClientEngine(keyGenerator.BlowfishKey);
		await stream.WriteAsync(CreateEncryptedAuthGameGuardFrame(clientEngine, sessionId));
		var authGameGuardPayload = await ReadEncryptedLoginPayloadAsync(stream, clientEngine);
		Assert.Equal(0x0B, authGameGuardPayload[0]);

		await stream.WriteAsync(CreateEncryptedOpcodeOnlyFrame(clientEngine, 0x00));
		await stream.WriteAsync(CreateEncryptedLoginFrame(clientEngine, keyGenerator.PublicParameters, sessionId, "player", "secret"));
		var loginOkPayload = await ReadEncryptedLoginPayloadAsync(stream, clientEngine);

		Assert.Equal(0x03, loginOkPayload[0]);
		Assert.Equal(authService.Account.Id, BinaryPrimitives.ReadInt32LittleEndian(loginOkPayload.AsSpan(1, 4)));
		Assert.Equal(1, authService.LoginAttempts);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(stream);
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_BannedAccountSendsJavaBannedPacketAndCloses()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new AccountBannedLoginAuthService();
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var stream = client.GetStream();
		var frame = await ReadFrameAsync(stream);
		var initPayload = DecryptFirstServerPayload(frame[2..]);
		var sessionId = BinaryPrimitives.ReadInt32LittleEndian(initPayload.AsSpan(1, 4));

		var clientEngine = CreatePrimedClientEngine(keyGenerator.BlowfishKey);
		await stream.WriteAsync(CreateEncryptedAuthGameGuardFrame(clientEngine, sessionId));
		var authGameGuardPayload = await ReadEncryptedLoginPayloadAsync(stream, clientEngine);
		Assert.Equal(0x0B, authGameGuardPayload[0]);

		await stream.WriteAsync(CreateEncryptedLoginFrame(clientEngine, keyGenerator.PublicParameters, sessionId, "player", "secret"));
		var accountBannedPayload = await ReadEncryptedLoginPayloadAsync(stream, clientEngine);
		Assert.Equal(0x09, accountBannedPayload[0]);
		Assert.Equal(1, authService.LoginAttempts);
		await AssertClientClosedAsync(stream);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_RejectsGameGuardWhenSessionIdDoesNotMatchInit()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var stream = client.GetStream();
		var frame = await ReadFrameAsync(stream);
		var initPayload = DecryptFirstServerPayload(frame[2..]);
		var sessionId = BinaryPrimitives.ReadInt32LittleEndian(initPayload.AsSpan(1, 4));

		var clientEngine = CreatePrimedClientEngine(keyGenerator.BlowfishKey);
		await stream.WriteAsync(CreateEncryptedAuthGameGuardFrame(clientEngine, sessionId ^ 0x01020304));
		var loginFailPayload = await ReadEncryptedLoginPayloadAsync(stream, clientEngine);
		Assert.Equal(0x01, loginFailPayload[0]);
		Assert.Equal((int)AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR, BinaryPrimitives.ReadInt32LittleEndian(loginFailPayload.AsSpan(1, 4)));
		Assert.Equal(0, authService.LoginAttempts);
		await AssertClientClosedAsync(stream);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_DuplicateLoginKicksExistingSessionAndRejectsIncoming()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var firstClient = await ConnectWithRetryAsync(port);
		var firstLogin = await CompleteLoginHandshakeAsync(firstClient, keyGenerator, authService.Account.Id);

		using var secondClient = await ConnectWithRetryAsync(port);
		var secondStream = secondClient.GetStream();
		var secondFrame = await ReadFrameAsync(secondStream);
		var secondInitPayload = DecryptFirstServerPayload(secondFrame[2..]);
		var secondSessionId = BinaryPrimitives.ReadInt32LittleEndian(secondInitPayload.AsSpan(1, 4));
		var secondEngine = CreatePrimedClientEngine(keyGenerator.BlowfishKey);
		await secondStream.WriteAsync(CreateEncryptedAuthGameGuardFrame(secondEngine, secondSessionId));
		var authGameGuardPayload = await ReadEncryptedLoginPayloadAsync(secondStream, secondEngine);
		Assert.Equal(0x0B, authGameGuardPayload[0]);

		await secondStream.WriteAsync(CreateEncryptedLoginFrame(secondEngine, keyGenerator.PublicParameters, secondSessionId, "player", "secret"));
		var kickPayload = await ReadEncryptedLoginPayloadAsync(firstClient.GetStream(), firstLogin.Engine);
		var duplicateLoginPayload = await ReadEncryptedLoginPayloadAsync(secondStream, secondEngine);

		Assert.Equal(0x08, kickPayload[0]);
		Assert.Equal((int)AionAuthResponse.STR_L2AUTH_S_KICKED_DOUBLE_LOGIN, BinaryPrimitives.ReadInt32LittleEndian(kickPayload.AsSpan(1, 4)));
		await AssertClientClosedAsync(firstClient.GetStream());
		Assert.Equal(0x01, duplicateLoginPayload[0]);
		Assert.Equal((int)AionAuthResponse.STR_L2AUTH_S_ALREADY_LOGIN, BinaryPrimitives.ReadInt32LittleEndian(duplicateLoginPayload.AsSpan(1, 4)));
		Assert.Equal(2, authService.LoginAttempts);
		Assert.Equal(1, authService.Logouts);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(secondStream);
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_ClosesWithNoServerListWhenNoGameServersAreRegistered()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var login = await CompleteLoginHandshakeAsync(client, keyGenerator, authService.Account.Id);
		await client.GetStream().WriteAsync(CreateEncryptedServerListFrame(login.Engine, login.AccountId, login.LoginOk));
		var loginFailPayload = await ReadEncryptedLoginPayloadAsync(client.GetStream(), login.Engine);
		Assert.Equal(0x01, loginFailPayload[0]);
		Assert.Equal((int)AionAuthResponse.STR_L2AUTH_S_NO_SERVER_LIST, BinaryPrimitives.ReadInt32LittleEndian(loginFailPayload.AsSpan(1, 4)));
		await AssertClientClosedAsync(client.GetStream());
		Assert.Equal(1, authService.Logouts);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_ServerListClosesWithSystemErrorWhenSessionKeyIsWrong()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var login = await CompleteLoginHandshakeAsync(client, keyGenerator, authService.Account.Id);
		await client.GetStream().WriteAsync(CreateEncryptedServerListFrame(login.Engine, login.AccountId, login.LoginOk ^ 0x01020304));
		var loginFailPayload = await ReadEncryptedLoginPayloadAsync(client.GetStream(), login.Engine);

		Assert.Equal(0x01, loginFailPayload[0]);
		Assert.Equal((int)AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR, BinaryPrimitives.ReadInt32LittleEndian(loginFailPayload.AsSpan(1, 4)));
		await AssertClientClosedAsync(client.GetStream());
		Assert.Equal(1, authService.Logouts);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_ServerListIncludesRegisteredOfflineGameServers()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var gameServerRegistry = new GameServerRegistry();
		gameServerRegistry.RegisterKnownServer(new GameServerInfo(1, "127.0.0.1", "secret"));
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			gameServerRegistry);
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var login = await CompleteLoginHandshakeAsync(client, keyGenerator, authService.Account.Id);
		await client.GetStream().WriteAsync(CreateEncryptedServerListFrame(login.Engine, login.AccountId, login.LoginOk));
		var serverListPayload = await ReadEncryptedLoginPayloadAsync(client.GetStream(), login.Engine);

		Assert.Equal(0x04, serverListPayload[0]);
		Assert.Equal(1, serverListPayload[1]);
		Assert.Equal((byte)authService.Account.LastServer, serverListPayload[2]);
		Assert.Equal(1, serverListPayload[3]);
		Assert.Equal(0, serverListPayload[18]);
		Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(serverListPayload.AsSpan(24, 2)));
		Assert.Equal(1, serverListPayload[26]);
		Assert.Equal(0, serverListPayload[27]);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(client.GetStream());
		Assert.Equal(1, authService.Logouts);
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_UpdateSessionRestoresReconnectingAccount()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var sessionRegistry = new LoginSessionRegistry();
		var reconnectingAccount = authService.Account;
		sessionRegistry.AddReconnectingAccount(new ReconnectingAccount(reconnectingAccount, 0x11223344));
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			sessionRegistry,
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var stream = client.GetStream();
		await ReadFrameAsync(stream);
		var clientEngine = CreatePrimedClientEngine(keyGenerator.BlowfishKey);
		await stream.WriteAsync(CreateEncryptedUpdateSessionFrame(clientEngine, reconnectingAccount.Id, loginOk: unchecked((int)0x88776655), reconnectKey: 0x11223344));
		var updatePayload = await ReadEncryptedLoginPayloadAsync(stream, clientEngine);

		Assert.Equal(0x0C, updatePayload[0]);
		Assert.Equal(reconnectingAccount.Id, BinaryPrimitives.ReadInt32LittleEndian(updatePayload.AsSpan(1, 4)));
		Assert.Equal(0, updatePayload[9]);
		var session = sessionRegistry.GetLoginSession(reconnectingAccount.Id);
		Assert.NotNull(session);
		Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(updatePayload.AsSpan(5, 4)), session.SessionKey.LoginOk);
		Assert.Equal(0, authService.LoginAttempts);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(stream);
		Assert.Equal(1, authService.Logouts);
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_UpdateSessionClosesWhenReconnectKeyIsWrong()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var sessionRegistry = new LoginSessionRegistry();
		var reconnectingAccount = authService.Account;
		sessionRegistry.AddReconnectingAccount(new ReconnectingAccount(reconnectingAccount, 0x11223344));
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			sessionRegistry,
			new GameServerRegistry());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var stream = client.GetStream();
		await ReadFrameAsync(stream);
		var clientEngine = CreatePrimedClientEngine(keyGenerator.BlowfishKey);
		await stream.WriteAsync(CreateEncryptedUpdateSessionFrame(clientEngine, reconnectingAccount.Id, loginOk: unchecked((int)0x88776655), reconnectKey: 0x55667788));

		await AssertClientClosedAsync(stream);
		Assert.False(sessionRegistry.TryConsumeReconnectingAccount(reconnectingAccount.Id, 0x11223344, out _));
		Assert.Null(sessionRegistry.GetLoginSession(reconnectingAccount.Id));
		Assert.Equal(0, authService.LoginAttempts);
		Assert.Equal(0, authService.Logouts);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Theory]
	[InlineData("offline", AionAuthResponse.STR_L2AUTH_S_SERVER_DOWN)]
	[InlineData("access", AionAuthResponse.STR_L2AUTH_S_SEVER_CHECK)]
	[InlineData("full", AionAuthResponse.STR_L2AUTH_S_LIMIT_EXCEED)]
	public async Task LoginClientSocketServer_PlaySelectionReturnsJavaFailureResponses(string mode, AionAuthResponse expectedResponse)
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var gameServerRegistry = new GameServerRegistry();
		var gameServer = new GameServerInfo(1, "127.0.0.1", "secret");
		gameServerRegistry.RegisterKnownServer(gameServer);
		if (mode != "offline")
		{
			var minAccessLevel = mode == "access" ? (byte)1 : (byte)0;
			var maxPlayers = mode == "full" ? 0 : 100;
			Assert.Equal(
				GsAuthResponse.AUTHED,
				gameServerRegistry.RegisterGameServer(
					new GameServerAuthRequest(1, "secret", new byte[] { 127, 0, 0, 1 }, 7777, minAccessLevel, maxPlayers),
					"127.0.0.1:7777",
					new NoopGameServerSession()));
		}

		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			gameServerRegistry);
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var login = await CompleteLoginHandshakeAsync(client, keyGenerator, authService.Account.Id);
		await client.GetStream().WriteAsync(CreateEncryptedPlayFrame(login.Engine, login.AccountId, login.LoginOk, serverId: 1));
		var playFailPayload = await ReadEncryptedLoginPayloadAsync(client.GetStream(), login.Engine);

		Assert.Equal(0x06, playFailPayload[0]);
		Assert.Equal((int)expectedResponse, BinaryPrimitives.ReadInt32LittleEndian(playFailPayload.AsSpan(1, 4)));

		await server.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(client.GetStream());
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginClientSocketServer_PlaySelectionClosesWhenSessionKeyIsWrong()
	{
		var port = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		var gameServerRegistry = new GameServerRegistry();
		var gameServer = new GameServerInfo(1, "127.0.0.1", "secret");
		gameServerRegistry.RegisterKnownServer(gameServer);
		gameServerRegistry.RegisterGameServer(
			new GameServerAuthRequest(1, "secret", new byte[] { 127, 0, 0, 1 }, 7777, 0, 100),
			"127.0.0.1:7777",
			new NoopGameServerSession());
		var server = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			new LoginSessionRegistry(),
			gameServerRegistry);
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var login = await CompleteLoginHandshakeAsync(client, keyGenerator, authService.Account.Id);
		await client.GetStream().WriteAsync(CreateEncryptedPlayFrame(login.Engine, login.AccountId, login.LoginOk ^ 0x01020304, serverId: 1));
		var loginFailPayload = await ReadEncryptedLoginPayloadAsync(client.GetStream(), login.Engine);

		Assert.Equal(0x01, loginFailPayload[0]);
		Assert.Equal((int)AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR, BinaryPrimitives.ReadInt32LittleEndian(loginFailPayload.AsSpan(1, 4)));
		await AssertClientClosedAsync(client.GetStream());

		await server.StopAsync(TimeSpan.FromSeconds(1));
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task LoginAndGameServerSockets_RouteServerListCharacterCountsAndPlaySelection()
	{
		var loginPort = GetFreeLoopbackPort();
		var gameServerPort = GetFreeLoopbackPort();
		using var keyGenerator = new FixedLoginKeyGenerator();
		var authService = new SuccessfulLoginAuthService();
		authService.Account.AllowedHddSerial = "disk-1";
		var sessionRegistry = new LoginSessionRegistry();
		var gameServerRegistry = new GameServerRegistry();
		var accountRepository = new TrackingAccountRepository();
		var premiumRepository = new FixedPremiumRepository(1_500);
		var gameServer = new GameServerInfo(1, "127.0.0.1", "secret");
		gameServerRegistry.RegisterKnownServer(gameServer);
		var loginServer = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, loginPort),
				MaxClientConnections = 10,
			},
			keyGenerator,
			authService,
			sessionRegistry,
			gameServerRegistry);
		var gameServerSocketServer = new GameServerSocketServer(
			NullLogger<GameServerSocketServer>.Instance,
			new LoginServerOptions
			{
				GameServerEndPoint = new IPEndPoint(IPAddress.Loopback, gameServerPort),
				MaxGameServerConnections = 10,
			},
			gameServerRegistry,
			sessionRegistry,
			accountRepository,
			new ThrowingAccountTimeRepository(),
			new EmptyBannedIpService(),
			premiumRepository,
			new ThrowingAccountsLogRepository(),
			new ThrowingLoginAuthService(),
			new EmptyBannedMacService(),
			new EmptyBannedHddService(),
			new ThrowingPlayerTransferService());
		var loginServerTask = loginServer.StartAsync();
		var gameServerTask = gameServerSocketServer.StartAsync();

		using var fakeGameServer = await ConnectWithRetryAsync(gameServerPort);
		await fakeGameServer.GetStream().WriteAsync(CreateGameServerAuthFrame());
		Assert.Equal(new byte[] { 0x05, 0x00, 0x00, 0x00, 0x01 }, await ReadFrameAsync(fakeGameServer.GetStream()));
		Assert.True(gameServer.IsOnline);

		using var fakeClient = await ConnectWithRetryAsync(loginPort);
		var login = await CompleteLoginHandshakeAsync(fakeClient, keyGenerator, authService.Account.Id);
		Assert.Equal(1, authService.LoginAttempts);
		Assert.Equal(1, authService.CompletedLogins);

		await fakeClient.GetStream().WriteAsync(CreateEncryptedServerListFrame(login.Engine, login.AccountId, login.LoginOk));
		var characterRequestFrame = await ReadFrameAsync(fakeGameServer.GetStream());
		Assert.Equal(PacketFrameCodec.CreateFrame(new byte[] { 0x08, 0x2A, 0x00, 0x00, 0x00 }), characterRequestFrame);
		await fakeGameServer.GetStream().WriteAsync(CreateGameServerCharacterFrame(login.AccountId, 3));

		var serverListPayload = await ReadEncryptedLoginPayloadAsync(fakeClient.GetStream(), login.Engine);
		Assert.Equal(0x04, serverListPayload[0]);
		Assert.Equal(1, serverListPayload[1]);
		Assert.Equal(0xFF, serverListPayload[2]);
		Assert.Equal(1, serverListPayload[3]);
		Assert.Equal(new byte[] { 127, 0, 0, 1 }, serverListPayload[4..8]);
		Assert.Equal(new byte[] { 0x61, 0x1E }, serverListPayload[8..10]);
		Assert.Equal(1, serverListPayload[18]);
		Assert.Equal(new byte[] { 0x02, 0x00 }, serverListPayload[24..26]);
		Assert.Equal(3, serverListPayload[27]);

		await fakeClient.GetStream().WriteAsync(CreateEncryptedPlayFrame(login.Engine, login.AccountId, login.LoginOk, serverId: 1));
		var playOkPayload = await ReadEncryptedLoginPayloadAsync(fakeClient.GetStream(), login.Engine);
		Assert.Equal(0x07, playOkPayload[0]);
		Assert.Equal(1, playOkPayload[9]);

		var playOk1 = BinaryPrimitives.ReadInt32LittleEndian(playOkPayload.AsSpan(1, 4));
		var playOk2 = BinaryPrimitives.ReadInt32LittleEndian(playOkPayload.AsSpan(5, 4));
		await fakeGameServer.GetStream().WriteAsync(CreateAccountAuthFrame(login.AccountId, login.LoginOk, playOk1, playOk2));
		var accountAuthFrame = await ReadFrameAsync(fakeGameServer.GetStream());
		using (var accountAuthPayload = new PacketBuffer(accountAuthFrame[2..]))
		{
			Assert.Equal(1, accountAuthPayload.ReadC());
			Assert.Equal(login.AccountId, accountAuthPayload.ReadD());
			Assert.Equal(1, accountAuthPayload.ReadC());
			Assert.Equal("player", accountAuthPayload.ReadS());
			Assert.Equal(new DateTimeOffset(authService.Account.CreationDate).ToUnixTimeMilliseconds(), accountAuthPayload.ReadQ());
			Assert.Equal(authService.Account.AccountTime.AccumulatedOnlineTime, accountAuthPayload.ReadQ());
			Assert.Equal(authService.Account.AccountTime.AccumulatedRestTime, accountAuthPayload.ReadQ());
			Assert.Equal(authService.Account.AccessLevel, accountAuthPayload.ReadC());
			Assert.Equal(authService.Account.Membership, accountAuthPayload.ReadC());
			Assert.Equal(1_500, accountAuthPayload.ReadQ());
			Assert.Equal("disk-1", accountAuthPayload.ReadS());
		}
		Assert.True(gameServer.IsAccountOnGameServer(login.AccountId));
		Assert.Equal((login.AccountId, (sbyte)1), accountRepository.LastServerUpdate);

		await loginServer.StopAsync(TimeSpan.FromSeconds(1));
		await gameServerSocketServer.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(fakeClient.GetStream());
		await AssertClientClosedAsync(fakeGameServer.GetStream());
		Assert.False(gameServer.IsOnline);
		Assert.Equal(0, loginServer.GetActiveConnections());
		Assert.Equal(0, gameServerSocketServer.GetActiveConnections());
		Assert.Equal(0, authService.Logouts);
		await AssertTaskCompletedAsync(loginServerTask);
		await AssertTaskCompletedAsync(gameServerTask);
	}

	[Fact]
	public async Task GameServerSocketServer_AuthenticatesRegisteredServerAndMarksItOfflineOnStop()
	{
		var port = GetFreeLoopbackPort();
		var registry = new GameServerRegistry();
		var gameServer = new GameServerInfo(1, "127.0.0.1", "secret");
		registry.RegisterKnownServer(gameServer);
		var server = new GameServerSocketServer(
			NullLogger<GameServerSocketServer>.Instance,
			new LoginServerOptions
			{
				GameServerEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxGameServerConnections = 10,
			},
			registry,
			new LoginSessionRegistry(),
			new ThrowingAccountRepository(),
			new ThrowingAccountTimeRepository(),
			new EmptyBannedIpService(),
			new ThrowingPremiumRepository(),
			new ThrowingAccountsLogRepository(),
			new ThrowingLoginAuthService(),
			new EmptyBannedMacService(),
			new EmptyBannedHddService(),
			new ThrowingPlayerTransferService());
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		await client.GetStream().WriteAsync(CreateGameServerAuthFrame());
		var frame = await ReadFrameAsync(client.GetStream());

		Assert.Equal(new byte[] { 0x05, 0x00, 0x00, 0x00, 0x01 }, frame);
		Assert.True(gameServer.IsOnline);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(client.GetStream());
		Assert.False(gameServer.IsOnline);
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Theory]
	[InlineData("unregistered")]
	[InlineData("wrong-password")]
	[InlineData("wrong-ip")]
	public async Task GameServerSocketServer_RejectsInvalidAuthAndClosesWithJavaResponse(string mode)
	{
		var port = GetFreeLoopbackPort();
		var registry = new GameServerRegistry();
		var gameServer = new GameServerInfo(1, mode == "wrong-ip" ? "10.0.0.1" : "127.0.0.1", "secret");
		if (mode != "unregistered")
			registry.RegisterKnownServer(gameServer);
		var server = CreateGameServerSocketServer(port, registry);
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		var serverId = mode == "unregistered" ? (byte)2 : (byte)1;
		var password = mode == "wrong-password" ? "bad-secret" : "secret";
		await client.GetStream().WriteAsync(CreateGameServerAuthFrame(serverId, password));
		var frame = await ReadFrameAsync(client.GetStream());

		Assert.Equal(CreateGameServerAuthResponseFrame(GsAuthResponse.NOT_AUTHED, registeredServerCount: mode == "unregistered" ? 0 : 1), frame);
		await AssertClientClosedAsync(client.GetStream());
		Assert.False(gameServer.IsOnline);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task GameServerSocketServer_ShortAuthPacketUsesJavaDefaultReadsAndClosesWithNotAuthed()
	{
		var port = GetFreeLoopbackPort();
		var registry = new GameServerRegistry();
		var gameServer = new GameServerInfo(1, "127.0.0.1", "secret");
		registry.RegisterKnownServer(gameServer);
		var server = CreateGameServerSocketServer(port, registry);
		var serverTask = server.StartAsync();

		using var client = await ConnectWithRetryAsync(port);
		await client.GetStream().WriteAsync(PacketFrameCodec.CreateFrame(new byte[] { 0 }));
		var frame = await ReadFrameAsync(client.GetStream());

		Assert.Equal(CreateGameServerAuthResponseFrame(GsAuthResponse.NOT_AUTHED, 1), frame);
		await AssertClientClosedAsync(client.GetStream());
		Assert.False(gameServer.IsOnline);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	[Fact]
	public async Task GameServerSocketServer_RejectsDuplicateRegistrationAndKeepsFirstServerOnline()
	{
		var port = GetFreeLoopbackPort();
		var registry = new GameServerRegistry();
		var gameServer = new GameServerInfo(1, "127.0.0.1", "secret");
		registry.RegisterKnownServer(gameServer);
		var server = CreateGameServerSocketServer(port, registry);
		var serverTask = server.StartAsync();

		using var firstClient = await ConnectWithRetryAsync(port);
		await firstClient.GetStream().WriteAsync(CreateGameServerAuthFrame());
		Assert.Equal(CreateGameServerAuthResponseFrame(GsAuthResponse.AUTHED, 1), await ReadFrameAsync(firstClient.GetStream()));
		Assert.True(gameServer.IsOnline);

		using var duplicateClient = await ConnectWithRetryAsync(port);
		await duplicateClient.GetStream().WriteAsync(CreateGameServerAuthFrame());
		Assert.Equal(CreateGameServerAuthResponseFrame(GsAuthResponse.ALREADY_REGISTERED, 1), await ReadFrameAsync(duplicateClient.GetStream()));
		await AssertClientClosedAsync(duplicateClient.GetStream());
		Assert.True(gameServer.IsOnline);

		await server.StopAsync(TimeSpan.FromSeconds(1));
		await AssertClientClosedAsync(firstClient.GetStream());
		Assert.False(gameServer.IsOnline);
		Assert.Equal(0, server.GetActiveConnections());
		await AssertTaskCompletedAsync(serverTask);
	}

	private static GameServerSocketServer CreateGameServerSocketServer(int port, GameServerRegistry registry)
	{
		return new GameServerSocketServer(
			NullLogger<GameServerSocketServer>.Instance,
			new LoginServerOptions
			{
				GameServerEndPoint = new IPEndPoint(IPAddress.Loopback, port),
				MaxGameServerConnections = 10,
			},
			registry,
			new LoginSessionRegistry(),
			new ThrowingAccountRepository(),
			new ThrowingAccountTimeRepository(),
			new EmptyBannedIpService(),
			new ThrowingPremiumRepository(),
			new ThrowingAccountsLogRepository(),
			new ThrowingLoginAuthService(),
			new EmptyBannedMacService(),
			new EmptyBannedHddService(),
			new ThrowingPlayerTransferService());
	}

	private static byte[] CreateGameServerAuthFrame(byte gameServerId = 1, string password = "secret")
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0);
		payload.WriteC(gameServerId);
		payload.WriteS(password);
		payload.WriteC(4);
		payload.WriteB(new byte[] { 127, 0, 0, 1 });
		payload.WriteH(7777);
		payload.WriteC(0);
		payload.WriteD(100);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateGameServerAuthResponseFrame(GsAuthResponse response, int registeredServerCount)
	{
		return PacketFrameCodec.CreateFrame(new SmGameServerAuthResponse(response, registeredServerCount).SerializePayload());
	}

	private static byte[] CreateGameServerCharacterFrame(int accountId, byte characterCount)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(8);
		payload.WriteD(accountId);
		payload.WriteC(characterCount);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateAccountAuthFrame(int accountId, int loginOk, int playOk1, int playOk2)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(1);
		payload.WriteD(accountId);
		payload.WriteD(loginOk);
		payload.WriteD(playOk1);
		payload.WriteD(playOk2);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateEncryptedAuthGameGuardFrame(LoginCryptEngine cryptEngine, int sessionId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x07);
		payload.WriteD(sessionId);
		payload.WriteD(0);
		payload.WriteD(0);
		payload.WriteD(0);
		payload.WriteD(0);
		payload.WriteB(new byte[0x0B]);

		var rawPayload = payload.ToArray();
		var encryptedPayload = new byte[rawPayload.Length + 16];
		rawPayload.CopyTo(encryptedPayload, 0);
		var encryptedLength = cryptEngine.Encrypt(encryptedPayload, 0, rawPayload.Length);
		return PacketFrameCodec.CreateFrame(encryptedPayload.AsSpan(0, encryptedLength));
	}

	private static byte[] CreateEncryptedServerListFrame(LoginCryptEngine cryptEngine, int accountId, int loginOk)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x05);
		payload.WriteD(accountId);
		payload.WriteD(loginOk);
		payload.WriteC(0);
		payload.WriteB(new byte[6]);
		payload.WriteD(0);
		payload.WriteD(0);
		return EncryptLoginPayload(cryptEngine, payload.ToArray());
	}

	private static byte[] CreateEncryptedPlayFrame(LoginCryptEngine cryptEngine, int accountId, int loginOk, byte serverId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x02);
		payload.WriteD(accountId);
		payload.WriteD(loginOk);
		payload.WriteC(serverId);
		payload.WriteB(new byte[6]);
		payload.WriteQ(0);
		return EncryptLoginPayload(cryptEngine, payload.ToArray());
	}

	private static byte[] CreateEncryptedOpcodeOnlyFrame(LoginCryptEngine cryptEngine, byte opcode)
	{
		return EncryptLoginPayload(cryptEngine, new[] { opcode });
	}

	private static byte[] CreateEncryptedUpdateSessionFrame(LoginCryptEngine cryptEngine, int accountId, int loginOk, int reconnectKey)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x08);
		payload.WriteD(accountId);
		payload.WriteD(loginOk);
		payload.WriteD(reconnectKey);
		payload.WriteC(68);
		payload.WriteB(new byte[] { 1, 2, 3, 4, 5, 6 });
		payload.WriteC(4);
		payload.WriteC(68);
		payload.WriteH(0x7788);
		return EncryptLoginPayload(cryptEngine, payload.ToArray());
	}

	private static byte[] EncryptLoginPayload(LoginCryptEngine cryptEngine, byte[] rawPayload)
	{
		var encryptedPayload = new byte[rawPayload.Length + 16];
		rawPayload.CopyTo(encryptedPayload, 0);
		var encryptedLength = cryptEngine.Encrypt(encryptedPayload, 0, rawPayload.Length);
		return PacketFrameCodec.CreateFrame(encryptedPayload.AsSpan(0, encryptedLength));
	}

	private static byte[] CreateEncryptedLoginFrame(LoginCryptEngine cryptEngine, System.Security.Cryptography.RSAParameters publicParameters, int sessionId, string username, string password)
	{
		var plainCredentials = new byte[128];
		WriteAscii(plainCredentials, 94, username);
		WriteAscii(plainCredentials, 108, password);
		BinaryPrimitives.WriteInt32LittleEndian(plainCredentials.AsSpan(124, 4), -1);
		var encryptedCredentials = LoginRsaKeyPair.RawEncryptForTesting(plainCredentials, publicParameters);

		using var payload = new PacketBuffer();
		payload.WriteC(0x00);
		payload.WriteB(encryptedCredentials);
		payload.WriteD(sessionId);
		payload.WriteB(new byte[16]);
		payload.WriteB(new byte[] { 0x20, 0, 0, 0, 0, 0, 1 });
		payload.WriteB(new byte[] { 0x9D, 0xDA, 0x47, 0xA7, 0x21, 0xC0, 0xA6, 0xA5, 0x4B, 0xB7, 0x5E, 0xE3, 0xCE, 0xC9, 0x26, 0xAA });
		payload.WriteD(0);

		return EncryptLoginPayload(cryptEngine, payload.ToArray());
	}

	private static void WriteAscii(byte[] buffer, int offset, string value)
	{
		for (var i = 0; i < value.Length; i++)
			buffer[offset + i] = (byte)value[i];
	}

	private static LoginCryptEngine CreatePrimedClientEngine(byte[] blowfishKey)
	{
		var engine = new LoginCryptEngine(() => 0x01020304);
		engine.UpdateKey(blowfishKey);
		var firstServerPacket = new byte[64];
		firstServerPacket[0] = 0x00;
		engine.Encrypt(firstServerPacket, 0, 1);
		return engine;
	}

	internal static async Task<(LoginCryptEngine Engine, int AccountId, int LoginOk)> CompleteLoginHandshakeAsync(
		TcpClient client,
		FixedLoginKeyGenerator keyGenerator,
		int expectedAccountId,
		string username = "player",
		string password = "secret")
	{
		var frame = await ReadFrameAsync(client.GetStream());
		Assert.Equal(210, frame.Length);
		Assert.Equal(210, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(0, 2)));
		var initPayload = DecryptFirstServerPayload(frame[2..]);
		var sessionId = BinaryPrimitives.ReadInt32LittleEndian(initPayload.AsSpan(1, 4));
		Assert.Equal(0x00, initPayload[0]);
		Assert.Equal(0x0000C621, BinaryPrimitives.ReadInt32LittleEndian(initPayload.AsSpan(5, 4)));
		Assert.Equal(keyGenerator.BlowfishKey, initPayload[153..169]);

		var clientEngine = CreatePrimedClientEngine(keyGenerator.BlowfishKey);
		await client.GetStream().WriteAsync(CreateEncryptedAuthGameGuardFrame(clientEngine, sessionId));
		var authGameGuardPayload = await ReadEncryptedLoginPayloadAsync(client.GetStream(), clientEngine);
		Assert.Equal(0x0B, authGameGuardPayload[0]);
		Assert.Equal(sessionId, BinaryPrimitives.ReadInt32LittleEndian(authGameGuardPayload.AsSpan(1, 4)));

		await client.GetStream().WriteAsync(CreateEncryptedLoginFrame(clientEngine, keyGenerator.PublicParameters, sessionId, username, password));
		var loginOkPayload = await ReadEncryptedLoginPayloadAsync(client.GetStream(), clientEngine);
		Assert.Equal(0x03, loginOkPayload[0]);
		var accountId = BinaryPrimitives.ReadInt32LittleEndian(loginOkPayload.AsSpan(1, 4));
		var loginOk = BinaryPrimitives.ReadInt32LittleEndian(loginOkPayload.AsSpan(5, 4));
		Assert.Equal(expectedAccountId, accountId);
		return (clientEngine, accountId, loginOk);
	}

	private static async Task<byte[]> ReadEncryptedLoginPayloadAsync(NetworkStream stream, LoginCryptEngine cryptEngine)
	{
		var frame = await ReadFrameAsync(stream);
		var payload = frame[2..];
		Assert.True(cryptEngine.Decrypt(payload, 0, payload.Length));
		return payload;
	}

	private static byte[] DecryptFirstServerPayload(byte[] encryptedPayload)
	{
		var payload = encryptedPayload.ToArray();
		var cipher = new BlowfishCipher(InitialLoginKey);
		cipher.Decipher(payload, 0, payload.Length);
		UndoFirstServerXorPass(payload);
		return payload;
	}

	private static void UndoFirstServerXorPass(byte[] payload)
	{
		unchecked
		{
			var stop = payload.Length - 8;
			var ecx = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(stop, 4));
			for (var position = stop - 4; position >= 4; position -= 4)
			{
				var encoded = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(position, 4));
				var plain = encoded ^ ecx;
				BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(position, 4), plain);
				ecx -= plain;
			}
		}
	}

	internal static int GetFreeLoopbackPort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}

	internal static async Task<TcpClient> ConnectWithRetryAsync(int port)
	{
		for (var attempt = 0; attempt < 20; attempt++)
		{
			var client = new TcpClient(AddressFamily.InterNetwork);
			try
			{
				await client.ConnectAsync(IPAddress.Loopback, port);
				return client;
			}
			catch (SocketException) when (attempt < 19)
			{
				client.Dispose();
				await Task.Delay(50);
			}
		}

		throw new InvalidOperationException($"Could not connect to loopback port {port}.");
	}

	private static async Task<byte[]> ReadFrameAsync(NetworkStream stream)
	{
		var header = await ReadExactAsync(stream, 2);
		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		var frame = new byte[frameLength];
		header.CopyTo(frame, 0);
		var payload = await ReadExactAsync(stream, frameLength - 2);
		payload.CopyTo(frame.AsSpan(2));
		return frame;
	}

	private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
	{
		var buffer = new byte[length];
		var offset = 0;
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		while (offset < length)
		{
			var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), timeout.Token);
			if (read == 0)
				throw new EndOfStreamException("Socket closed before the expected frame was read.");
			offset += read;
		}

		return buffer;
	}

	internal static async Task AssertClientClosedAsync(NetworkStream stream)
	{
		var buffer = new byte[1];
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		var read = await stream.ReadAsync(buffer, timeout.Token);
		Assert.Equal(0, read);
	}

	internal static async Task AssertTaskCompletedAsync(Task task)
	{
		var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.Same(task, completed);
		await task;
	}

	private static async Task AssertActiveConnectionsAsync(Func<int> getActiveConnections, int expected)
	{
		var deadline = DateTime.UtcNow.AddSeconds(2);
		while (DateTime.UtcNow < deadline)
		{
			if (getActiveConnections() == expected)
				return;
			await Task.Delay(25);
		}

		Assert.Equal(expected, getActiveConnections());
	}

	internal sealed class FixedLoginKeyGenerator : ILoginKeyGenerator, IDisposable
	{
		private readonly LoginRsaKeyPair _keyPair = LoginRsaKeyPair.Generate();
		private readonly byte[] _blowfishKey = Enumerable.Range(0, 16).Select(i => (byte)(0x10 + i)).ToArray();

		public byte[] BlowfishKey => (byte[])_blowfishKey.Clone();

		public System.Security.Cryptography.RSAParameters PublicParameters => _keyPair.PublicParameters;

		public LoginRsaKeyPair GetEncryptedRsaKeyPair() => _keyPair;

		public byte[] GenerateBlowfishKey() => (byte[])_blowfishKey.Clone();

		public void Dispose() => _keyPair.Dispose();
	}

	private sealed class SuccessfulLoginAuthService : ILoginAuthService
	{
		public Account Account { get; } = new()
		{
			Id = 42,
			Name = "player",
			Activated = 1,
			AccountTime = new AccountTime(),
		};

		public int LoginAttempts { get; private set; }

		public int CompletedLogins { get; private set; }

		public int Logouts { get; private set; }

		public Task<LoginAuthResult> LoginAsync(string username, string password, string remoteIp, CancellationToken cancellationToken = default)
		{
			LoginAttempts++;
			Assert.Equal("player", username);
			Assert.Equal("secret", password);
			Assert.Equal("127.0.0.1", remoteIp);
			return Task.FromResult(LoginAuthResult.Success(Account));
		}

		public Task CompleteSuccessfulLoginAsync(Account account, string remoteIp, CancellationToken cancellationToken = default)
		{
			CompletedLogins++;
			Assert.Same(Account, account);
			Assert.Equal("127.0.0.1", remoteIp);
			return Task.CompletedTask;
		}

		public Task UpdateOnLogoutAsync(Account account, CancellationToken cancellationToken = default)
		{
			Logouts++;
			Assert.Same(Account, account);
			return Task.CompletedTask;
		}
	}

	private sealed class AccountBannedLoginAuthService : ILoginAuthService
	{
		public int LoginAttempts { get; private set; }

		public Task<LoginAuthResult> LoginAsync(string username, string password, string remoteIp, CancellationToken cancellationToken = default)
		{
			LoginAttempts++;
			Assert.Equal("player", username);
			Assert.Equal("secret", password);
			Assert.Equal("127.0.0.1", remoteIp);
			return Task.FromResult(LoginAuthResult.AccountBanned());
		}

		public Task CompleteSuccessfulLoginAsync(Account account, string remoteIp, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateOnLogoutAsync(Account account, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class ThrowingLoginAuthService : ILoginAuthService
	{
		public Task<LoginAuthResult> LoginAsync(string username, string password, string remoteIp, CancellationToken cancellationToken = default)
		{
			throw new InvalidOperationException("Auth service should not be reached by socket smoke tests.");
		}

		public Task CompleteSuccessfulLoginAsync(Account account, string remoteIp, CancellationToken cancellationToken = default)
		{
			throw new InvalidOperationException("Auth service should not be reached by socket smoke tests.");
		}

		public Task UpdateOnLogoutAsync(Account account, CancellationToken cancellationToken = default)
		{
			throw new InvalidOperationException("Auth service should not be reached by socket smoke tests.");
		}
	}

	private sealed class ThrowingAccountRepository : IAccountRepository
	{
		public Task<Account?> GetAccountByNameAsync(string name, bool useExternalAuth, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<Account?> GetAccountByIdAsync(int id, bool useExternalAuth, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> InsertAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateLastIpAsync(int accountId, string ip, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdateLastMacAsync(int accountId, string mac, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdateLastHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdateAllowedHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<string> GetLastIpAsync(int accountId, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdateAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateLastServerAsync(int accountId, sbyte lastServer, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateMembershipAsync(int accountId, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class TrackingAccountRepository : IAccountRepository
	{
		public (int AccountId, sbyte LastServer)? LastServerUpdate { get; private set; }

		public Task<Account?> GetAccountByNameAsync(string name, bool useExternalAuth, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<Account?> GetAccountByIdAsync(int id, bool useExternalAuth, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> InsertAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateLastIpAsync(int accountId, string ip, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdateLastMacAsync(int accountId, string mac, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdateLastHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdateAllowedHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<string> GetLastIpAsync(int accountId, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdateAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateLastServerAsync(int accountId, sbyte lastServer, CancellationToken cancellationToken = default)
		{
			LastServerUpdate = (accountId, lastServer);
			return Task.CompletedTask;
		}

		public Task UpdateMembershipAsync(int accountId, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class ThrowingAccountTimeRepository : IAccountTimeRepository
	{
		public Task<AccountTime?> GetAccountTimeAsync(int accountId, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateAccountTimeAsync(int accountId, AccountTime accountTime, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class EmptyBannedIpService : IBannedIpService
	{
		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public IReadOnlyCollection<BannedIp> GetEntries() => Array.Empty<BannedIp>();

		public bool IsBanned(string ip) => false;

		public Task<bool> BanAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UnbanAsync(string mask, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class ThrowingPremiumRepository : IPremiumRepository
	{
		public Task<long> GetPointsAsync(int accountId, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdatePointsAsync(int accountId, long points, long required, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class FixedPremiumRepository : IPremiumRepository
	{
		private readonly long _points;

		public FixedPremiumRepository(long points)
		{
			_points = points;
		}

		public Task<long> GetPointsAsync(int accountId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_points);
		}

		public Task<bool> UpdatePointsAsync(int accountId, long points, long required, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class ThrowingAccountsLogRepository : IAccountsLogRepository
	{
		public Task AddRecordAsync(int accountId, byte gameServerId, DateTime time, string ip, string mac, string hddSerial, CancellationToken cancellationToken = default)
		{
			throw NotUsed();
		}
	}

	private sealed class EmptyBannedMacService : IBannedMacService
	{
		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task<IReadOnlyCollection<BannedMacEntry>> GetEntriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<BannedMacEntry>>(Array.Empty<BannedMacEntry>());

		public Task BanAsync(string address, DateTime time, string details, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UnbanAsync(string address, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class EmptyBannedHddService : IBannedHddService
	{
		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task<IReadOnlyDictionary<string, DateTime>> GetEntriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, DateTime>>(new Dictionary<string, DateTime>());

		public Task BanAsync(string serial, DateTime time, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UnbanAsync(string serial, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class ThrowingPlayerTransferService : IPlayerTransferService
	{
		public Task VerifyNewTasksAsync(CancellationToken cancellationToken = default) => throw NotUsed();

		public Task RequestTransferAsync(int taskId, string name, byte[] db, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task OnErrorAsync(int taskId, string reason, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task OnOkAsync(int taskId, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task OnTaskStopAsync(int taskId, string reason, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private static InvalidOperationException NotUsed()
	{
		return new InvalidOperationException("Dependency should not be reached by socket smoke tests.");
	}

	private sealed class NoopGameServerSession : IGameServerSession
	{
		public Task SendPacketAsync(GsServerPacket packet)
		{
			return Task.CompletedTask;
		}
	}
}
