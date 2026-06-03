using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class PlayerGroupOfflineTimeoutSchedulerTests
{
	[Fact]
	public async Task Start_SchedulesJavaOfflineGroupCheckerCadenceWithoutStartupRegistration()
	{
		var observations = new List<ThreadPoolScheduleObservation>();
		await using var threadPoolManager = new ThreadPoolManager(
			NullLogger<ThreadPoolManager>.Instance,
			observations.Add);
		var scheduler = CreateScheduler(threadPoolManager, new GameServerOptions());

		var scheduledTask = scheduler.Start();
		var cancelled = scheduledTask.Cancel();
		await threadPoolManager.ShutdownAsync();

		Assert.True(cancelled);
		var observation = Assert.Single(observations);
		Assert.Equal(ThreadPoolScheduleKind.FixedRate, observation.Kind);
		Assert.Equal(TimeSpan.FromSeconds(1), observation.Delay);
		Assert.Equal(TimeSpan.FromSeconds(30), observation.Period);
	}

	[Fact]
	public async Task RunScanOnceAsync_UsesConfiguredGroupRemoveTimeLikeGroupConfig()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: true);
		var expiredByConfiguredTime = CreatePlayer(1002, "Expired", isOnline: false);
		var stillWaitingAtConfiguredTime = CreatePlayer(1003, "Waiting", isOnline: false);
		groups.CreateOrUpdateGroup(99001, [leader, expiredByConfiguredTime, stillWaitingAtConfiguredTime]);
		groups.UpdateMemberLastOnlineTime(expiredByConfiguredTime, DateTimeOffset.FromUnixTimeMilliseconds(100_000));
		groups.UpdateMemberLastOnlineTime(stillWaitingAtConfiguredTime, DateTimeOffset.FromUnixTimeMilliseconds(450_001));
		var dispatchService = new PlayerGroupOfflineTimeoutDispatchService(groups, registry);
		var timeProvider = new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(700_000));
		var scheduler = new PlayerGroupOfflineTimeoutScheduler(
			threadPoolManager,
			dispatchService,
			new GameServerOptions
			{
				Group = new GameServerGroupOptions
				{
					GroupRemoveTimeSeconds = 250,
				},
			},
			timeProvider);

		var result = await scheduler.RunScanOnceAsync();

		Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(700_000), result.ScanTime);
		Assert.Equal(250, result.GroupRemoveTimeSeconds);
		Assert.Equal(1, result.TimedOutMemberCount);
		Assert.Equal([1002], result.DispatchResults.Select(dispatch => dispatch.TimeoutPlan.TimedOutPlayerObjectId));
		Assert.Equal([1001, 1003], groups.GetMemberObjectIds(99001));
		Assert.Equal(PlayerTeamMembership.None, expiredByConfiguredTime.TeamMembership);
		Assert.Equal(PlayerTeamMembership.Group, stillWaitingAtConfiguredTime.TeamMembership);
	}

	private static PlayerGroupOfflineTimeoutScheduler CreateScheduler(
		ThreadPoolManager threadPoolManager,
		GameServerOptions options)
	{
		var dispatchService = new PlayerGroupOfflineTimeoutDispatchService(
			new PlayerGroupRuntime(),
			new CapturingConnectionRegistry());
		return new PlayerGroupOfflineTimeoutScheduler(
			threadPoolManager,
			dispatchService,
			options,
			new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(700_000)));
	}

	private static Player CreatePlayer(int objectId, string name, bool isOnline)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = isOnline,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
		};
	}

	private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow()
		{
			return utcNow;
		}
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}
}
