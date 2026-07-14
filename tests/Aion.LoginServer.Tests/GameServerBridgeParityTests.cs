using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ServerPackets;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.LoginServer.Tests;

public sealed class GameServerBridgeParityTests
{
	[Fact]
	public async Task GameServerBridge_AppliesConnectionInfoBeforeLoginServerControlResponse()
	{
		var account = TestAccount(99);
		var accountRepository = new FakeAccountRepository(account);
		var accountLogRepository = new FakeAccountsLogRepository();
		await using var context = await StartGameServerBridgeAsync(
			accountRepository: accountRepository,
			accountsLogRepository: accountLogRepository,
			logGameServerLogins: true);

		var time = 1_700_000_000_000L;
		await context.Stream.WriteAsync(CreateAccountConnectionInfoFrame(account.Id, time, "10.0.0.9", "aa-bb", "disk-a"));
		await context.Stream.WriteAsync(CreateLoginServerControlFrame(type: 1, param: 7, account.Id, adminId: 12345));

		var response = await ReadFrameAsync(context.Stream);

		Assert.Equal(PacketFrameCodec.CreateFrame(new SmLoginServerControlResponse(1, 7, account.Id, 12345, true).SerializePayload()), response);
		Assert.Equal(7, account.AccessLevel);
		Assert.Equal(1, accountRepository.UpdateAccountCalls);
		Assert.Equal((account.Id, "aa-bb"), accountRepository.LastMacUpdate);
		Assert.Equal((account.Id, "disk-a"), accountRepository.LastHddSerialUpdate);
		var record = Assert.Single(accountLogRepository.Records);
		Assert.Equal((account.Id, (byte)1, DateTimeOffset.FromUnixTimeMilliseconds(time).UtcDateTime, "10.0.0.9", "aa-bb", "disk-a"), record);
	}

	[Fact]
	public async Task GameServerBridge_FullBanUsesLastIpUpdatesAccountTimeAndReturnsJavaResponse()
	{
		var account = TestAccount(99);
		var accountRepository = new FakeAccountRepository(account)
		{
			LastIp = "10.0.0.5",
		};
		var accountTimeRepository = new FakeAccountTimeRepository((account.Id, account.AccountTime));
		var bannedIpService = new FakeBannedIpService("10.0.0.5");
		await using var context = await StartGameServerBridgeAsync(
			accountRepository: accountRepository,
			accountTimeRepository: accountTimeRepository,
			bannedIpService: bannedIpService);

		var before = DateTime.UtcNow;
		await context.Stream.WriteAsync(CreateBanFrame(type: 3, account.Id, "127.0.0.1", time: 15, adminObjectId: 12345));
		var response = await ReadFrameAsync(context.Stream);
		var after = DateTime.UtcNow;

		Assert.Equal(PacketFrameCodec.CreateFrame(new SmBanResponse(3, account.Id, "10.0.0.5", 15, 12345, true).SerializePayload()), response);
		Assert.Equal(1, accountTimeRepository.UpdateCalls);
		Assert.NotNull(account.AccountTime.PenaltyEnd);
		Assert.InRange(account.AccountTime.PenaltyEnd.Value, before.AddMinutes(14), after.AddMinutes(16));
		Assert.Equal(new[] { "10.0.0.5" }, bannedIpService.UnbannedMasks);
		Assert.Equal(("10.0.0.5", true), (bannedIpService.LastBannedMask, bannedIpService.LastBanExpireTime.HasValue));
	}

	[Fact]
	public async Task GameServerBridge_BanControlCoversAccountOnlyAndIpUnbanBranches()
	{
		var account = TestAccount(99);
		var accountTimeRepository = new FakeAccountTimeRepository((account.Id, account.AccountTime));
		var bannedIpService = new FakeBannedIpService("192.0.2.1");
		await using var context = await StartGameServerBridgeAsync(
			accountTimeRepository: accountTimeRepository,
			bannedIpService: bannedIpService);

		await context.Stream.WriteAsync(CreateBanFrame(type: 1, account.Id, "ignored-ip", time: 0, adminObjectId: 12345));
		var accountBanResponse = await ReadFrameAsync(context.Stream);

		await context.Stream.WriteAsync(CreateBanFrame(type: 2, accountId: 0, "192.0.2.1", time: -1, adminObjectId: 23456));
		var ipUnbanResponse = await ReadFrameAsync(context.Stream);

		Assert.Equal(PacketFrameCodec.CreateFrame(new SmBanResponse(1, account.Id, "ignored-ip", 0, 12345, true).SerializePayload()), accountBanResponse);
		Assert.Equal(1, accountTimeRepository.UpdateCalls);
		Assert.Equal(DateTime.UnixEpoch.AddMilliseconds(1000), account.AccountTime.PenaltyEnd);
		Assert.Null(bannedIpService.LastBannedMask);
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmBanResponse(2, 0, "192.0.2.1", -1, 23456, true).SerializePayload()), ipUnbanResponse);
		Assert.Equal(new[] { "192.0.2.1" }, bannedIpService.UnbannedMasks);
	}

	[Fact]
	public async Task GameServerBridge_AccountListLoadsNewAccountsAndRequestsDuplicateKick()
	{
		var localAccount = TestAccount(99);
		var duplicateAccount = TestAccount(100);
		var accountRepository = new FakeAccountRepository(localAccount, duplicateAccount);
		await using var context = await StartGameServerBridgeAsync(accountRepository: accountRepository);
		var otherGameServer = new GameServerInfo(2, "127.0.0.1", "other-secret");
		context.Registry.RegisterKnownServer(otherGameServer);
		context.Registry.RegisterGameServer(
			new GameServerAuthRequest(2, "other-secret", new byte[] { 127, 0, 0, 1 }, 7778, 0, 100),
			"127.0.0.1:7778",
			new CapturingGameServerSession());
		otherGameServer.AddAccount(duplicateAccount);

		await context.Stream.WriteAsync(CreateAccountListFrame(localAccount.Id, duplicateAccount.Id));

		var kickResponse = await ReadFrameAsync(context.Stream);
		var macBanList = await ReadFrameAsync(context.Stream);
		var hddBanList = await ReadFrameAsync(context.Stream);

		Assert.True(context.GameServer.IsAccountOnGameServer(localAccount.Id));
		Assert.False(context.GameServer.IsAccountOnGameServer(duplicateAccount.Id));
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmRequestKickAccount(duplicateAccount.Id, notifyDoubleLogin: false).SerializePayload()), kickResponse);
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmMacBanList(Array.Empty<BannedMacEntry>()).SerializePayload()), macBanList);
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmHddBanList(new Dictionary<string, DateTime>()).SerializePayload()), hddBanList);
	}

	[Fact]
	public async Task GameServerBridge_ReconnectKeyRemovesAccountAndRegistersReconnectState()
	{
		var account = TestAccount(99);
		await using var context = await StartGameServerBridgeAsync();
		context.GameServer.AddAccount(account);

		await context.Stream.WriteAsync(CreateAccountReconnectKeyFrame(account.Id));
		var response = await ReadFrameAsync(context.Stream);
		using var payload = new PacketBuffer(response[2..]);

		Assert.Equal(3, payload.ReadC());
		Assert.Equal(account.Id, payload.ReadD());
		var reconnectKey = payload.ReadD();
		Assert.False(context.GameServer.IsAccountOnGameServer(account.Id));
		Assert.True(context.SessionRegistry.TryConsumeReconnectingAccount(account.Id, reconnectKey, out var reconnectingAccount));
		Assert.NotNull(reconnectingAccount);
		Assert.Same(account, reconnectingAccount.Account);
	}

	[Fact]
	public async Task GameServerBridge_DisconnectRemovesAccountAndUpdatesLogoutBeforeNextResponse()
	{
		var account = TestAccount(99);
		var accountRepository = new FakeAccountRepository(account);
		var authService = new TrackingLoginAuthService();
		await using var context = await StartGameServerBridgeAsync(accountRepository: accountRepository, authService: authService);
		context.GameServer.AddAccount(account);

		await context.Stream.WriteAsync(CreateAccountDisconnectedFrame(account.Id));
		await context.Stream.WriteAsync(CreateLoginServerControlFrame(type: 2, param: 3, account.Id, adminId: 12345));
		var response = await ReadFrameAsync(context.Stream);

		Assert.False(context.GameServer.IsAccountOnGameServer(account.Id));
		Assert.Equal(new[] { account }, authService.LogoutUpdates);
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmLoginServerControlResponse(2, 3, account.Id, 12345, true).SerializePayload()), response);
	}

	[Fact]
	public async Task GameServerBridge_AllowedHddUpdateMatchesJavaDaoSideEffect()
	{
		var account = TestAccount(99);
		var accountRepository = new FakeAccountRepository(account);
		await using var context = await StartGameServerBridgeAsync(accountRepository: accountRepository);

		await context.Stream.WriteAsync(CreateChangeAllowedHddSerialFrame(account.Id, "disk-allowed"));
		await context.Stream.WriteAsync(CreateLoginServerControlFrame(type: 1, param: 4, account.Id, adminId: 12345));
		var response = await ReadFrameAsync(context.Stream);

		Assert.Equal((account.Id, "disk-allowed"), accountRepository.LastAllowedHddSerialUpdate);
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmLoginServerControlResponse(1, 4, account.Id, 12345, true).SerializePayload()), response);
	}

	[Fact]
	public async Task GameServerBridge_MacAndHddBanControlsMatchJavaManagerSideEffects()
	{
		var account = TestAccount(99);
		var macService = new TrackingBannedMacService();
		var hddService = new TrackingBannedHddService();
		await using var context = await StartGameServerBridgeAsync(
			accountRepository: new FakeAccountRepository(account),
			bannedMacService: macService,
			bannedHddService: hddService);

		var macTime = 1_700_000_010_000L;
		var hddTime = 1_700_000_020_000L;
		await context.Stream.WriteAsync(CreateMacBanControlFrame(type: 1, "aa-bb-cc-dd-ee-ff", "manual ban", macTime));
		await context.Stream.WriteAsync(CreateMacBanControlFrame(type: 0, "aa-bb-cc-dd-ee-ff", "manual unban", time: 0));
		await context.Stream.WriteAsync(CreateHddBanControlFrame(type: 1, "disk-ban", hddTime));
		await context.Stream.WriteAsync(CreateHddBanControlFrame(type: 0, "disk-ban", time: 0));
		await context.Stream.WriteAsync(CreateLoginServerControlFrame(type: 1, param: 4, account.Id, adminId: 12345));
		var response = await ReadFrameAsync(context.Stream);

		Assert.Equal(
			new[] { ("aa-bb-cc-dd-ee-ff", DateTimeOffset.FromUnixTimeMilliseconds(macTime).UtcDateTime, "manual ban") },
			macService.BanCalls);
		Assert.Equal(new[] { "aa-bb-cc-dd-ee-ff" }, macService.UnbanCalls);
		Assert.Equal(
			new[] { ("disk-ban", DateTimeOffset.FromUnixTimeMilliseconds(hddTime).UtcDateTime) },
			hddService.BanCalls);
		Assert.Equal(new[] { "disk-ban" }, hddService.UnbanCalls);
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmLoginServerControlResponse(1, 4, account.Id, 12345, true).SerializePayload()), response);
	}

	[Fact]
	public async Task GameServerBridge_PlayerTransferControlDispatchesJavaReadSideEffects()
	{
		var account = TestAccount(99);
		var playerTransferService = new TrackingPlayerTransferService();
		await using var context = await StartGameServerBridgeAsync(
			accountRepository: new FakeAccountRepository(account),
			playerTransferService: playerTransferService);

		await context.Stream.WriteAsync(CreatePlayerTransferRequestFrame(10, "source-player", new byte[] { 1, 2, 3, 4 }));
		await context.Stream.WriteAsync(CreatePlayerTransferErrorFrame(11, "bad transfer"));
		await context.Stream.WriteAsync(CreatePlayerTransferOkFrame(12));
		await context.Stream.WriteAsync(CreatePlayerTransferStopFrame(13, "stopped"));
		await context.Stream.WriteAsync(CreateLoginServerControlFrame(type: 1, param: 4, account.Id, adminId: 12345));
		var response = await ReadFrameAsync(context.Stream);

		var request = Assert.Single(playerTransferService.Requests);
		Assert.Equal((10, "source-player"), (request.TaskId, request.Name));
		Assert.Equal(new byte[] { 1, 2, 3, 4 }, request.Db);
		Assert.Equal(new[] { (11, "bad transfer") }, playerTransferService.Errors);
		Assert.Equal(new[] { 12 }, playerTransferService.Oks);
		Assert.Equal(new[] { (13, "stopped") }, playerTransferService.Stops);
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmLoginServerControlResponse(1, 4, account.Id, 12345, true).SerializePayload()), response);
	}

	[Fact]
	public async Task GameServerBridge_HandlerFailureDoesNotCloseSocket()
	{
		var account = TestAccount(99);
		await using var context = await StartGameServerBridgeAsync(accountRepository: new FakeAccountRepository(account));

		// The default transfer service deliberately throws. Java GsClientPacket.run() logs this
		// handler failure and leaves the dispatcher/socket available for the next packet.
		await context.Stream.WriteAsync(CreatePlayerTransferOkFrame(12));
		await context.Stream.WriteAsync(CreateLoginServerControlFrame(type: 1, param: 4, account.Id, adminId: 12345));

		var response = await ReadFrameAsync(context.Stream);
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmLoginServerControlResponse(1, 4, account.Id, 12345, true).SerializePayload()), response);
		Assert.True(context.GameServer.IsOnline);
	}

	[Fact]
	public async Task GameServerBridge_PingPongLoopMatchesJavaMissedPongLifecycle()
	{
		await using var context = await StartGameServerBridgeAsync(gameServerPingInterval: TimeSpan.FromMilliseconds(100));
		var pingFrame = PacketFrameCodec.CreateFrame(new SmPing().SerializePayload());

		Assert.Equal(pingFrame, await ReadFrameAsync(context.Stream));
		await context.Stream.WriteAsync(CreateGameServerPongFrame());
		Assert.Equal(pingFrame, await ReadFrameAsync(context.Stream));
		Assert.Equal(pingFrame, await ReadFrameAsync(context.Stream));
		Assert.Equal(pingFrame, await ReadFrameAsync(context.Stream));
		await Assert.ThrowsAsync<EndOfStreamException>(() => ReadFrameAsync(context.Stream));
		Assert.False(context.GameServer.IsOnline);
	}

	private static async Task<BridgeContext> StartGameServerBridgeAsync(
		bool logGameServerLogins = false,
		FakeAccountRepository? accountRepository = null,
		FakeAccountTimeRepository? accountTimeRepository = null,
		FakeBannedIpService? bannedIpService = null,
		FakeAccountsLogRepository? accountsLogRepository = null,
		ILoginAuthService? authService = null,
		IBannedMacService? bannedMacService = null,
		IBannedHddService? bannedHddService = null,
		IPlayerTransferService? playerTransferService = null,
		TimeSpan? gameServerPingInterval = null)
	{
		var port = SocketServerSmokeTests.GetFreeLoopbackPort();
		var gameServer = new GameServerInfo(1, "127.0.0.1", "secret");
		var registry = new GameServerRegistry();
		var sessionRegistry = new LoginSessionRegistry();
		registry.RegisterKnownServer(gameServer);
		var options = new LoginServerOptions
		{
			GameServerEndPoint = new IPEndPoint(IPAddress.Loopback, port),
			MaxGameServerConnections = 10,
			LogGameServerLogins = logGameServerLogins,
		};

		accountRepository ??= new FakeAccountRepository();
		accountTimeRepository ??= new FakeAccountTimeRepository();
		bannedIpService ??= new FakeBannedIpService();
		accountsLogRepository ??= new FakeAccountsLogRepository();

		var server = new GameServerSocketServer(
			NullLogger<GameServerSocketServer>.Instance,
			options,
			registry,
			sessionRegistry,
			accountRepository,
			accountTimeRepository,
			bannedIpService,
			accountsLogRepository,
			authService ?? new ThrowingLoginAuthService(),
			bannedMacService ?? new EmptyBannedMacService(),
			bannedHddService ?? new EmptyBannedHddService(),
			playerTransferService ?? new ThrowingPlayerTransferService(),
			gameServerPingInterval);
		var serverTask = server.StartAsync();
		var client = await SocketServerSmokeTests.ConnectWithRetryAsync(port);
		await client.GetStream().WriteAsync(CreateGameServerAuthFrame());
		Assert.Equal(PacketFrameCodec.CreateFrame(new SmGameServerAuthResponse(GsAuthResponse.AUTHED, 1).SerializePayload()), await ReadFrameAsync(client.GetStream()));
		Assert.True(gameServer.IsOnline);

		return new BridgeContext(server, serverTask, client, registry, sessionRegistry, gameServer);
	}

	private static Account TestAccount(int id)
	{
		return new Account
		{
			Id = id,
			Name = $"account-{id}",
			PasswordHash = "hash",
			Activated = 1,
			LastMac = "xx-xx-xx-xx-xx-xx",
			AccountTime = new AccountTime(),
		};
	}

	private static byte[] CreateGameServerAuthFrame()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0);
		payload.WriteC(1);
		payload.WriteS("secret");
		payload.WriteC(4);
		payload.WriteB(new byte[] { 127, 0, 0, 1 });
		payload.WriteH(7777);
		payload.WriteC(0);
		payload.WriteD(100);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateGameServerPongFrame()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(12);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateAccountConnectionInfoFrame(int accountId, long time, string ip, string mac, string hddSerial)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(7);
		payload.WriteD(accountId);
		payload.WriteQ(time);
		payload.WriteS(ip);
		payload.WriteS(mac);
		payload.WriteS(hddSerial);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateLoginServerControlFrame(byte type, byte param, int accountId, int adminId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(5);
		payload.WriteC(type);
		payload.WriteC(param);
		payload.WriteD(accountId);
		payload.WriteD(adminId);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateAccountReconnectKeyFrame(int accountId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(2);
		payload.WriteD(accountId);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateAccountDisconnectedFrame(int accountId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(3);
		payload.WriteD(accountId);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateChangeAllowedHddSerialFrame(int accountId, string hddSerial)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(11);
		payload.WriteD(accountId);
		payload.WriteS(hddSerial);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateMacBanControlFrame(byte type, string address, string details, long time)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(9);
		payload.WriteC(type);
		payload.WriteS(address);
		payload.WriteS(details);
		payload.WriteQ(time);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateHddBanControlFrame(byte type, string serial, long time)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(10);
		payload.WriteC(type);
		payload.WriteS(serial);
		payload.WriteQ(time);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreatePlayerTransferRequestFrame(int taskId, string name, byte[] db)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(13);
		payload.WriteC(1);
		payload.WriteD(taskId);
		payload.WriteS(name);
		payload.WriteB(db);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreatePlayerTransferErrorFrame(int taskId, string reason)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(13);
		payload.WriteC(2);
		payload.WriteD(taskId);
		payload.WriteS(reason);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreatePlayerTransferOkFrame(int taskId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(13);
		payload.WriteC(3);
		payload.WriteD(taskId);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreatePlayerTransferStopFrame(int taskId, string reason)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(13);
		payload.WriteC(4);
		payload.WriteD(taskId);
		payload.WriteS(reason);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateBanFrame(byte type, int accountId, string ip, int time, int adminObjectId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(6);
		payload.WriteC(type);
		payload.WriteD(accountId);
		payload.WriteS(ip);
		payload.WriteD(time);
		payload.WriteD(adminObjectId);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateAccountListFrame(params int[] accountIds)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(4);
		payload.WriteD(accountIds.Length);
		foreach (var accountId in accountIds)
			payload.WriteD(accountId);
		return PacketFrameCodec.CreateFrame(payload.ToArray());
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

	private sealed class BridgeContext : IAsyncDisposable
	{
		private readonly GameServerSocketServer _server;
		private readonly Task _serverTask;
		private readonly TcpClient _client;

		public BridgeContext(GameServerSocketServer server, Task serverTask, TcpClient client, GameServerRegistry registry, LoginSessionRegistry sessionRegistry, GameServerInfo gameServer)
		{
			_server = server;
			_serverTask = serverTask;
			_client = client;
			Registry = registry;
			SessionRegistry = sessionRegistry;
			GameServer = gameServer;
		}

		public NetworkStream Stream => _client.GetStream();

		public GameServerRegistry Registry { get; }

		public LoginSessionRegistry SessionRegistry { get; }

		public GameServerInfo GameServer { get; }

		public async ValueTask DisposeAsync()
		{
			await _server.StopAsync(TimeSpan.FromSeconds(1));
			_client.Dispose();
			await _serverTask;
		}
	}

	private sealed class FakeAccountRepository : IAccountRepository
	{
		private readonly Dictionary<int, Account> _accounts;

		public FakeAccountRepository(params Account[] accounts)
		{
			_accounts = accounts.ToDictionary(account => account.Id);
		}

		public string LastIp { get; init; } = string.Empty;

		public int UpdateAccountCalls { get; private set; }

		public (int AccountId, string Mac) LastMacUpdate { get; private set; }

		public (int AccountId, string HddSerial) LastHddSerialUpdate { get; private set; }

		public (int AccountId, string HddSerial) LastAllowedHddSerialUpdate { get; private set; }

		public Task<Account?> GetAccountByNameAsync(string name, bool useExternalAuth, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_accounts.Values.FirstOrDefault(account => account.Name == name));
		}

		public Task<Account?> GetAccountByIdAsync(int id, bool useExternalAuth, CancellationToken cancellationToken = default)
		{
			_accounts.TryGetValue(id, out var account);
			return Task.FromResult(account);
		}

		public Task<bool> InsertAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default)
		{
			_accounts[account.Id] = account;
			return Task.FromResult(true);
		}

		public Task UpdateLastIpAsync(int accountId, string ip, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task<bool> UpdateLastMacAsync(int accountId, string mac, CancellationToken cancellationToken = default)
		{
			LastMacUpdate = (accountId, mac);
			return Task.FromResult(true);
		}

		public Task<bool> UpdateLastHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default)
		{
			LastHddSerialUpdate = (accountId, hddSerial);
			return Task.FromResult(true);
		}

		public Task<bool> UpdateAllowedHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default)
		{
			LastAllowedHddSerialUpdate = (accountId, hddSerial);
			return Task.FromResult(true);
		}

		public Task<string> GetLastIpAsync(int accountId, CancellationToken cancellationToken = default) => Task.FromResult(LastIp);

		public Task<bool> UpdateAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default)
		{
			UpdateAccountCalls++;
			_accounts[account.Id] = account;
			return Task.FromResult(true);
		}

		public Task UpdateLastServerAsync(int accountId, sbyte lastServer, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task UpdateMembershipAsync(int accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class FakeAccountTimeRepository : IAccountTimeRepository
	{
		private readonly Dictionary<int, AccountTime> _times = new();

		public FakeAccountTimeRepository(params (int AccountId, AccountTime AccountTime)[] entries)
		{
			foreach (var (accountId, accountTime) in entries)
				_times[accountId] = accountTime;
		}

		public int UpdateCalls { get; private set; }

		public Task<AccountTime?> GetAccountTimeAsync(int accountId, CancellationToken cancellationToken = default)
		{
			_times.TryGetValue(accountId, out var accountTime);
			return Task.FromResult<AccountTime?>(accountTime ?? new AccountTime());
		}

		public Task UpdateAccountTimeAsync(int accountId, AccountTime accountTime, CancellationToken cancellationToken = default)
		{
			UpdateCalls++;
			_times[accountId] = accountTime;
			return Task.CompletedTask;
		}
	}

	private sealed class FakeBannedIpService : IBannedIpService
	{
		private readonly HashSet<string> _bannedMasks;
		private readonly List<string> _unbannedMasks = new();

		public FakeBannedIpService(params string[] bannedMasks)
		{
			_bannedMasks = bannedMasks.ToHashSet(StringComparer.OrdinalIgnoreCase);
		}

		public IReadOnlyList<string> UnbannedMasks => _unbannedMasks;

		public string? LastBannedMask { get; private set; }

		public DateTime? LastBanExpireTime { get; private set; }

		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public IReadOnlyCollection<BannedIp> GetEntries() => _bannedMasks.Select(mask => new BannedIp { Mask = mask }).ToArray();

		public bool IsBanned(string ip) => _bannedMasks.Contains(ip);

		public Task<bool> BanAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default)
		{
			LastBannedMask = mask;
			LastBanExpireTime = expireTime;
			_bannedMasks.Add(mask);
			return Task.FromResult(true);
		}

		public Task<bool> UnbanAsync(string mask, CancellationToken cancellationToken = default)
		{
			_unbannedMasks.Add(mask);
			return Task.FromResult(_bannedMasks.Remove(mask));
		}
	}

	private sealed class FakeAccountsLogRepository : IAccountsLogRepository
	{
		private readonly List<(int AccountId, byte GameServerId, DateTime Time, string Ip, string Mac, string HddSerial)> _records = new();

		public IReadOnlyList<(int AccountId, byte GameServerId, DateTime Time, string Ip, string Mac, string HddSerial)> Records => _records;

		public Task AddRecordAsync(int accountId, byte gameServerId, DateTime time, string ip, string mac, string hddSerial, CancellationToken cancellationToken = default)
		{
			_records.Add((accountId, gameServerId, time, ip, mac, hddSerial));
			return Task.CompletedTask;
		}
	}

	private sealed class EmptyBannedMacService : IBannedMacService
	{
		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task<IReadOnlyCollection<BannedMacEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyCollection<BannedMacEntry>>(Array.Empty<BannedMacEntry>());
		}

		public Task BanAsync(string address, DateTime time, string details, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task UnbanAsync(string address, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class TrackingBannedMacService : IBannedMacService
	{
		private readonly List<(string Address, DateTime Time, string Details)> _banCalls = new();
		private readonly List<string> _unbanCalls = new();

		public IReadOnlyList<(string Address, DateTime Time, string Details)> BanCalls => _banCalls;

		public IReadOnlyList<string> UnbanCalls => _unbanCalls;

		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task<IReadOnlyCollection<BannedMacEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyCollection<BannedMacEntry>>(Array.Empty<BannedMacEntry>());
		}

		public Task BanAsync(string address, DateTime time, string details, CancellationToken cancellationToken = default)
		{
			_banCalls.Add((address, time, details));
			return Task.CompletedTask;
		}

		public Task UnbanAsync(string address, CancellationToken cancellationToken = default)
		{
			_unbanCalls.Add(address);
			return Task.CompletedTask;
		}
	}

	private sealed class EmptyBannedHddService : IBannedHddService
	{
		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task<IReadOnlyDictionary<string, DateTime>> GetEntriesAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyDictionary<string, DateTime>>(new Dictionary<string, DateTime>());
		}

		public Task BanAsync(string serial, DateTime time, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task UnbanAsync(string serial, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class TrackingBannedHddService : IBannedHddService
	{
		private readonly List<(string Serial, DateTime Time)> _banCalls = new();
		private readonly List<string> _unbanCalls = new();

		public IReadOnlyList<(string Serial, DateTime Time)> BanCalls => _banCalls;

		public IReadOnlyList<string> UnbanCalls => _unbanCalls;

		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task<IReadOnlyDictionary<string, DateTime>> GetEntriesAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyDictionary<string, DateTime>>(new Dictionary<string, DateTime>());
		}

		public Task BanAsync(string serial, DateTime time, CancellationToken cancellationToken = default)
		{
			_banCalls.Add((serial, time));
			return Task.CompletedTask;
		}

		public Task UnbanAsync(string serial, CancellationToken cancellationToken = default)
		{
			_unbanCalls.Add(serial);
			return Task.CompletedTask;
		}
	}

	private sealed class TrackingPlayerTransferService : IPlayerTransferService
	{
		private readonly List<(int TaskId, string Name, byte[] Db)> _requests = new();
		private readonly List<(int TaskId, string Reason)> _errors = new();
		private readonly List<int> _oks = new();
		private readonly List<(int TaskId, string Reason)> _stops = new();

		public IReadOnlyList<(int TaskId, string Name, byte[] Db)> Requests => _requests;

		public IReadOnlyList<(int TaskId, string Reason)> Errors => _errors;

		public IReadOnlyList<int> Oks => _oks;

		public IReadOnlyList<(int TaskId, string Reason)> Stops => _stops;

		public Task VerifyNewTasksAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task RequestTransferAsync(int taskId, string name, byte[] db, CancellationToken cancellationToken = default)
		{
			_requests.Add((taskId, name, db));
			return Task.CompletedTask;
		}

		public Task OnErrorAsync(int taskId, string reason, CancellationToken cancellationToken = default)
		{
			_errors.Add((taskId, reason));
			return Task.CompletedTask;
		}

		public Task OnOkAsync(int taskId, CancellationToken cancellationToken = default)
		{
			_oks.Add(taskId);
			return Task.CompletedTask;
		}

		public Task OnTaskStopAsync(int taskId, string reason, CancellationToken cancellationToken = default)
		{
			_stops.Add((taskId, reason));
			return Task.CompletedTask;
		}
	}

	private sealed class CapturingGameServerSession : IGameServerSession
	{
		public List<GsServerPacket> Packets { get; } = new();

		public Task SendPacketAsync(GsServerPacket packet)
		{
			Packets.Add(packet);
			return Task.CompletedTask;
		}
	}

	private sealed class ThrowingLoginAuthService : ILoginAuthService
	{
		public Task<LoginAuthResult> LoginAsync(string username, string password, string remoteIp, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task CompleteSuccessfulLoginAsync(Account account, string remoteIp, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateOnLogoutAsync(Account account, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class TrackingLoginAuthService : ILoginAuthService
	{
		private readonly List<Account> _logoutUpdates = new();

		public IReadOnlyList<Account> LogoutUpdates => _logoutUpdates;

		public Task<LoginAuthResult> LoginAsync(string username, string password, string remoteIp, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task CompleteSuccessfulLoginAsync(Account account, string remoteIp, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateOnLogoutAsync(Account account, CancellationToken cancellationToken = default)
		{
			_logoutUpdates.Add(account);
			return Task.CompletedTask;
		}
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
		return new InvalidOperationException("Dependency should not be reached by game-server bridge parity tests.");
	}
}
