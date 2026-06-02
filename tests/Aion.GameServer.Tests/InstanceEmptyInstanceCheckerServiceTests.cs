using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class InstanceEmptyInstanceCheckerServiceTests
{
	[Fact]
	public void CreateCheckPlan_BlocksDestroyWhilePlayersInsideLikeJavaEmptyInstanceCheckerTask()
	{
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, maxPlayers: 6);
		instance.AddPlayer(1001);
		var taskStart = DateTimeOffset.UnixEpoch.AddMinutes(1);
		var now = taskStart.AddHours(1);

		var plan = InstanceEmptyInstanceCheckerService.CreateCheckPlan(
			instance,
			taskStart,
			now,
			TimeSpan.FromMinutes(30));

		Assert.Equal(InstanceEmptyInstanceCheckStatus.PlayersInside, plan.Status);
		Assert.False(plan.ShouldDestroy);
		Assert.Null(plan.DestroyTime);
		Assert.Contains("getPlayersInside", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateCheckPlan_UsesPersonalTeamAndDelayBranchesLikeJavaEmptyInstanceCheckerTask()
	{
		var taskStart = DateTimeOffset.UnixEpoch.AddMinutes(10);
		var destroyDelay = TimeSpan.FromMinutes(30);
		var personal = new WorldMapInstanceRuntimeState(instanceId: 7, ownerId: 1001, maxPlayers: 1);
		var team = new WorldMapInstanceRuntimeState(instanceId: 8, maxPlayers: 6);
		var delayed = new WorldMapInstanceRuntimeState(instanceId: 9, maxPlayers: 6);
		delayed.RemovePlayer(2001, taskStart.AddMinutes(5));

		var personalPlan = InstanceEmptyInstanceCheckerService.CreateCheckPlan(
			personal,
			taskStart,
			taskStart,
			destroyDelay);
		var teamPlan = InstanceEmptyInstanceCheckerService.CreateCheckPlan(
			team,
			taskStart,
			taskStart,
			destroyDelay,
			registeredTeamDisbanded: true);
		var waitingPlan = InstanceEmptyInstanceCheckerService.CreateCheckPlan(
			delayed,
			taskStart,
			taskStart.AddMinutes(20),
			destroyDelay);
		var elapsedPlan = InstanceEmptyInstanceCheckerService.CreateCheckPlan(
			delayed,
			taskStart,
			taskStart.AddMinutes(35).AddMilliseconds(1),
			destroyDelay);

		Assert.Equal(InstanceEmptyInstanceCheckStatus.PersonalInstance, personalPlan.Status);
		Assert.True(personalPlan.ShouldDestroy);
		Assert.Equal(InstanceEmptyInstanceCheckStatus.RegisteredTeamDisbanded, teamPlan.Status);
		Assert.True(teamPlan.ShouldDestroy);
		Assert.Equal(InstanceEmptyInstanceCheckStatus.WaitingForDestroyDelay, waitingPlan.Status);
		Assert.False(waitingPlan.ShouldDestroy);
		Assert.Equal(taskStart.AddMinutes(35), waitingPlan.DestroyTime);
		Assert.Equal(InstanceEmptyInstanceCheckStatus.DestroyDelayElapsed, elapsedPlan.Status);
		Assert.True(elapsedPlan.ShouldDestroy);
		Assert.Contains("calculateDestroyTime", elapsedPlan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Schedule_StoresCancellableFixedRateTaskLikeJavaGetNextAvailableInstance()
	{
		var observations = new List<ThreadPoolScheduleObservation>();
		await using var threadPoolManager = new ThreadPoolManager(
			NullLogger<ThreadPoolManager>.Instance,
			observations.Add);
		var context = new GameServerRuntimeContext();
		var worldMaps = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
		]);
		context.SetWorldMapStates(worldMaps);
		var instance = worldMaps.AddWorldMapInstance(300030000, instanceId: 7);
		Assert.NotNull(instance);
		var service = CreateService(context, threadPoolManager);

		var plan = service.Schedule(
			300030000,
			instance,
			TimeSpan.FromMinutes(30),
			taskStartTime: DateTimeOffset.UnixEpoch.AddMinutes(1));

		Assert.Same(plan.ScheduledTask, instance.EmptyInstanceTask);
		Assert.Equal(TimeSpan.FromSeconds(60), plan.InitialDelay);
		Assert.Equal(TimeSpan.FromSeconds(60), plan.Period);
		var observation = Assert.Single(observations);
		Assert.Equal(ThreadPoolScheduleKind.FixedRate, observation.Kind);
		Assert.Equal(TimeSpan.FromSeconds(60), observation.Delay);
		Assert.Equal(TimeSpan.FromSeconds(60), observation.Period);
		Assert.True(instance.CancelEmptyInstanceTask());
	}

	[Fact]
	public async Task DestroyInstance_CancelsStoredEmptyInstanceTaskBeforeRemovingMapLikeJavaDestroyInstance()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var table = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
		]);
		var instance = table.AddWorldMapInstance(300030000, instanceId: 7);
		Assert.NotNull(instance);
		var scheduledTask = threadPoolManager.ScheduleAtFixedRateTask(
			_ => ValueTask.CompletedTask,
			TimeSpan.FromMinutes(10),
			TimeSpan.FromMinutes(10));
		instance.SetEmptyInstanceTask(scheduledTask);

		var plan = InstanceRuntimeService.DestroyInstance(table, 300030000, instance.InstanceId);

		Assert.True(plan.Removed);
		Assert.True(plan.CanceledEmptyInstanceTask);
		Assert.Null(instance.EmptyInstanceTask);
	}

	private static InstanceEmptyInstanceCheckerService CreateService(
		GameServerRuntimeContext context,
		ThreadPoolManager threadPoolManager)
	{
		var world = new Aion.GameServer.World.World(NullLogger<Aion.GameServer.World.World>.Instance);
		var spawnService = new WorldNpcSpawnService(
			context,
			world,
			new IDFactory(),
			NullLogger<WorldNpcSpawnService>.Instance);
		var walkerRouteWalking = new WorldNpcWalkerRouteWalkingService(
			context,
			world,
			new WorldNpcWalkerSpawnPlanCacheService(),
			new WorldNpcWalkerRouteService(),
			new WorldNpcWalkerMovementStateService(),
			new WorldNpcWalkerMovementBroadcastService(world, new NullConnectionRegistry()));
		var destroyWorkflow = new InstanceDestroyWorkflowService(context, world, spawnService, walkerRouteWalking);
		return new InstanceEmptyInstanceCheckerService(threadPoolManager, destroyWorkflow);
	}

	private sealed class NullConnectionRegistry : IGameClientConnectionRegistry
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
			return Task.FromResult(false);
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
