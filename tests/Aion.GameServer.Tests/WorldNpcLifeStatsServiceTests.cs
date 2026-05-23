using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcLifeStatsServiceTests
{
	[Fact]
	public void Initialize_UsesNpcMaxHpAndMpAsCurrentStats()
	{
		var service = CreateLifeStatsService(out _, out _, out _, out _, out _, out _);
		var npc = CreateWorldNpc(objectId: 1);

		var stats = service.Initialize(npc, maxHp: 125, maxMp: 40);

		Assert.Equal(new WorldNpcLifeStats(125, 40, 125, 40), stats);
		Assert.True(service.TryGetStats(1, out var stored));
		Assert.Equal(stats, stored);
	}

	[Fact]
	public void Clear_RemovesStoredStats()
	{
		var service = CreateLifeStatsService(out _, out _, out _, out _, out _, out _);
		var npc = CreateWorldNpc(objectId: 1);

		service.Initialize(npc, maxHp: 125, maxMp: 40);
		service.Clear(npc.ObjectId);

		Assert.False(service.TryGetStats(npc.ObjectId, out _));
	}

	[Fact]
	public async Task ReduceHpAsync_ReducesHpWithoutDeathAboveZero()
	{
		var service = CreateLifeStatsService(out var spawnService, out var world, out _, out var threadPoolManager, out var aiStates, out _);
		try
		{
			SpawnNpc(spawnService, world, 203081);
			var npc = Assert.Single(world.GetNpcs());

			var result = await service.ReduceHpAsync(
				npc,
				damage: 25,
				maxHp: 100,
				maxMp: 50,
				attacker: CreatePlayer());

			Assert.Equal(WorldNpcLifeStatsDamageStatus.Reduced, result.Status);
			Assert.Equal(new WorldNpcLifeStats(100, 50, 100, 50), result.Previous);
			Assert.Equal(new WorldNpcLifeStats(100, 50, 75, 50), result.Current);
			Assert.Null(result.DeathResult);
			Assert.False(spawnService.HasRespawnTask(npc.ObjectId));
			Assert.False(spawnService.HasDecayTask(npc.ObjectId));
			Assert.False(aiStates.TryGetState(npc.ObjectId, out _));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ReduceHpAsync_TriggersDeathWorkflowOnceWhenHpReachesZero()
	{
		var service = CreateLifeStatsService(out var spawnService, out var world, out _, out var threadPoolManager, out var aiStates, out _);
		try
		{
			SpawnNpc(spawnService, world, 203082);
			var npc = Assert.Single(world.GetNpcs());

			var result = await service.ReduceHpAsync(
				npc,
				damage: 150,
				maxHp: 100,
				maxMp: 50,
				attacker: CreatePlayer(),
				deathOptions: WorldNpcDeathDropOptions.Default with { RewardLoot = false });

			Assert.Equal(WorldNpcLifeStatsDamageStatus.Died, result.Status);
			Assert.Equal(new WorldNpcLifeStats(100, 50, 100, 50), result.Previous);
			Assert.Equal(new WorldNpcLifeStats(100, 50, 0, 0), result.Current);
			Assert.NotNull(result.DeathResult);
			Assert.Equal(WorldNpcDeathDropWorkflowStatus.Scheduled, result.DeathResult.Status);
			Assert.True(result.DeathResult.RespawnScheduled);
			Assert.True(result.DeathResult.DecayScheduled);
			Assert.True(result.DeathResult.AiMarkedDied);
			Assert.True(spawnService.HasRespawnTask(npc.ObjectId));
			Assert.True(spawnService.HasDecayTask(npc.ObjectId));
			Assert.True(aiStates.TryGetState(npc.ObjectId, out var state));
			Assert.Equal(WorldNpcAiState.Died, state!.State);

			var duplicate = await service.ReduceHpAsync(
				npc,
				damage: 1,
				maxHp: 100,
				maxMp: 50,
				attacker: CreatePlayer(),
				deathOptions: WorldNpcDeathDropOptions.Default with { RewardLoot = false });

			Assert.Equal(WorldNpcLifeStatsDamageStatus.AlreadyDead, duplicate.Status);
			Assert.Null(duplicate.DeathResult);
			Assert.Equal(1, spawnService.PendingDecayCount);
			Assert.NotNull(spawnService.CancelDecay(npc.ObjectId));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ReduceHpAsync_ReturnsMissingNpcWithoutInitializingStats()
	{
		var service = CreateLifeStatsService(out _, out _, out _, out var threadPoolManager, out _, out _);
		try
		{
			var result = await service.ReduceHpAsync(
				null,
				damage: 10,
				maxHp: 100,
				attacker: CreatePlayer());

			Assert.Equal(WorldNpcLifeStatsDamageStatus.MissingNpc, result.Status);
			Assert.Null(result.Previous);
			Assert.Null(result.Current);
			Assert.Null(result.DeathResult);
			Assert.False(service.TryGetStats(1, out _));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	private static WorldNpcLifeStatsService CreateLifeStatsService(
		out WorldNpcSpawnService spawnService,
		out GameWorld world,
		out WorldNpcDropRegistrationService dropRegistration,
		out ThreadPoolManager threadPoolManager,
		out WorldNpcAiStateService aiStates,
		out StaticPlaceableStateService staticPlaceables)
	{
		world = new GameWorld(NullLogger<GameWorld>.Instance);
		dropRegistration = new WorldNpcDropRegistrationService();
		threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		aiStates = new WorldNpcAiStateService();
		staticPlaceables = new StaticPlaceableStateService();
		spawnService = new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager,
			connectionRegistry: null,
			staticPlaceables,
			walkerSpawnPlans: null,
			walkerPlacementApplication: null,
			NullLogger<WorldNpcSpawnService>.Instance,
			dropRegistrationLookup: dropRegistration,
			npcAiStates: aiStates);
		var lootService = new WorldNpcLootService(dropRegistration, spawnService, threadPoolManager);
		var broadcastService = new WorldNpcLootBroadcastService(lootService, new NullConnectionRegistry());
		var dropWorkflow = new WorldNpcDropRegistrationWorkflowService(
			new WorldNpcCustomDropService(new CustomNpcDropTable([])),
			dropRegistration,
			broadcastService);
		var deathWorkflow = new WorldNpcDeathDropWorkflowService(spawnService, dropWorkflow, aiStates);
		return new WorldNpcLifeStatsService(deathWorkflow);
	}

	private static void SpawnNpc(WorldNpcSpawnService spawnService, GameWorld world, int npcTemplateId)
	{
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, npcTemplateId, respawnSeconds: 30)]);
		var templates = new NpcTemplateTable([CreateTemplate(npcTemplateId)]);
		spawnService.SpawnWorldNpcs(spawns, templates, [210010000]);
		Assert.Single(world.GetNpcs());
	}

	private static WorldNpc CreateWorldNpc(int objectId)
	{
		return new WorldNpc(
			objectId,
			203080,
			CreateTemplate(203080),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static Player CreatePlayer()
	{
		return new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 };
	}

	private static NpcSpawnSummary CreateSpawn(
		int mapId,
		int npcId,
		int respawnSeconds)
	{
		return new NpcSpawnSummary(
			mapId,
			npcId,
			X: 1,
			Y: 2,
			Z: 3,
			Heading: 0,
			RespawnSeconds: respawnSeconds,
			PoolSize: 0,
			DifficultId: 0,
			Handler: string.Empty,
			StaticId: 0,
			RandomWalkRange: 0,
			WalkerId: string.Empty,
			WalkerIndex: 0,
			Anchor: string.Empty,
			State: 0,
			AiName: string.Empty,
			Custom: false,
			GroupTemporarySchedule: null,
			SpotTemporarySchedule: null);
	}

	private static NpcTemplateSummary CreateTemplate(int templateId)
	{
		return new NpcTemplateSummary(
			templateId,
			$"npc-{templateId}",
			NameId: templateId,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL");
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
