using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerRouteWalkingServiceTests
{
	[Fact]
	public async Task StartRouteWalkingAsync_StartsSingleWalkerAndStoresBroadcastState()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-start-single-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 1, formation: "POINT", rows: "");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 8, 0, 0, 7), walkerId: "route-a", walkerIndex: 0);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var cache = CreateCache(context, [npc]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry);

			var result = await service.StartRouteWalkingAsync(npc.ObjectId);

			Assert.True(result.Started);
			Assert.Equal(WorldNpcWalkerRouteWalkingStartStatus.Started, result.Status);
			Assert.Equal(1, result.BroadcastCount);
			var state = Assert.Single(result.States);
			Assert.Equal(1, service.ActiveStateCount);
			Assert.True(service.TryGetActiveState(npc.ObjectId, out var activeState));
			Assert.Equal(state, activeState);
			Assert.Equal(1, state.TargetStepIndex);
			Assert.Equal(10, state.Target.X);
			Assert.Single(registry.Broadcasts);
			using var reader = new PacketBuffer(SerializeUnencryptedPayload(registry.Broadcasts[0].Packet));
			Assert.Equal(npc.ObjectId, reader.ReadD());
			Assert.Equal(8, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(7, (int)reader.ReadC());
			Assert.Equal(0xE0, (int)reader.ReadC());
			Assert.Equal(10, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.Remaining);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task StartRouteWalkingAsync_SetsNpcAiStateToWalkPath()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-ai-start-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 1, formation: "POINT", rows: "");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 8, 0, 0, 7), walkerId: "route-a", walkerIndex: 0);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var cache = CreateCache(context, [npc]);
			var registry = new CapturingConnectionRegistry();
			var aiStates = new WorldNpcAiStateService();
			var service = CreateService(context, world, cache, registry, aiStates: aiStates);

			var result = await service.StartRouteWalkingAsync(npc.ObjectId);

			Assert.True(result.Started);
			Assert.True(aiStates.TryGetState(npc.ObjectId, out var aiState));
			Assert.NotNull(aiState);
			Assert.Equal(WorldNpcAiState.Walking, aiState.State);
			Assert.Equal(WorldNpcAiSubState.WalkPath, aiState.SubState);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task StartRouteWalkingAsync_StartsWholeFormationFromOneMember()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-start-formation-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 2, formation: "SQUARE", rows: "2");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var first = CreateNpc(1, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 1);
			var second = CreateNpc(2, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 2);
			Assert.True(world.TryAddObject(first.ObjectId, first));
			Assert.True(world.TryAddObject(second.ObjectId, second));
			var cache = CreateCache(context, [first, second]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry);

			var result = await service.StartRouteWalkingAsync(first.ObjectId);

			Assert.True(result.Started);
			Assert.Equal(2, result.States.Count);
			Assert.Equal(2, result.BroadcastCount);
			Assert.Equal(2, service.ActiveStateCount);
			Assert.All(result.States, state =>
			{
				Assert.True(state.IsFormationMember);
				Assert.Equal(0, state.GroupStep);
				Assert.Equal(0, state.TargetStepIndex);
				Assert.True(service.TryGetActiveState(state.ObjectId, out _));
			});
			Assert.Equal([2, 1], result.States.Select(state => state.ObjectId).ToArray());
			Assert.Equal([2, 1], registry.Broadcasts.Select(broadcast => broadcast.SourceObjectId).ToArray());
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task TargetReachedAsync_AdvancesSingleWalkerAndBroadcastsNextTarget()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-target-reached-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 1, formation: "POINT", rows: "");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 0);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var cache = CreateCache(context, [npc]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry);
			var start = await service.StartRouteWalkingAsync(npc.ObjectId);
			Assert.True(start.Started);

			var result = await service.TargetReachedAsync(npc.ObjectId);

			Assert.True(result.Handled);
			Assert.Equal(WorldNpcWalkerRouteWalkingTargetReachedStatus.Advanced, result.Status);
			Assert.Equal(1, result.BroadcastCount);
			Assert.Equal(TimeSpan.Zero, result.RestDelay);
			Assert.NotNull(result.State);
			Assert.Equal(1, result.State.TargetStepIndex);
			Assert.Equal(10, result.State.Target.X);
			Assert.Equal(0, service.PendingRestTaskCount);
			Assert.True(service.TryGetActiveState(npc.ObjectId, out var activeState));
			Assert.Equal(result.State, activeState);
			Assert.Equal(2, registry.Broadcasts.Count);
			using var reader = new PacketBuffer(SerializeUnencryptedPayload(registry.Broadcasts[1].Packet));
			Assert.Equal(npc.ObjectId, reader.ReadD());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, (int)reader.ReadC());
			Assert.Equal(0xE0, (int)reader.ReadC());
			Assert.Equal(10, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.Remaining);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task StartRouteWalkingAsync_SchedulesMoveArrivalAndAdvancesSingleWalker()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-arrival-schedule-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 1, formation: "POINT", rows: "");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 9.9f, 0, 0, 3), walkerId: "route-a", walkerIndex: 0, runSpeed: 1);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var cache = CreateCache(context, [npc]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry, threadPoolManager);

			var start = await service.StartRouteWalkingAsync(npc.ObjectId);

			Assert.True(start.Started);
			Assert.Equal(1, service.PendingArrivalTaskCount);
			Assert.Equal(1, start.States[0].TargetStepIndex);
			await WaitUntilAsync(() => registry.Broadcasts.Count >= 2 && service.PendingArrivalTaskCount == 1);
			Assert.True(service.TryGetActiveState(npc.ObjectId, out var activeState));
			Assert.NotNull(activeState);
			Assert.Equal(0, activeState.TargetStepIndex);
			Assert.True(world.TryGetObject(npc.ObjectId, out var movedObject));
			var movedNpc = Assert.IsType<WorldNpc>(movedObject);
			Assert.Equal(10, movedNpc.Position.X);
			Assert.Equal(0, movedNpc.Position.Y);
			Assert.Equal(0, movedNpc.Position.Z);
			Assert.Equal(3, movedNpc.Position.Heading);
			Assert.Equal(10, registry.Broadcasts[1].SourcePosition.X);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task StartRouteWalkingAsync_InterpolatesNpcPositionBeforeMoveArrival()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-interpolate-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 1, formation: "POINT", rows: "");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 9, 0, 0, 3), walkerId: "route-a", walkerIndex: 0, runSpeed: 0.5f);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var cache = CreateCache(context, [npc]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry, threadPoolManager);

			var start = await service.StartRouteWalkingAsync(npc.ObjectId);

			Assert.True(start.Started);
			Assert.Equal(1, service.PendingArrivalTaskCount);
			Assert.Equal(1, service.PendingMovementTickTaskCount);
			await WaitUntilAsync(() =>
			{
				if (!world.TryGetObject(npc.ObjectId, out var movedObject) || movedObject is not WorldNpc movedNpc)
					return false;

				return movedNpc.Position.X > 9 && movedNpc.Position.X < 10 && registry.Broadcasts.Count == 1;
			});

			Assert.True(world.TryGetObject(npc.ObjectId, out var interpolatedObject));
			var interpolatedNpc = Assert.IsType<WorldNpc>(interpolatedObject);
			Assert.InRange(interpolatedNpc.Position.X, 9.05f, 9.95f);
			Assert.Equal(0, interpolatedNpc.Position.Y);
			Assert.Equal(0, interpolatedNpc.Position.Z);
			Assert.Equal(3, interpolatedNpc.Position.Heading);
			Assert.Single(registry.Broadcasts);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task TargetReachedAsync_UpdatesNpcPositionToReachedTargetBeforeNextBroadcast()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-target-position-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 1, formation: "POINT", rows: "");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 8, 0, 0, 7), walkerId: "route-a", walkerIndex: 0);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var cache = CreateCache(context, [npc]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry);
			var start = await service.StartRouteWalkingAsync(npc.ObjectId);
			Assert.True(start.Started);
			Assert.Equal(1, start.States[0].TargetStepIndex);

			var result = await service.TargetReachedAsync(npc.ObjectId);

			Assert.True(result.Handled);
			Assert.Equal(WorldNpcWalkerRouteWalkingTargetReachedStatus.Advanced, result.Status);
			Assert.NotNull(result.State);
			Assert.Equal(0, result.State.TargetStepIndex);
			Assert.True(world.TryGetObject(npc.ObjectId, out var movedObject));
			var movedNpc = Assert.IsType<WorldNpc>(movedObject);
			Assert.Equal(10, movedNpc.Position.X);
			Assert.Equal(0, movedNpc.Position.Y);
			Assert.Equal(0, movedNpc.Position.Z);
			Assert.Equal(7, movedNpc.Position.Heading);
			Assert.Equal(10, registry.Broadcasts[1].SourcePosition.X);
			using var reader = new PacketBuffer(SerializeUnencryptedPayload(registry.Broadcasts[1].Packet));
			Assert.Equal(npc.ObjectId, reader.ReadD());
			Assert.Equal(10, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(7, (int)reader.ReadC());
			Assert.Equal(0xE0, (int)reader.ReadC());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.ReadF());
			Assert.Equal(0, reader.Remaining);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task TargetReachedAsync_SchedulesBroadcastAfterRestTime()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-rest-schedule-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(
				tempPath,
				pool: 1,
				formation: "POINT",
				rows: "",
				firstRestTime: 25);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 0);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var cache = CreateCache(context, [npc]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry, threadPoolManager);
			var start = await service.StartRouteWalkingAsync(npc.ObjectId);
			Assert.True(start.Started);

			var result = await service.TargetReachedAsync(npc.ObjectId);

			Assert.True(result.Handled);
			Assert.Equal(WorldNpcWalkerRouteWalkingTargetReachedStatus.Scheduled, result.Status);
			Assert.Equal(TimeSpan.FromMilliseconds(25), result.RestDelay);
			Assert.Equal(0, result.BroadcastCount);
			Assert.NotNull(result.State);
			Assert.Equal(1, result.State.TargetStepIndex);
			Assert.Equal(1, service.PendingRestTaskCount);
			Assert.True(service.TryGetActiveState(npc.ObjectId, out var activeState));
			Assert.Equal(result.State, activeState);
			Assert.Single(registry.Broadcasts);

			await WaitUntilAsync(() => registry.Broadcasts.Count == 2 && service.PendingRestTaskCount == 0);

			Assert.Equal(npc.ObjectId, registry.Broadcasts[1].SourceObjectId);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task TargetReachedAsync_StopsLoopNoneWalkerAtLastStep()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-loop-none-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(
				tempPath,
				pool: 1,
				formation: "POINT",
				rows: "",
				loopType: "NONE");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 10, 0, 0, 0), walkerId: "route-a", walkerIndex: 0);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var cache = CreateCache(context, [npc]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry);
			var start = await service.StartRouteWalkingAsync(npc.ObjectId);
			Assert.True(start.Started);
			Assert.True(service.TryGetActiveState(npc.ObjectId, out var startedState));
			Assert.NotNull(startedState);
			Assert.True(startedState.Target.ShouldStop);

			var result = await service.TargetReachedAsync(npc.ObjectId);

			Assert.True(result.Handled);
			Assert.Equal(WorldNpcWalkerRouteWalkingTargetReachedStatus.Stopped, result.Status);
			Assert.Null(result.State);
			Assert.Equal(0, result.BroadcastCount);
			Assert.False(service.TryGetActiveState(npc.ObjectId, out _));
			Assert.Equal(0, service.ActiveStateCount);
			Assert.Single(registry.Broadcasts);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task TargetReachedAsync_WaitsForAllFormationMembersBeforeAdvancingGroup()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-formation-target-reached-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 2, formation: "SQUARE", rows: "2");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var first = CreateNpc(1, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 1);
			var second = CreateNpc(2, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 2);
			Assert.True(world.TryAddObject(first.ObjectId, first));
			Assert.True(world.TryAddObject(second.ObjectId, second));
			var cache = CreateCache(context, [first, second]);
			var registry = new CapturingConnectionRegistry();
			var aiStates = new WorldNpcAiStateService();
			var service = CreateService(context, world, cache, registry, aiStates: aiStates);
			var start = await service.StartRouteWalkingAsync(first.ObjectId);
			Assert.True(start.Started);

			var waiting = await service.TargetReachedAsync(first.ObjectId);

			Assert.True(waiting.Handled);
			Assert.Equal(WorldNpcWalkerRouteWalkingTargetReachedStatus.WaitingGroup, waiting.Status);
			Assert.Equal(1, waiting.ArrivedCount);
			Assert.Equal(2, waiting.ExpectedArrivalCount);
			Assert.Equal(2, service.ActiveStateCount);
			Assert.Equal(1, service.ActiveFormationStateCount);
			Assert.Equal(2, registry.Broadcasts.Count);
			Assert.True(aiStates.TryGetState(first.ObjectId, out var firstWaitingState));
			Assert.NotNull(firstWaitingState);
			Assert.Equal(WorldNpcAiState.Walking, firstWaitingState.State);
			Assert.Equal(WorldNpcAiSubState.WalkWaitGroup, firstWaitingState.SubState);
			Assert.True(aiStates.TryGetState(second.ObjectId, out var secondWalkingState));
			Assert.NotNull(secondWalkingState);
			Assert.Equal(WorldNpcAiSubState.WalkPath, secondWalkingState.SubState);

			var advanced = await service.TargetReachedAsync(second.ObjectId);

			Assert.True(advanced.Handled);
			Assert.Equal(WorldNpcWalkerRouteWalkingTargetReachedStatus.Advanced, advanced.Status);
			Assert.Equal(2, advanced.BroadcastCount);
			Assert.Equal(2, advanced.States.Count);
			Assert.Equal([2, 1], advanced.States.Select(state => state.ObjectId).ToArray());
			Assert.All(advanced.States, state =>
			{
				Assert.True(state.IsFormationMember);
				Assert.Equal(1, state.TargetStepIndex);
				Assert.Equal(1, state.GroupStep);
				Assert.True(service.TryGetActiveState(state.ObjectId, out var activeState));
				Assert.Equal(state, activeState);
			});
			Assert.Equal(4, registry.Broadcasts.Count);
			Assert.True(aiStates.TryGetState(first.ObjectId, out var firstResumedState));
			Assert.NotNull(firstResumedState);
			Assert.Equal(WorldNpcAiSubState.WalkPath, firstResumedState.SubState);
			Assert.True(aiStates.TryGetState(second.ObjectId, out var secondResumedState));
			Assert.NotNull(secondResumedState);
			Assert.Equal(WorldNpcAiSubState.WalkPath, secondResumedState.SubState);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task TargetReachedAsync_SchedulesFormationMovementAfterRestTime()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-formation-rest-schedule-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(
				tempPath,
				pool: 2,
				formation: "SQUARE",
				rows: "2",
				firstRestTime: 25);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var first = CreateNpc(1, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 1);
			var second = CreateNpc(2, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 2);
			Assert.True(world.TryAddObject(first.ObjectId, first));
			Assert.True(world.TryAddObject(second.ObjectId, second));
			var cache = CreateCache(context, [first, second]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry, threadPoolManager);
			var start = await service.StartRouteWalkingAsync(first.ObjectId);
			Assert.True(start.Started);
			var waiting = await service.TargetReachedAsync(first.ObjectId);
			Assert.Equal(WorldNpcWalkerRouteWalkingTargetReachedStatus.WaitingGroup, waiting.Status);

			var scheduled = await service.TargetReachedAsync(second.ObjectId);

			Assert.True(scheduled.Handled);
			Assert.Equal(WorldNpcWalkerRouteWalkingTargetReachedStatus.Scheduled, scheduled.Status);
			Assert.Equal(TimeSpan.FromMilliseconds(25), scheduled.RestDelay);
			Assert.Equal(0, scheduled.BroadcastCount);
			Assert.Equal(2, scheduled.States.Count);
			Assert.Equal(2, service.PendingRestTaskCount);
			Assert.Equal(2, registry.Broadcasts.Count);

			await WaitUntilAsync(() => registry.Broadcasts.Count == 4 && service.PendingRestTaskCount == 0);

			Assert.Equal([2, 1], registry.Broadcasts.Take(2).Select(broadcast => broadcast.SourceObjectId).ToArray());
			Assert.Equal([1, 2], registry.Broadcasts.Skip(2).Select(broadcast => broadcast.SourceObjectId).OrderBy(objectId => objectId).ToArray());
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task StartWorldRouteWalkingAsync_StartsEachFormationOnce()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-start-world-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 2, formation: "SQUARE", rows: "2");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var first = CreateNpc(1, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 1);
			var second = CreateNpc(2, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 2);
			Assert.True(world.TryAddObject(first.ObjectId, first));
			Assert.True(world.TryAddObject(second.ObjectId, second));
			var cache = CreateCache(context, [first, second]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry);

			var result = await service.StartWorldRouteWalkingAsync(210010000);

			Assert.True(result.Started);
			Assert.Equal(WorldNpcWalkerRouteWalkingWorldStartStatus.Started, result.Status);
			Assert.Equal(1, result.RouteStartCount);
			Assert.Equal(2, result.StateCount);
			Assert.Equal(2, result.BroadcastCount);
			var start = Assert.Single(result.Results);
			Assert.True(start.Started);
			Assert.Equal(2, service.ActiveStateCount);
			Assert.Equal(1, service.ActiveFormationStateCount);
			Assert.Equal([2, 1], registry.Broadcasts.Select(broadcast => broadcast.SourceObjectId).ToArray());
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task StartRouteWalkingAsync_DoesNotRestartAlreadyActiveWalker()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-start-active-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 1, formation: "POINT", rows: "");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 8, 0, 0, 7), walkerId: "route-a", walkerIndex: 0);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var cache = CreateCache(context, [npc]);
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, cache, registry);
			var firstStart = await service.StartRouteWalkingAsync(npc.ObjectId);
			Assert.True(firstStart.Started);

			var secondStart = await service.StartRouteWalkingAsync(npc.ObjectId);

			Assert.False(secondStart.Started);
			Assert.Equal(WorldNpcWalkerRouteWalkingStartStatus.AlreadyWalking, secondStart.Status);
			Assert.Equal(1, service.ActiveStateCount);
			Assert.Single(registry.Broadcasts);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task StartRouteWalkingAsync_RequiresCachedActiveWalkerPlan()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walk-start-missing-cache-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath, pool: 1, formation: "POINT", rows: "");
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(1, new WorldPosition(210010000, 0, 0, 0, 0), walkerId: "route-a", walkerIndex: 0);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var registry = new CapturingConnectionRegistry();
			var service = CreateService(context, world, new WorldNpcWalkerSpawnPlanCacheService(), registry);

			var result = await service.StartRouteWalkingAsync(npc.ObjectId);

			Assert.False(result.Started);
			Assert.Equal(WorldNpcWalkerRouteWalkingStartStatus.MissingWorldPlan, result.Status);
			Assert.Equal(0, service.ActiveStateCount);
			Assert.Empty(registry.Broadcasts);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	private static WorldNpcWalkerRouteWalkingService CreateService(
		GameServerRuntimeContext context,
		GameWorld world,
		IWorldNpcWalkerSpawnPlanCacheService cache,
		IGameClientConnectionRegistry registry,
		ThreadPoolManager? threadPoolManager = null,
		WorldNpcAiStateService? aiStates = null)
	{
		var routeService = new WorldNpcWalkerRouteService();
		var movementStateService = new WorldNpcWalkerMovementStateService();
		var broadcastService = new WorldNpcWalkerMovementBroadcastService(world, registry);
		return new WorldNpcWalkerRouteWalkingService(
			context,
			world,
			cache,
			routeService,
			movementStateService,
			broadcastService,
			threadPoolManager,
			aiStates);
	}

	private static WorldNpcWalkerSpawnPlanCacheService CreateCache(
		GameServerRuntimeContext context,
		IReadOnlyList<WorldNpc> npcs)
	{
		var staticData = context.DataManager!.StaticData;
		var cache = new WorldNpcWalkerSpawnPlanCacheService();
		cache.RefreshWorldPlans(npcs, staticData.WalkerTemplates, staticData.WalkerVersions);
		return cache;
	}

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextWithWalkerDataAsync(
		string tempPath,
		int pool,
		string formation,
		string rows,
		string loopType = "NORMAL",
		int firstRestTime = 0,
		int secondRestTime = 0)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		var rowsAttribute = string.IsNullOrWhiteSpace(rows) ? string.Empty : $" rows=\"{rows}\"";
		File.WriteAllText(
			staticDataFile,
			$"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<npc_walker>
					<walker_template route_id="route-a" pool="{pool}" formation="{formation}" loop_type="{loopType}"{rowsAttribute}>
						<routestep x="0" y="0" z="0" rest_time="{firstRestTime}" />
						<routestep x="10" y="0" z="0" rest_time="{secondRestTime}" />
					</walker_template>
				</npc_walker>
			</static_data>
			""");
		File.WriteAllText(schemaFile, """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" />""");
		var dataManager = await DataManager.LoadAsync(
			new XmlDataLoaderOptions
			{
				MainXmlFilePath = staticDataFile,
				CacheXmlFilePath = cacheFile,
				SchemaFilePath = schemaFile,
				ValidateWhenCacheChanges = false,
			});
		var context = new GameServerRuntimeContext();
		context.SetDataManager(dataManager);
		return context;
	}

	private static WorldNpc CreateNpc(
		int objectId,
		WorldPosition position,
		string walkerId,
		int walkerIndex,
		float runSpeed = 0)
	{
		return new WorldNpc(
			ObjectId: objectId,
			TemplateId: 203000,
			Template: new NpcTemplateSummary(
				203000,
				"walker-npc",
				NameId: 203000,
				Level: 1,
				Rank: "NORMAL",
				Rating: "NORMAL",
				Race: "ELYOS",
				Tribe: "GENERAL",
				Type: "GENERAL",
				RunSpeed: runSpeed),
			Position: position,
			WalkerId: walkerId,
			WalkerIndex: walkerIndex,
			SpawnPosition: position);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
		while (DateTimeOffset.UtcNow < deadline)
		{
			if (condition())
				return;

			await Task.Delay(25);
		}

		Assert.True(condition(), "Condition was not met before the timeout.");
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly object _gate = new();
		private readonly List<BroadcastRecord> _broadcasts = [];

		public IReadOnlyList<BroadcastRecord> Broadcasts
		{
			get
			{
				lock (_gate)
					return _broadcasts.ToArray();
			}
		}

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
			lock (_gate)
				_broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet));
			return Task.FromResult(1);
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

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet);
}
