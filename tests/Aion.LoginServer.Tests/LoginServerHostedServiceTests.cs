using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.LoginServer.Tests;

public sealed class LoginServerHostedServiceTests
{
	[Fact]
	public async Task StartAsync_LoadsRegistryAndBanListsBeforeOpeningSocketListeners()
	{
		var loginPort = SocketServerSmokeTests.GetFreeLoopbackPort();
		var gameServerPort = SocketServerSmokeTests.GetFreeLoopbackPort();
		var order = new ConcurrentQueue<string>();
		var gameServersRepository = new BlockingGameServersRepository(order);
		var bannedIpService = new TrackingBannedIpService(order);
		var bannedMacService = new TrackingBannedMacService(order);
		var bannedHddService = new TrackingBannedHddService(order);
		var playerTransferScheduler = new TrackingPlayerTransferScheduler(order);
		var registry = new GameServerRegistry();
		var sessions = new LoginSessionRegistry();
		using var keyGenerator = new SocketServerSmokeTests.FixedLoginKeyGenerator();
		var dependencies = new ThrowingGameServerDependencies();

		var loginServer = new LoginClientSocketServer(
			NullLogger<LoginClientSocketServer>.Instance,
			new LoginServerOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, loginPort),
				MaxClientConnections = 10,
			},
			keyGenerator,
			dependencies,
			sessions,
			registry);
		var gameServerSocketServer = new GameServerSocketServer(
			NullLogger<GameServerSocketServer>.Instance,
			new LoginServerOptions
			{
				GameServerEndPoint = new IPEndPoint(IPAddress.Loopback, gameServerPort),
				MaxGameServerConnections = 10,
			},
			registry,
			sessions,
			dependencies,
			dependencies,
			bannedIpService,
			dependencies,
			dependencies,
			dependencies,
			bannedMacService,
			bannedHddService,
			dependencies);
		var hosted = new LoginServerHostedService(
			loginServer,
			gameServerSocketServer,
			gameServersRepository,
			registry,
			bannedIpService,
			bannedMacService,
			bannedHddService,
			playerTransferScheduler,
			NullLogger<LoginServerHostedService>.Instance);

		using var startupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var startTask = hosted.StartAsync(startupTimeout.Token);
		await gameServersRepository.WaitUntilCalledAsync();
		Assert.False(startTask.IsCompleted);
		Assert.False(await CanConnectAsync(loginPort));
		Assert.False(await CanConnectAsync(gameServerPort));

		var configuredGameServer = new GameServerInfo(1, "127.0.0.1", "secret");
		gameServersRepository.Release(new Dictionary<byte, GameServerInfo>
		{
			[configuredGameServer.Id] = configuredGameServer,
		});
		await SocketServerSmokeTests.AssertTaskCompletedAsync(startTask);

		Assert.Equal(new[] { "gameservers", "ip", "mac-clean", "hdd-clean", "ptransfer" }, order.ToArray());
		Assert.Same(configuredGameServer, registry.GetGameServer(configuredGameServer.Id));

		using var loginClient = await SocketServerSmokeTests.ConnectWithRetryAsync(loginPort);
		using var gameServerClient = await SocketServerSmokeTests.ConnectWithRetryAsync(gameServerPort);
		await AssertActiveConnectionsAsync(loginServer.GetActiveConnections, 1);
		await AssertActiveConnectionsAsync(gameServerSocketServer.GetActiveConnections, 1);

		playerTransferScheduler.BlockStop();
		var stopTask = hosted.StopAsync(CancellationToken.None);
		await playerTransferScheduler.WaitUntilStopCalledAsync();
		Assert.False(stopTask.IsCompleted);
		Assert.Equal(1, loginServer.GetActiveConnections());
		Assert.Equal(1, gameServerSocketServer.GetActiveConnections());

		playerTransferScheduler.ReleaseStop();
		await SocketServerSmokeTests.AssertTaskCompletedAsync(stopTask);
		await AssertEventuallyClosedAsync(loginClient.GetStream());
		await AssertEventuallyClosedAsync(gameServerClient.GetStream());
		Assert.Equal(0, loginServer.GetActiveConnections());
		Assert.Equal(0, gameServerSocketServer.GetActiveConnections());
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

	private static async Task AssertEventuallyClosedAsync(NetworkStream stream)
	{
		var buffer = new byte[256];
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		while (true)
		{
			try
			{
				var read = await stream.ReadAsync(buffer, timeout.Token);
				if (read == 0)
					return;
			}
			catch (IOException)
			{
				return;
			}
		}
	}

	private static async Task<bool> CanConnectAsync(int port)
	{
		using var client = new TcpClient(AddressFamily.InterNetwork);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
		try
		{
			await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
			return true;
		}
		catch (Exception ex) when (ex is SocketException or OperationCanceledException)
		{
			return false;
		}
	}

	private sealed class BlockingGameServersRepository : IGameServersRepository
	{
		private readonly ConcurrentQueue<string> _order;
		private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource<IReadOnlyDictionary<byte, GameServerInfo>> _release =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		public BlockingGameServersRepository(ConcurrentQueue<string> order)
		{
			_order = order;
		}

		public Task WaitUntilCalledAsync() => _entered.Task;

		public void Release(IReadOnlyDictionary<byte, GameServerInfo> gameServers)
		{
			_release.SetResult(gameServers);
		}

		public async Task<IReadOnlyDictionary<byte, GameServerInfo>> GetAllGameServersAsync(CancellationToken cancellationToken = default)
		{
			_order.Enqueue("gameservers");
			_entered.SetResult();
			return await _release.Task.WaitAsync(cancellationToken);
		}
	}

	private sealed class TrackingBannedMacService : IBannedMacService
	{
		private readonly ConcurrentQueue<string> _order;

		public TrackingBannedMacService(ConcurrentQueue<string> order)
		{
			_order = order;
		}

		public Task LoadAsync(CancellationToken cancellationToken = default) => throw NotUsed();

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default)
		{
			_order.Enqueue("mac-clean");
			return Task.CompletedTask;
		}

		public Task<IReadOnlyCollection<BannedMacEntry>> GetEntriesAsync(CancellationToken cancellationToken = default) => throw NotUsed();

		public Task BanAsync(string address, DateTime time, string details, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UnbanAsync(string address, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class TrackingBannedIpService : IBannedIpService
	{
		private readonly ConcurrentQueue<string> _order;

		public TrackingBannedIpService(ConcurrentQueue<string> order)
		{
			_order = order;
		}

		public Task LoadAsync(CancellationToken cancellationToken = default)
		{
			_order.Enqueue("ip");
			return Task.CompletedTask;
		}

		public IReadOnlyCollection<BannedIp> GetEntries() => Array.Empty<BannedIp>();

		public bool IsBanned(string ip) => false;

		public Task<bool> BanAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UnbanAsync(string mask, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class TrackingBannedHddService : IBannedHddService
	{
		private readonly ConcurrentQueue<string> _order;

		public TrackingBannedHddService(ConcurrentQueue<string> order)
		{
			_order = order;
		}

		public Task LoadAsync(CancellationToken cancellationToken = default) => throw NotUsed();

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default)
		{
			_order.Enqueue("hdd-clean");
			return Task.CompletedTask;
		}

		public Task<IReadOnlyDictionary<string, DateTime>> GetEntriesAsync(CancellationToken cancellationToken = default) => throw NotUsed();

		public Task BanAsync(string serial, DateTime time, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UnbanAsync(string serial, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private sealed class TrackingPlayerTransferScheduler : IPlayerTransferScheduler
	{
		private readonly ConcurrentQueue<string> _order;
		private readonly TaskCompletionSource _stopEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _releaseStop = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private bool _blockStop;

		public TrackingPlayerTransferScheduler(ConcurrentQueue<string> order)
		{
			_order = order;
		}

		public void BlockStop()
		{
			_blockStop = true;
		}

		public Task WaitUntilStopCalledAsync() => _stopEntered.Task;

		public void ReleaseStop()
		{
			_releaseStop.SetResult();
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_order.Enqueue("ptransfer");
			return Task.CompletedTask;
		}

		public async Task StopAsync(CancellationToken cancellationToken)
		{
			_order.Enqueue("ptransfer-stop");
			_stopEntered.SetResult();
			if (_blockStop)
				await _releaseStop.Task.WaitAsync(cancellationToken);
		}
	}

	private sealed class ThrowingGameServerDependencies :
		IAccountRepository,
		IAccountTimeRepository,
		IPremiumRepository,
		IAccountsLogRepository,
		ILoginAuthService,
		IPlayerTransferService
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

		public Task<AccountTime?> GetAccountTimeAsync(int accountId, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateAccountTimeAsync(int accountId, AccountTime accountTime, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task CleanExpiredBansAsync(CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<IReadOnlyCollection<BannedIp>> GetAllBansAsync(CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> InsertAsync(string mask, DateTime? expireTime, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> RemoveAsync(string mask, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<long> GetPointsAsync(int accountId, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task<bool> UpdatePointsAsync(int accountId, long points, long required, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task AddRecordAsync(int accountId, byte gameServerId, DateTime time, string ip, string mac, string hddSerial, CancellationToken cancellationToken = default)
		{
			throw NotUsed();
		}

		public Task<LoginAuthResult> LoginAsync(string username, string password, string remoteIp, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task CompleteSuccessfulLoginAsync(Account account, string remoteIp, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task UpdateOnLogoutAsync(Account account, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task VerifyNewTasksAsync(CancellationToken cancellationToken = default) => throw NotUsed();

		public Task RequestTransferAsync(int taskId, string name, byte[] db, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task OnErrorAsync(int taskId, string reason, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task OnOkAsync(int taskId, CancellationToken cancellationToken = default) => throw NotUsed();

		public Task OnTaskStopAsync(int taskId, string reason, CancellationToken cancellationToken = default) => throw NotUsed();
	}

	private static InvalidOperationException NotUsed()
	{
		return new InvalidOperationException("Dependency should not be reached by hosted service startup tests.");
	}
}
