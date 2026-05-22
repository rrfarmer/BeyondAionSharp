using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcRandomWalkServiceTests
{
	[Fact]
	public async Task StartRandomWalkingAsync_SchedulesRandomTargetAndBroadcastsMove()
	{
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var options = CreateOptions(minimumDelaySeconds: 0, maximumDelaySeconds: 0);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(
				objectId: 1,
				position: new WorldPosition(210010000, 100, 200, 10, 3),
				randomWalkRange: 10);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var registry = new CapturingConnectionRegistry();
			var aiStates = new WorldNpcAiStateService();
			var randomValues = new Queue<float>([12, 4]);
			var service = new WorldNpcRandomWalkService(
				world,
				registry,
				options,
				threadPoolManager,
				aiStates,
				maxExclusive =>
				{
					Assert.Equal(20, maxExclusive);
					return randomValues.Dequeue();
				},
				(minimum, maximum) =>
				{
					Assert.Equal(0, minimum);
					Assert.Equal(0, maximum);
					return 0;
				});

			var result = await service.StartRandomWalkingAsync(npc.ObjectId);

			Assert.True(result.Started);
			Assert.Equal(WorldNpcRandomWalkStartStatus.Scheduled, result.Status);
			Assert.Equal(TimeSpan.Zero, result.Delay);
			Assert.True(aiStates.TryGetState(npc.ObjectId, out var aiState));
			Assert.NotNull(aiState);
			Assert.Equal(WorldNpcAiState.Walking, aiState.State);
			Assert.Equal(WorldNpcAiSubState.WalkRandom, aiState.SubState);

			await WaitUntilAsync(() => registry.Broadcasts.Count == 1
				&& service.TryGetActiveState(npc.ObjectId, out var activeState)
				&& activeState?.Target != null);

			Assert.True(service.TryGetActiveState(npc.ObjectId, out var state));
			Assert.NotNull(state);
			Assert.NotNull(state.Target);
			Assert.Equal(102, state.Target.X);
			Assert.Equal(194, state.Target.Y);
			Assert.Equal(10, state.Target.Z);
			Assert.Equal(1, state.BroadcastCount);
			Assert.Equal(0, service.PendingArrivalTaskCount);
			Assert.Equal(0, service.PendingMovementTickTaskCount);
			Assert.Equal(npc.ObjectId, registry.Broadcasts[0].SourceObjectId);
			Assert.Equal(npc.Position, registry.Broadcasts[0].SourcePosition);

			using var reader = new PacketBuffer(SerializeUnencryptedPayload(registry.Broadcasts[0].Packet));
			Assert.Equal(npc.ObjectId, reader.ReadD());
			Assert.Equal(100, reader.ReadF());
			Assert.Equal(200, reader.ReadF());
			Assert.Equal(10, reader.ReadF());
			Assert.Equal(3, (int)reader.ReadC());
			Assert.Equal(MovementMask.NpcStartMove, reader.ReadC());
			Assert.Equal(102, reader.ReadF());
			Assert.Equal(194, reader.ReadF());
			Assert.Equal(10, reader.ReadF());
			Assert.Equal(0, reader.Remaining);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task StartRandomWalkingAsync_InterpolatesToTargetAndSchedulesNextRandomPointAfterArrival()
	{
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var options = CreateOptions(minimumDelaySeconds: 0, maximumDelaySeconds: 60);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(
				objectId: 1,
				position: new WorldPosition(210010000, 0, 0, 0, 3),
				randomWalkRange: 1,
				runSpeed: 1);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var registry = new CapturingConnectionRegistry();
			var aiStates = new WorldNpcAiStateService();
			var randomValues = new Queue<float>([1.5f, 1f]);
			var delays = new Queue<int>([0, 60]);
			var service = new WorldNpcRandomWalkService(
				world,
				registry,
				options,
				threadPoolManager,
				aiStates,
				maxExclusive =>
				{
					Assert.Equal(2, maxExclusive);
					return randomValues.Dequeue();
				},
				(minimum, maximum) =>
				{
					Assert.Equal(0, minimum);
					Assert.Equal(60, maximum);
					return delays.Dequeue();
				});

			var result = await service.StartRandomWalkingAsync(npc.ObjectId);

			Assert.True(result.Started);
			Assert.Equal(TimeSpan.Zero, result.Delay);
			await WaitUntilAsync(() => registry.Broadcasts.Count == 1
				&& service.TryGetActiveState(npc.ObjectId, out var activeState)
				&& activeState?.Target != null
				&& service.PendingArrivalTaskCount == 1);

			Assert.Equal(1, service.PendingMovementTickTaskCount);
			await WaitUntilAsync(() =>
			{
				if (!world.TryGetObject(npc.ObjectId, out var movedObject) || movedObject is not WorldNpc movedNpc)
					return false;

				return movedNpc.Position.X > 0 && movedNpc.Position.X < 0.5f;
			});

			Assert.True(world.TryGetObject(npc.ObjectId, out var interpolatedObject));
			var interpolatedNpc = Assert.IsType<WorldNpc>(interpolatedObject);
			Assert.InRange(interpolatedNpc.Position.X, 0.05f, 0.49f);
			Assert.Equal(0, interpolatedNpc.Position.Y);
			Assert.Equal(0, interpolatedNpc.Position.Z);

			await WaitUntilAsync(() =>
			{
				if (!world.TryGetObject(npc.ObjectId, out var reachedObject) || reachedObject is not WorldNpc reachedNpc)
					return false;

				return Math.Abs(reachedNpc.Position.X - 0.5f) < 0.0001f
					&& service.PendingTargetTaskCount == 1
					&& service.PendingArrivalTaskCount == 0
					&& service.PendingMovementTickTaskCount == 0;
			});

			Assert.True(service.TryGetActiveState(npc.ObjectId, out var state));
			Assert.NotNull(state);
			Assert.Null(state.Target);
			Assert.Equal(TimeSpan.FromSeconds(60), state.Delay);
			Assert.Single(registry.Broadcasts);
			Assert.True(aiStates.TryGetState(npc.ObjectId, out var aiState));
			Assert.NotNull(aiState);
			Assert.Equal(WorldNpcAiState.Walking, aiState.State);
			Assert.Equal(WorldNpcAiSubState.WalkRandom, aiState.SubState);
			Assert.True(world.TryGetObject(npc.ObjectId, out var reachedObject));
			var reachedNpc = Assert.IsType<WorldNpc>(reachedObject);
			Assert.Equal(0.5f, reachedNpc.Position.X);
			Assert.Equal(0, reachedNpc.Position.Y);
			Assert.Equal(0, reachedNpc.Position.Z);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task StartRandomWalkingAsync_RequiresMovementEnabled()
	{
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var options = CreateOptions(movementEnabled: false);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(
				objectId: 1,
				position: new WorldPosition(210010000, 100, 200, 10, 3),
				randomWalkRange: 10);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var registry = new CapturingConnectionRegistry();
			var aiStates = new WorldNpcAiStateService();
			var service = new WorldNpcRandomWalkService(world, registry, options, threadPoolManager, aiStates);

			var result = await service.StartRandomWalkingAsync(npc.ObjectId);

			Assert.False(result.Started);
			Assert.Equal(WorldNpcRandomWalkStartStatus.MovementDisabled, result.Status);
			Assert.Equal(0, service.ActiveStateCount);
			Assert.False(aiStates.TryGetState(npc.ObjectId, out _));
			Assert.Empty(registry.Broadcasts);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task StartRandomWalkingAsync_RequiresRandomWalkRange()
	{
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var options = CreateOptions();
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var npc = CreateNpc(
				objectId: 1,
				position: new WorldPosition(210010000, 100, 200, 10, 3),
				randomWalkRange: 0);
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			var registry = new CapturingConnectionRegistry();
			var service = new WorldNpcRandomWalkService(world, registry, options, threadPoolManager);

			var result = await service.StartRandomWalkingAsync(npc.ObjectId);

			Assert.False(result.Started);
			Assert.Equal(WorldNpcRandomWalkStartStatus.NotRandomWalker, result.Status);
			Assert.Equal(0, service.ActiveStateCount);
			Assert.Empty(registry.Broadcasts);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	private static GameServerOptions CreateOptions(
		bool movementEnabled = true,
		int minimumDelaySeconds = 3,
		int maximumDelaySeconds = 15)
	{
		return new GameServerOptions
		{
			Ai = new GameServerAiOptions
			{
				NpcMovementEnabled = movementEnabled,
				NpcMovementMinimumDelaySeconds = minimumDelaySeconds,
				NpcMovementMaximumDelaySeconds = maximumDelaySeconds,
			},
		};
	}

	private static WorldNpc CreateNpc(
		int objectId,
		WorldPosition position,
		int randomWalkRange,
		float runSpeed = 0)
	{
		return new WorldNpc(
			ObjectId: objectId,
			TemplateId: 203000,
			Template: new NpcTemplateSummary(
				203000,
				"random-walker",
				NameId: 203000,
				Level: 1,
				Rank: "NORMAL",
				Rating: "NORMAL",
				Race: "ELYOS",
				Tribe: "GENERAL",
				Type: "GENERAL",
				RunSpeed: runSpeed),
			Position: position,
			RandomWalkRange: randomWalkRange,
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
