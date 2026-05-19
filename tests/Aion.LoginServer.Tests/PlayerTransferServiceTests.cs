using System.Buffers.Binary;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.LoginServer.Tests;

public class PlayerTransferServiceTests
{
	[Fact]
	public async Task VerifyRequestAndOk_FollowsJavaPlayerTransferFlow()
	{
		var task = TestTask();
		var repository = new FakePlayerTransferRepository(task);
		var registry = new FakeGameServerRegistry();
		registry.AddOnlineServer(1);
		registry.AddOnlineServer(2);
		var sourceAccount = new Account { Id = 100, Name = "source", Activated = 1 };
		var targetAccount = new Account { Id = 101, Name = "target", Activated = 1 };
		var accountRepository = new FakeAccountRepository(sourceAccount, targetAccount);
		var service = CreateService(repository, registry, accountRepository);

		await service.VerifyNewTasksAsync();

		Assert.Equal(PlayerTransferTask.StatusActive, task.Status);
		Assert.Equal(PlayerTransferResultStatus.PerformAction, PacketResult(registry.LastPacketTo(1)));

		await service.RequestTransferAsync(task.Id, "character", new byte[] { 1, 2, 3 });

		Assert.Equal(0, sourceAccount.Activated);
		Assert.Equal(0, targetAccount.Activated);
		Assert.Equal(PlayerTransferResultStatus.SendInfo, PacketResult(registry.LastPacketTo(2)));

		await service.OnOkAsync(task.Id);

		Assert.Equal(PlayerTransferTask.StatusDone, task.Status);
		Assert.Equal("task done", task.Comment);
		Assert.Equal(1, sourceAccount.Activated);
		Assert.Equal(1, targetAccount.Activated);
		Assert.Equal(PlayerTransferResultStatus.Ok, PacketResult(registry.LastPacketTo(1)));
		Assert.Contains(repository.UpdatedTasks, updated => updated.Status == PlayerTransferTask.StatusActive);
		Assert.Contains(repository.UpdatedTasks, updated => updated.Status == PlayerTransferTask.StatusDone);
	}

	[Fact]
	public async Task VerifyNewTasks_SkipsTaskWhenSourceAccountIsOnline()
	{
		var task = TestTask();
		var repository = new FakePlayerTransferRepository(task);
		var registry = new FakeGameServerRegistry();
		var sourceServer = registry.AddOnlineServer(1);
		registry.AddOnlineServer(2);
		sourceServer.AddAccount(new Account { Id = task.SourceAccountId });
		var service = CreateService(repository, registry, new FakeAccountRepository());

		await service.VerifyNewTasksAsync();

		Assert.Equal(PlayerTransferTask.StatusWait, task.Status);
		Assert.Empty(repository.UpdatedTasks);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task OnError_ReactivatesAccountsAndNotifiesTargetServer()
	{
		var task = TestTask();
		var repository = new FakePlayerTransferRepository(task);
		var registry = new FakeGameServerRegistry();
		registry.AddOnlineServer(1);
		registry.AddOnlineServer(2);
		var sourceAccount = new Account { Id = 100, Name = "source", Activated = 1 };
		var targetAccount = new Account { Id = 101, Name = "target", Activated = 1 };
		var service = CreateService(repository, registry, new FakeAccountRepository(sourceAccount, targetAccount));
		await service.VerifyNewTasksAsync();
		await service.RequestTransferAsync(task.Id, "character", new byte[] { 1, 2, 3 });

		await service.OnErrorAsync(task.Id, "failed");

		Assert.Equal(PlayerTransferTask.StatusError, task.Status);
		Assert.Equal("failed", task.Comment);
		Assert.Equal(1, sourceAccount.Activated);
		Assert.Equal(1, targetAccount.Activated);
		Assert.Equal(PlayerTransferResultStatus.Error, PacketResult(registry.LastPacketTo(2)));
	}

	private static PlayerTransferService CreateService(
		FakePlayerTransferRepository repository,
		FakeGameServerRegistry registry,
		FakeAccountRepository accountRepository)
	{
		return new PlayerTransferService(repository, registry, accountRepository, NullLogger<PlayerTransferService>.Instance);
	}

	private static PlayerTransferTask TestTask()
	{
		return new PlayerTransferTask
		{
			Id = 10,
			SourceServerId = 1,
			TargetServerId = 2,
			SourceAccountId = 100,
			TargetAccountId = 101,
			PlayerId = 5000,
			Status = PlayerTransferTask.StatusWait
		};
	}

	private static PlayerTransferResultStatus PacketResult(GsServerPacket packet)
	{
		var payload = packet.SerializePayload();
		Assert.Equal(12, payload[0]);
		return (PlayerTransferResultStatus)BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1, 4));
	}

	private sealed class FakePlayerTransferRepository : IPlayerTransferRepository
	{
		private readonly IReadOnlyCollection<PlayerTransferTask> _tasks;

		public FakePlayerTransferRepository(params PlayerTransferTask[] tasks)
		{
			_tasks = tasks;
		}

		public List<PlayerTransferTask> UpdatedTasks { get; } = new();

		public Task<IReadOnlyCollection<PlayerTransferTask>> GetNewAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_tasks);
		}

		public Task<bool> UpdateAsync(PlayerTransferTask task, CancellationToken cancellationToken = default)
		{
			UpdatedTasks.Add(
				new PlayerTransferTask
				{
					Id = task.Id,
					SourceServerId = task.SourceServerId,
					TargetServerId = task.TargetServerId,
					SourceAccountId = task.SourceAccountId,
					TargetAccountId = task.TargetAccountId,
					PlayerId = task.PlayerId,
					Status = task.Status,
					Comment = task.Comment,
				});
			return Task.FromResult(true);
		}
	}

	private sealed class FakeGameServerRegistry : IGameServerRegistry
	{
		private readonly Dictionary<byte, GameServerInfo> _gameServers = new();

		public List<(byte ServerId, GsServerPacket Packet)> SentPackets { get; } = new();

		public GameServerInfo AddOnlineServer(byte serverId)
		{
			var server = new GameServerInfo(serverId, "*", "pass");
			server.MarkOnline(new byte[] { 127, 0, 0, serverId }, 7000, 0, 100);
			_gameServers[serverId] = server;
			return server;
		}

		public GsServerPacket LastPacketTo(byte serverId)
		{
			return SentPackets.Last(sent => sent.ServerId == serverId).Packet;
		}

		public IReadOnlyCollection<GameServerInfo> GetGameServers() => _gameServers.Values.ToArray();

		public GameServerInfo? GetGameServer(byte serverId)
		{
			_gameServers.TryGetValue(serverId, out var server);
			return server;
		}

		public void RegisterKnownServer(GameServerInfo gameServerInfo)
		{
			_gameServers[gameServerInfo.Id] = gameServerInfo;
		}

		public GsAuthResponse RegisterGameServer(GameServerAuthRequest request, string remoteAddress, IGameServerSession? session = null) => GsAuthResponse.AUTHED;

		public void UnregisterGameServer(byte serverId, IGameServerSession session)
		{
		}

		public GameServerInfo? FindLoggedInAccountGameServer(int accountId)
		{
			return _gameServers.Values.FirstOrDefault(server => server.IsAccountOnGameServer(accountId));
		}

		public Task<bool> KickAccountFromGameServerAsync(int accountId, bool notifyDoubleLogin) => Task.FromResult(false);

		public IReadOnlyDictionary<byte, int> GetOfflineGameServerCharacterCounts() => new Dictionary<byte, int>();

		public Task RequestOnlineGameServerCharacterCountsAsync(int accountId) => Task.CompletedTask;

		public Task<bool> SendPacketToGameServerAsync(byte serverId, GsServerPacket packet)
		{
			SentPackets.Add((serverId, packet));
			return Task.FromResult(_gameServers.ContainsKey(serverId));
		}
	}

	private sealed class FakeAccountRepository : IAccountRepository
	{
		private readonly Dictionary<int, Account> _accounts;

		public FakeAccountRepository(params Account[] accounts)
		{
			_accounts = accounts.ToDictionary(account => account.Id);
		}

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

		public Task<bool> UpdateLastMacAsync(int accountId, string mac, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<bool> UpdateLastHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<bool> UpdateAllowedHddSerialAsync(int accountId, string hddSerial, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<string> GetLastIpAsync(int accountId, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);

		public Task<bool> UpdateAccountAsync(Account account, bool useExternalAuth, CancellationToken cancellationToken = default)
		{
			_accounts[account.Id] = account;
			return Task.FromResult(true);
		}

		public Task UpdateLastServerAsync(int accountId, sbyte lastServer, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task UpdateMembershipAsync(int accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}
}
