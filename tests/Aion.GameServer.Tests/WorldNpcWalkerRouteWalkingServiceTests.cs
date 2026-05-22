using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
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
		IGameClientConnectionRegistry registry)
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
			broadcastService);
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
		string rows)
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
					<walker_template route_id="route-a" pool="{pool}" formation="{formation}"{rowsAttribute}>
						<routestep x="0" y="0" z="0" />
						<routestep x="10" y="0" z="0" />
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
		int walkerIndex)
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
				Type: "GENERAL"),
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

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<BroadcastRecord> Broadcasts { get; } = [];

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
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet));
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
